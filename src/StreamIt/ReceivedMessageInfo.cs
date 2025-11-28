using System.Net.WebSockets;

namespace StreamIt;

public class ReceivedMessageInfo
{
    public int Length { get; set; }
    public WebSocketMessageType MessageType { get; set; }
}