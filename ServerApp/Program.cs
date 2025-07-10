using NetMQ;
using NetMQ.Sockets;
using Microsoft.Extensions.Configuration;
class Program
{
    static void Main(string[] args)
    {

        var config = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json")
            .Build();

        string port = config.GetSection("NetMQ:Address:Port").Get<string>();
        string ip = config.GetSection("NetMQ:Address:Ip").Get<string>();

        string topic = config.GetSection("NetMQ:Topic").Get<string>();

        string fullAddress = $"{ip}:{port}";

        using (var server = new SubscriberSocket())
        {
            server.Bind(fullAddress);
            server.Subscribe(topic);

            Console.WriteLine($"📡 Server listening on port {port}...");

            while (true)
            {
                byte[] cborBytes = server.ReceiveFrameBytes();
                Console.WriteLine("Received {0} bytes from client.", cborBytes.Length);
                Console.WriteLine(BitConverter.ToString(cborBytes).Replace("-", ""));
            }
        }

    }
}
