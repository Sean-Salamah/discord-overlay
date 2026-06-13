using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Net.Sockets;
using System.Reflection.Emit;
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

     // All messages to discord need to be sent as bytes, the format is:
     // [opcode: 4 bytes][length: 4 bytes][JSON as bytes]
     // opcode is the type of message. 0 being a handshake to establish connection.
     // After that everthing else atleast in this project is 1 which is for normal messages

    // Establishes connection with discord
     public void SendHandshake()
    {
        // This establishes the connection between the overlay and discord using opcode 0 and the client ID
        byte[] fullByteArray = CreateMessage(0,"{\"v\":1,\"client_id\":\"1511039920662118551\"}");
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

    public void Authorize()
    {
        
        socket.Send(CreateMessage(1, "{\"cmd\":\"AUTHORIZE\",\"args\":{\"client_id\":\"1511039920662118551\",\"scopes\":[\"rpc\",\"rpc.voice.read\"]},\"nonce\":\"" + Guid.NewGuid().ToString() + "\"}"));
    }

    public byte[] CreateMessage(int opcode,String messageBody)
    {
        byte[] opcodeByteArray = BitConverter.GetBytes(opcode);

        byte[] messageBodyByteArray = Encoding.UTF8.GetBytes(messageBody);

        byte[] lengthByteArray = BitConverter.GetBytes(messageBodyByteArray.Length);

        byte[] completeByteArray = opcodeByteArray.Concat(lengthByteArray).Concat(messageBodyByteArray).ToArray();

        return completeByteArray;
    }
}