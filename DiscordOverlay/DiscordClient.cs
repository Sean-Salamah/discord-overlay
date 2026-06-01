using System;
using System.Linq;
using System.Net.Sockets;
using System.Text;
namespace DiscordOverlay;

    
public class DiscordClient{
    private Socket socket;
    public void Connect()
     {
        string socketPath = "/run/user/1000/discord-ipc-0";
        var endPoint = new UnixDomainSocketEndPoint(socketPath);
        socket = new Socket (AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        socket.Connect(endPoint);
        
     }

    // Establishes connection with discord
     public void SendHandshake()
    {
        // This tells discord this message is a handshake
        var opcode = 0;
        byte[] opcodeByteArray = BitConverter.GetBytes(opcode);

        // The body of the json that we want to send to discord
        var body = "{\"v\":1,\"client_id\":\"1511039920662118551\"}";

        byte[] bodyByteArray = Encoding.UTF8.GetBytes(body);

        byte[] lengthByteArray = BitConverter.GetBytes(bodyByteArray.Length);

        byte[] fullByteArray = opcodeByteArray.Concat(lengthByteArray).Concat(bodyByteArray).ToArray();
        
        socket.Send(fullByteArray);
    }

    public void ReadMessage()
    {
        //Takes in 8byte header message from discord
        byte[] header = new byte[8];
        socket.Receive(header);

        // Get the last 4 bits of the message from discord which is the length of the body
        int length = BitConverter.ToInt32(header, 4);

        // Use the length to determine how long the message is and use it to read the body
        byte[] body = new byte[length];
        socket.Receive(body);

        string json= Encoding.UTF8.GetString(body);
        Console.WriteLine(json);
    }
}