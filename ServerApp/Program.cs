using NetMQ;
using NetMQ.Sockets;

class Program
{
    static void Main(string[] args)
    {
        using (var server = new SubscriberSocket())
        {
            server.Bind("tcp://*:5556");
            server.Subscribe("");

            Console.WriteLine("📡 Server listening on port 5556...");

            while (true)
            {
                var topic = server.ReceiveFrameString();

                byte[] cborBytes = server.ReceiveFrameBytes();
                Console.WriteLine("Received {0} bytes from client.", cborBytes.Length);
                Console.WriteLine(BitConverter.ToString(cborBytes).Replace("-", ""));
            }
        }

    }
}
