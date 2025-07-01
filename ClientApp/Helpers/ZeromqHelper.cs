using Microsoft.VisualBasic;
using NetMQ;
using NetMQ.Sockets;

public static class ZeromqHelper
{
    public static void SendCbor(byte[] cborData)
    {
        using (var client = new PublisherSocket())
        {
            client.Connect("tcp://127.0.0.1:5556");
            Thread.Sleep(500); // <== Let ZMQ handshake settle

            client.SendMoreFrame("D2S").SendFrame(cborData);

        }
    }
}