using Microsoft.VisualBasic;
using NetMQ;
using NetMQ.Sockets;
using Microsoft.Extensions.Configuration;
using Serilog;

public static class ZeromqHelper
{
    public static void SendCbor(byte[] cborData)
    {
        var config = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json")
            .Build();

        // Log.Logger = new LoggerConfiguration()
        //     .WriteTo.File("logs/try.txt", rollingInterval: RollingInterval.Day)
        //     .CreateLogger();

        string ip = config.GetSection("NetMQ:Address:Ip").Get<string>();
        string port = config.GetSection("NetMQ:Address:Port").Get<string>();

        string topic = config.GetSection("NetMQ:Topic").Get<string>();

        string fullAddress = $"{ip}:{port}";

        Log.Information("Sending Cbor packet to {fullAddress} with topic {topic}", fullAddress, topic);

        using (var client = new PublisherSocket())
        {
            client.Connect(fullAddress);
            Thread.Sleep(500);

            client.SendMoreFrame(topic).SendFrame(cborData);

        }
    }
}