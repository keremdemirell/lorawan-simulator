// using Org.BouncyCastle.Crypto;
// using Org.BouncyCastle.Crypto.Macs;
// using Org.BouncyCastle.Crypto.Parameters;
using System;
using System.IO;
using System.Text.Json.Serialization;
using System.Timers;
using Newtonsoft.Json;
using Microsoft.Extensions.Configuration;
using System.Reflection.Metadata;
using Org.BouncyCastle.Crypto.Tls;
using System.Formats.Cbor;

public static class Program
{
    public static List<CborPayload> CborPayloads = new();
    public static int AggregationId = 1;

    static void Main(string[] args)
    {
        while (true)
        {
            byte[] phyPayload = new byte[] { };

            var config = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json")
                .Build();

            List<DeviceConfig> packetDevices = config.GetSection("DeviceSources:ByPacketSize").Get<List<DeviceConfig>>();
            List<TimedDeviceConfig> timedDevices = config.GetSection("DeviceSources:ByDuration").Get<List<TimedDeviceConfig>>();

            Console.WriteLine("Should the devices be entered by you or taken from appsettings.json? (A/C)");
            string isCustomDevice = Console.ReadLine();

            Console.WriteLine("By packet size or by timer? (P/T)");
            string isTimer = Console.ReadLine();

            if (isCustomDevice == "A")
            {
                if (isTimer == "P")
                {
                    arrangePHYPayload(packetDevices);
                }

                else
                {
                    arrangeTimedPHYPayload(timedDevices);
                }
            }

            else
            {

                Console.WriteLine("How many devices?");
                int deviceCount = int.Parse(Console.ReadLine());

                List<DeviceConfig> customDevices = new List<DeviceConfig>(deviceCount);
                List<TimedDeviceConfig> customTimedDevices = new List<TimedDeviceConfig>(deviceCount);

                for (int i = 0; i < deviceCount; i++)
                {
                    Console.WriteLine($"Enter {i + 1}. DevAddr (0x..)");
                    string customDevAddr = Console.ReadLine();

                    Console.WriteLine($"Enter {i + 1}. NwkSKey (0x..)");
                    string customNwkSKey = Console.ReadLine();

                    Console.WriteLine($"Enter {i + 1}. AppSKey (0x..)");
                    string customAppSKey = Console.ReadLine();

                    if (isTimer == "P")
                    {
                        Console.WriteLine($"Enter {i + 1}. Packet Size");
                        int customPacketSize = int.Parse(Console.ReadLine());

                        customDevices.Add(new DeviceConfig(customDevAddr, customNwkSKey, customAppSKey, customPacketSize));
                    }
                    else
                    {
                        Console.WriteLine($"Enter {i + 1}. Duration (seconds)");
                        int customDuration = int.Parse(Console.ReadLine());

                        Console.WriteLine($"Enter {i + 1}. Interval (m.seconds)");
                        int customIntervalSeconds = int.Parse(Console.ReadLine());

                        customTimedDevices.Add(new TimedDeviceConfig(customDevAddr, customNwkSKey, customAppSKey, customDuration, customIntervalSeconds));
                    }
                }

                if (isTimer == "P")
                {
                    arrangePHYPayload(customDevices);
                }
                else
                {
                    arrangeTimedPHYPayload(customTimedDevices);
                }

            }

            CborHeader cborHeader = EncapsulateCbroPayloadWithHeader(CborPayloads.Count);

            CborPacket cborPacket = new CborPacket()
            {
                Header = cborHeader,
                Payloads = CborPayloads
            };

            // string json = JsonConvert.SerializeObject(cborPacket, Formatting.Indented);
            // Console.WriteLine("Serialized CBOR Packet:");
            // Console.WriteLine(json);

            byte[] cborData = SerializeToCbor(cborPacket);
            Console.WriteLine(BitConverter.ToString(cborData).Replace("-", ""));
            File.WriteAllBytes("packet.cbor", cborData);

            ZeromqHelper.SendCbor(cborData);

            Console.WriteLine("Would you like to start over? (Y/N)");
            string restart = Console.ReadLine().Trim().ToUpper();
            if (restart == "N") break;

            CborPayloads = new();
        }
    }

    public static void arrangePHYPayload(List<DeviceConfig> devices)
    {
        foreach (DeviceConfig device in devices)
        {
            Console.WriteLine($"For device DevAddr: {device.DevAddr}, creating {device.PacketSize} packets:");

            for (int i = 0; i < device.PacketSize; i++)
            {
                byte[] phyPayload = generatePHYPayload(device.DevAddr, device.NwkSKey, device.AppSKey);
                EncapsulatePhyPayload(phyPayload);
            }
        }
    }

    public static void arrangeTimedPHYPayload(List<TimedDeviceConfig> devices)
    {
        List<Task> tasks = new();

        foreach (TimedDeviceConfig device in devices)
        {
            var tcs = new TaskCompletionSource();

            System.Timers.Timer timer = new System.Timers.Timer(device.IntervalSeconds);
            timer.Elapsed += (sender, e) =>
            {
                byte[] phyPayload = generateTimedPHYPayload(sender, e, device.DevAddr, device.NwkSKey, device.AppSKey);
                EncapsulatePhyPayload(phyPayload);
            };
            timer.AutoReset = true;
            timer.Enabled = true;

            Console.WriteLine($"Sending LoRaWAN packets every {device.IntervalSeconds} milliseconds for {device.DevAddr}.");

            Task.Delay(device.Duration * 1000).ContinueWith(_ =>
            {
                timer.Stop();
                timer.Dispose();
                Console.WriteLine($"Finished the {device.Duration} seconds sending for {device.DevAddr}");
                tcs.SetResult();
            });

            tasks.Add(tcs.Task);
        }

        Task.WaitAll(tasks.ToArray());
    }


    public static byte[] generatePHYPayload(string devAddr, string nwkSKey, string appSKey) // Object source, ElapsedEventArgs e
    {
        byte[] phyPayload = PayloadBuilder.BuildPhyPayload(devAddr, nwkSKey, appSKey);
        Console.WriteLine($"Generated PHYPayload :  for devaddr {devAddr}");
        Console.WriteLine(BitConverter.ToString(phyPayload));

        string hexPayload = BitConverter.ToString(phyPayload).Replace("-", "");
        Console.WriteLine(hexPayload);

        return phyPayload;
    }

    public static byte[] generateTimedPHYPayload(Object source, ElapsedEventArgs e, string devAddr, string nwkSKey, string appSKey) // Object source, ElapsedEventArgs e
    {
        byte[] phyPayload = PayloadBuilder.BuildPhyPayload(devAddr, nwkSKey, appSKey);
        Console.WriteLine($"Generated PHYPayload :  for devaddr {devAddr}");
        Console.WriteLine(BitConverter.ToString(phyPayload));

        string hexPayload = BitConverter.ToString(phyPayload).Replace("-", "");
        Console.WriteLine(hexPayload);

        return phyPayload;
    }

    public static CborPayload EncapsulatePhyPayload(byte[] phyPayload)
    {
        Random rnd = new Random();
        CborPayload cborPayload = new CborPayload()
        {
            RegionNumber = 5,
            Timestamp = new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds(),
            RSSI = rnd.Next(-200, -100),
            SNR = rnd.Next(5, 30),
            Frequency = 867000000,
            FrequencyDrift = rnd.Next(-400, -300),
            FrequencyInit = rnd.Next(2) == 0 ? 46 : 60,
            Payload = phyPayload
        };

        CborPayloads.Add(cborPayload);

        return cborPayload;
    }

    public static CborHeader EncapsulateCbroPayloadWithHeader(int payloadLen)
    {

        Random rn = new Random();
        int satelliteIdIndex = rn.Next(0, 7);

        var config = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json")
                .Build();

        List<int> satelliteIds = config.GetSection("SatelliteIds:ConnectaIoT").Get<List<int>>();
        int satelliteId = satelliteIds[satelliteIdIndex];

        CborHeader cborHeader = new CborHeader()
        {
            SatelliteId = satelliteId,
            AggregationId = AggregationId++,
            TimeStamp = new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds(),
            PayloadLength = payloadLen
        };

        return cborHeader;
    }

    public static byte[] SerializeToCbor(CborPacket packet)
    {
        var writer = new CborWriter();

        // Outer array ( [HeaderArray, [PayloadArray1, PayloadArray2, ...]] )
        writer.WriteStartArray(2);

        // header
        writer.WriteStartArray(4);
        writer.WriteInt32(packet.Header.SatelliteId);
        writer.WriteInt32(packet.Header.AggregationId);
        writer.WriteInt64(packet.Header.TimeStamp);
        writer.WriteInt32(packet.Header.PayloadLength);
        writer.WriteEndArray();

        // payloads
        writer.WriteStartArray(packet.Payloads.Count);
        foreach (var p in packet.Payloads)
        {
            writer.WriteStartArray(8);
            writer.WriteInt32(p.RegionNumber);
            writer.WriteInt64(p.Timestamp);
            writer.WriteInt32(p.RSSI);
            writer.WriteInt32(p.SNR);
            writer.WriteInt64(p.Frequency);
            writer.WriteInt32(p.FrequencyDrift);
            writer.WriteInt32(p.FrequencyInit);
            writer.WriteByteString(p.Payload);
            writer.WriteEndArray();
        }
        writer.WriteEndArray(); // end of payloads

        writer.WriteEndArray(); // end of outer array

        return writer.Encode();
    }



}