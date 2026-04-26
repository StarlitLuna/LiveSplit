using WebSocketSharp.Server;

namespace LiveSplit.Server;

internal class WsConnection : WebSocketBehavior, IConnection
{
    internal MessageEventHandler EventHandler { get; set; }

    protected override void OnMessage(WebSocketSharp.MessageEventArgs e)
    {
        EventHandler?.Invoke(this, new MessageEventArgs(this, e.Data));
    }

    public void SendMessage(string message)
    {
        Send(message);
    }
}
