using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace DiscordOverlay;

public record VoiceMember(string Id, string Name, string? AvatarHash, bool Muted, bool Deafened);

public class DiscordClient : IDisposable
{
    // All messages to discord need to be sent as bytes, the format is:
    // [opcode: 4 bytes][length: 4 bytes][JSON as bytes]
    // opcode is the type of message. 0 being a handshake to establish connection.
    private const int OpHandshake = 0;
    private const int OpFrame = 1;
    private const int OpClose = 2;
    private const int OpPing = 3;
    private const int OpPong = 4;

    private static readonly string[] ChannelEvents =
    {
        "VOICE_STATE_CREATE",
        "VOICE_STATE_UPDATE",
        "VOICE_STATE_DELETE",
        "SPEAKING_START",
        "SPEAKING_STOP"
    };

    private readonly Config config;
    private readonly DiscordAuth auth;
    private readonly SemaphoreSlim writeLock = new(1, 1);

    private Socket? socket;
    private NetworkStream? stream;
    private string? channelId;

    public event Action<string?>? StatusChanged;
    public event Action<IReadOnlyList<VoiceMember>>? ChannelJoined;
    public event Action? ChannelLeft;
    public event Action<VoiceMember>? MemberUpdated;
    public event Action<string>? MemberLeft;
    public event Action<string, bool>? SpeakingChanged;

    public DiscordClient(Config config)
    {
        this.config = config;
        auth = new DiscordAuth(config);
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            TimeSpan retryDelay = TimeSpan.FromSeconds(5);

            try
            {
                await ConnectAsync(cancellationToken);
                await SendHandshakeAsync(cancellationToken);
                await AuthenticateAsync(cancellationToken);
                StatusChanged?.Invoke(null);

                await SendCommandAsync("SUBSCRIBE", null, "VOICE_CHANNEL_SELECT", cancellationToken);
                await SendCommandAsync("GET_SELECTED_VOICE_CHANNEL", null, null, cancellationToken);
                await ReadLoopAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex) when (ex is SocketException or IOException or ObjectDisposedException)
            {
                StatusChanged?.Invoke("Waiting for Discord...");
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                StatusChanged?.Invoke(ex.Message);
                retryDelay = TimeSpan.FromSeconds(30);
            }

            Disconnect();

            if (channelId != null)
            {
                channelId = null;
                ChannelLeft?.Invoke();
            }

            try
            {
                await Task.Delay(retryDelay, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task ConnectAsync(CancellationToken cancellationToken)
    {
        string socketPath = FindSocketPath() ?? throw new IOException("Discord is not running.");

        socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        await socket.ConnectAsync(new UnixDomainSocketEndPoint(socketPath), cancellationToken);
        stream = new NetworkStream(socket, ownsSocket: true);
    }

    private static string? FindSocketPath()
    {
        var folders = new List<string>();
        foreach (string variable in new[] { "XDG_RUNTIME_DIR", "TMPDIR", "TMP", "TEMP" })
        {
            string? value = Environment.GetEnvironmentVariable(variable);
            if (!string.IsNullOrEmpty(value))
            {
                folders.Add(value);
            }
        }
        folders.Add("/tmp");

        string[] subfolders = { "", "app/com.discordapp.Discord", "snap.discord", ".flatpak/dev.vencord.Vesktop/xdg-run" };

        for (int i = 0; i < 10; i++)
        {
            foreach (string folder in folders.Distinct())
            {
                foreach (string subfolder in subfolders)
                {
                    string path = Path.Combine(folder, subfolder, $"discord-ipc-{i}");
                    if (File.Exists(path))
                    {
                        return path;
                    }
                }
            }
        }

        return null;
    }

    // Establishes connection with discord
    private async Task SendHandshakeAsync(CancellationToken cancellationToken)
    {
        var handshake = new JsonObject
        {
            ["v"] = 1,
            ["client_id"] = config.ClientId
        };
        await SendAsync(OpHandshake, handshake.ToJsonString(), cancellationToken);

        JsonObject ready = await ReadMessageAsync(cancellationToken);
        if ((string?)ready["evt"] != "READY")
        {
            throw new DiscordException($"Unexpected handshake reply: {ready.ToJsonString()}");
        }
    }

    private async Task AuthenticateAsync(CancellationToken cancellationToken)
    {
        string? accessToken = await auth.GetSavedAccessTokenAsync(cancellationToken);
        if (accessToken != null)
        {
            JsonObject response = await SendAuthenticateAsync(accessToken, cancellationToken);
            if (!IsError(response))
            {
                return;
            }

            auth.ClearSavedToken();
        }

        StatusChanged?.Invoke("Approve the overlay in Discord");

        var authorizeArgs = new JsonObject
        {
            ["client_id"] = config.ClientId,
            ["scopes"] = new JsonArray("rpc", "rpc.voice.read")
        };
        JsonObject authorize = await RequestAsync("AUTHORIZE", authorizeArgs, cancellationToken);
        ThrowIfError(authorize);

        string code = (string?)authorize["data"]?["code"]
            ?? throw new DiscordException("Discord did not send back an authorization code.");

        accessToken = await auth.ExchangeCodeAsync(code, cancellationToken);

        JsonObject authenticate = await SendAuthenticateAsync(accessToken, cancellationToken);
        ThrowIfError(authenticate);
    }

    private Task<JsonObject> SendAuthenticateAsync(string accessToken, CancellationToken cancellationToken)
    {
        return RequestAsync("AUTHENTICATE", new JsonObject { ["access_token"] = accessToken }, cancellationToken);
    }

    private async Task ReadLoopAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            JsonObject message = await ReadMessageAsync(cancellationToken);
            string? command = (string?)message["cmd"];
            string? evt = (string?)message["evt"];
            JsonNode? data = message["data"];

            if (evt == "ERROR")
            {
                Console.WriteLine($"Discord error for {command}: {(string?)data?["message"]}");
                continue;
            }

            if (command == "GET_SELECTED_VOICE_CHANNEL" || command == "GET_CHANNEL")
            {
                await JoinChannelAsync(data as JsonObject, cancellationToken);
            }
            else if (command == "DISPATCH")
            {
                await HandleEventAsync(evt, data, cancellationToken);
            }
        }
    }

    private async Task HandleEventAsync(string? evt, JsonNode? data, CancellationToken cancellationToken)
    {
        switch (evt)
        {
            case "VOICE_CHANNEL_SELECT":
                string? newChannelId = (string?)data?["channel_id"];
                if (newChannelId == channelId)
                {
                    break;
                }

                await LeaveChannelAsync(cancellationToken);
                if (newChannelId != null)
                {
                    await SendCommandAsync("GET_CHANNEL", new JsonObject { ["channel_id"] = newChannelId }, null, cancellationToken);
                }
                break;

            case "VOICE_STATE_CREATE":
            case "VOICE_STATE_UPDATE":
                VoiceMember? member = ParseMember(data);
                if (member != null)
                {
                    MemberUpdated?.Invoke(member);
                }
                break;

            case "VOICE_STATE_DELETE":
                string? leftUserId = (string?)data?["user"]?["id"];
                if (leftUserId != null)
                {
                    MemberLeft?.Invoke(leftUserId);
                }
                break;

            case "SPEAKING_START":
            case "SPEAKING_STOP":
                string? speakingUserId = (string?)data?["user_id"];
                if (speakingUserId != null)
                {
                    SpeakingChanged?.Invoke(speakingUserId, evt == "SPEAKING_START");
                }
                break;
        }
    }

    private async Task JoinChannelAsync(JsonObject? channel, CancellationToken cancellationToken)
    {
        string? newChannelId = (string?)channel?["id"];
        if (channel == null || newChannelId == null)
        {
            await LeaveChannelAsync(cancellationToken);
            return;
        }

        if (newChannelId != channelId)
        {
            await LeaveChannelAsync(cancellationToken);
            channelId = newChannelId;

            foreach (string channelEvent in ChannelEvents)
            {
                await SendCommandAsync("SUBSCRIBE", ChannelArgs(newChannelId), channelEvent, cancellationToken);
            }
        }

        var members = new List<VoiceMember>();
        if (channel["voice_states"] is JsonArray voiceStates)
        {
            foreach (JsonNode? voiceState in voiceStates)
            {
                VoiceMember? member = ParseMember(voiceState);
                if (member != null)
                {
                    members.Add(member);
                }
            }
        }

        ChannelJoined?.Invoke(members);
    }

    private async Task LeaveChannelAsync(CancellationToken cancellationToken)
    {
        if (channelId == null)
        {
            return;
        }

        string oldChannelId = channelId;
        channelId = null;

        foreach (string channelEvent in ChannelEvents)
        {
            await SendCommandAsync("UNSUBSCRIBE", ChannelArgs(oldChannelId), channelEvent, cancellationToken);
        }

        ChannelLeft?.Invoke();
    }

    private static JsonObject ChannelArgs(string id) => new() { ["channel_id"] = id };

    private static VoiceMember? ParseMember(JsonNode? voiceState)
    {
        JsonNode? user = voiceState?["user"];
        string? id = (string?)user?["id"];
        if (id == null)
        {
            return null;
        }

        string name = (string?)voiceState?["nick"]
            ?? (string?)user?["global_name"]
            ?? (string?)user?["username"]
            ?? "Unknown";

        JsonNode? state = voiceState?["voice_state"];
        bool muted = (bool?)state?["mute"] == true || (bool?)state?["self_mute"] == true;
        bool deafened = (bool?)state?["deaf"] == true || (bool?)state?["self_deaf"] == true;

        return new VoiceMember(id, name, (string?)user?["avatar"], muted, deafened);
    }

    private async Task<JsonObject> RequestAsync(string command, JsonObject? args, CancellationToken cancellationToken)
    {
        string nonce = await SendCommandAsync(command, args, null, cancellationToken);

        while (true)
        {
            JsonObject response = await ReadMessageAsync(cancellationToken);
            if ((string?)response["nonce"] == nonce)
            {
                return response;
            }
        }
    }

    private async Task<string> SendCommandAsync(string command, JsonObject? args, string? evt, CancellationToken cancellationToken)
    {
        string nonce = Guid.NewGuid().ToString();

        var message = new JsonObject
        {
            ["cmd"] = command,
            ["args"] = args ?? new JsonObject(),
            ["nonce"] = nonce
        };

        if (evt != null)
        {
            message["evt"] = evt;
        }

        await SendAsync(OpFrame, message.ToJsonString(), cancellationToken);
        return nonce;
    }

    private async Task SendAsync(int opcode, string messageBody, CancellationToken cancellationToken)
    {
        NetworkStream currentStream = stream ?? throw new IOException("Not connected to Discord.");
        byte[] message = CreateMessage(opcode, messageBody);

        await writeLock.WaitAsync(cancellationToken);
        try
        {
            await currentStream.WriteAsync(message, cancellationToken);
        }
        finally
        {
            writeLock.Release();
        }
    }

    private async Task<JsonObject> ReadMessageAsync(CancellationToken cancellationToken)
    {
        NetworkStream currentStream = stream ?? throw new IOException("Not connected to Discord.");

        while (true)
        {
            //Takes in 8byte header message from discord
            byte[] header = new byte[8];
            await currentStream.ReadExactlyAsync(header, cancellationToken);

            int opcode = BitConverter.ToInt32(header, 0);

            // Get the last 4 bytes of the header which is the length of the body
            int length = BitConverter.ToInt32(header, 4);

            // Use the length to determine how long the message is and use it to read the body
            byte[] body = new byte[length];
            await currentStream.ReadExactlyAsync(body, cancellationToken);

            string json = Encoding.UTF8.GetString(body);

            if (opcode == OpPing)
            {
                await SendAsync(OpPong, json, cancellationToken);
                continue;
            }

            if (opcode == OpClose)
            {
                JsonNode? close = JsonNode.Parse(json);
                throw new DiscordException($"Discord closed the connection: {(string?)close?["message"]}");
            }

            if (JsonNode.Parse(json) is JsonObject message)
            {
                return message;
            }
        }
    }

    private static bool IsError(JsonObject response) => (string?)response["evt"] == "ERROR";

    private static void ThrowIfError(JsonObject response)
    {
        if (IsError(response))
        {
            string? message = (string?)response["data"]?["message"];
            throw new DiscordException($"Discord error: {message}");
        }
    }

    private static byte[] CreateMessage(int opcode, string messageBody)
    {
        byte[] opcodeByteArray = BitConverter.GetBytes(opcode);

        byte[] messageBodyByteArray = Encoding.UTF8.GetBytes(messageBody);

        byte[] lengthByteArray = BitConverter.GetBytes(messageBodyByteArray.Length);

        byte[] completeByteArray = opcodeByteArray.Concat(lengthByteArray).Concat(messageBodyByteArray).ToArray();

        return completeByteArray;
    }

    private void Disconnect()
    {
        stream?.Dispose();
        stream = null;
        socket = null;
    }

    public void Dispose()
    {
        Disconnect();
        GC.SuppressFinalize(this);
    }
}
