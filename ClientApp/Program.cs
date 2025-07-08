using System.Timers;
using Microsoft.Extensions.Configuration;
using System.Formats.Cbor;
using Serilog;

public static class Program
{
    public static List<CborPayload> CborPayloads = new();
    public static int AggregationId = 1;
    public static Random rnd = new Random();

    static void Main(string[] args)
    {

        var config = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json")
                .Build();

        Log.Logger = new LoggerConfiguration()
                .ReadFrom.Configuration(config)
                .CreateLogger();

        Log.Information("LoRaWAN Simulator Started");

        while (true)
        {
            byte[] phyPayload = new byte[] { };

            List<DeviceConfig> packetDevices = config.GetSection("DeviceSources:ByPacketSize").Get<List<DeviceConfig>>();
            List<TimedDeviceConfig> timedDevices = config.GetSection("DeviceSources:ByDuration").Get<List<TimedDeviceConfig>>();
            var isInteractive = config.GetSection("IsInteractive").Get<bool>();

            var deviceInfoSource =
                isInteractive == true ?
                GetIntInput("How should the devices be drawn?\n1 - From appsettings.json\n2 - Your custom input", 2) : 1;
            
            if (deviceInfoSource == 1)
            {
                ArrangePHYPayload(packetDevices);                
                ArrangeTimedPHYPayload(timedDevices);
            }

            else
            {

                int packetGenerationOption = GetIntInput("Choose the method to generate packages:\n1 - Packet size\n2 - Timer", 2);

                int deviceCount = GetIntInput("How many devices would you like to input?");

                List<DeviceConfig> customDevices = new List<DeviceConfig>(deviceCount);
                List<TimedDeviceConfig> customTimedDevices = new List<TimedDeviceConfig>(deviceCount);

                for (int i = 0; i < deviceCount; i++)
                {

                    string customDevAddr = ValidateDeviceInformationInput($"Enter {i + 1}. DevAddr (0x..): ", DeviceInfoType.DevAddr);
                    string customNwkSKey = ValidateDeviceInformationInput($"Enter {i + 1}. NwkSKey (0x..): ", DeviceInfoType.NwkSKey);
                    string customAppSKey = ValidateDeviceInformationInput($"Enter {i + 1}. AppSKey (0x..): ", DeviceInfoType.AppSKey);

                    if (packetGenerationOption == 1)
                    {
                        int customPacketSize = GetIntInput($"Enter {i + 1}. Packet Size: ");

                        customDevices.Add(new DeviceConfig(customDevAddr, customNwkSKey, customAppSKey, customPacketSize));
                    }
                    else
                    {
                        int customDuration = GetIntInput($"Enter {i + 1}. Duration (seconds): ");
                        int customIntervalSeconds = GetIntInput($"Enter {i + 1}. Interval (milliseconds): ");

                        customTimedDevices.Add(new TimedDeviceConfig(customDevAddr, customNwkSKey, customAppSKey, customDuration, customIntervalSeconds));
                    }
                }

                if (packetGenerationOption == 1)
                {
                    ArrangePHYPayload(customDevices);
                }
                else
                {
                    ArrangeTimedPHYPayload(customTimedDevices);
                }

            }

            CborHeader cborHeader = EncapsulateCbroPayloadWithHeader(CborPayloads.Count);

            CborPacket cborPacket = new CborPacket()
            {
                Header = cborHeader,
                Payloads = CborPayloads
            };

            byte[] cborData = SerializeToCbor(cborPacket);
            Log.Debug("Created Cbor: {Cbor}", BitConverter.ToString(cborData).Replace("-", ""));
            File.WriteAllBytes("packet.cbor", cborData);

            ZeromqHelper.SendCbor(cborData);

            Log.Information("Session finished");

            int restart = isInteractive == true ? GetIntInput("Would you like to start over?\n1 - Yes\n2 - No", 2) : 2;
            if (restart == 2) break;

            CborPayloads = new();
        }

        Log.Information("Program Closed");
        Log.CloseAndFlush();
    }

    public static void ArrangePHYPayload(List<DeviceConfig> devices)
    {
        foreach (DeviceConfig device in devices)
        {
            Log.Debug("For device DevAddr: {device.DevAddr}, creating {device.PacketSize} packets:", device.DevAddr, device.PacketSize);

            for (int i = 0; i < device.PacketSize; i++)
            {
                byte[] phyPayload = GeneratePHYPayload(device.DevAddr, device.NwkSKey, device.AppSKey);
                Log.Information("PHYPayload generated for DevAddr {DevAddr} (Packet {generatedPacket}/{packetSize})", device.DevAddr, i + 1, device.PacketSize);
                EncapsulatePhyPayload(phyPayload);
            }
        }
    }

    public static void ArrangeTimedPHYPayload(List<TimedDeviceConfig> devices)
    {
        List<Task> tasks = new();

        foreach (TimedDeviceConfig device in devices)
        {
            int i = 1;

            var tcs = new TaskCompletionSource();

            System.Timers.Timer timer = new System.Timers.Timer(device.IntervalSeconds);
            timer.Elapsed += (sender, e) =>
            {
                byte[] phyPayload = GenerateTimedPHYPayload(sender, e, device.DevAddr, device.NwkSKey, device.AppSKey);
                Log.Information("PHYPayload generated for DevAddr {DevAddr} ({generatedPacket} seconds of {totalTime})", device.DevAddr, device.IntervalSeconds / 1000 * i++, device.Duration);
                EncapsulatePhyPayload(phyPayload);
            };
            timer.AutoReset = true;
            timer.Enabled = true;

            Log.Debug("Sending LoRaWAN packets every {device.IntervalSeconds} milliseconds for {device.DevAddr}.", device.IntervalSeconds, device.DevAddr);

            Task.Delay(device.Duration * 1000).ContinueWith(_ =>
            {
                timer.Stop();
                timer.Dispose();
                Log.Debug("Finished the {device.Duration} seconds sending for {device.DevAddr}", device.Duration, device.DevAddr);
                tcs.SetResult();
            });

            tasks.Add(tcs.Task);
        }

        Task.WaitAll(tasks.ToArray());
    }

    public static byte[] GeneratePHYPayload(string devAddr, string nwkSKey, string appSKey) // Object source, ElapsedEventArgs e
    {
        byte[] phyPayload = PayloadBuilder.BuildPhyPayload(devAddr, nwkSKey, appSKey);
        Log.Debug("Generated PHYPayload for devaddr {devaddr}: {hexPayload}", devAddr, BitConverter.ToString(phyPayload).Replace("-", ""));

        return phyPayload;
    }

    public static byte[] GenerateTimedPHYPayload(Object source, ElapsedEventArgs e, string devAddr, string nwkSKey, string appSKey) // Object source, ElapsedEventArgs e
    {
        byte[] phyPayload = PayloadBuilder.BuildPhyPayload(devAddr, nwkSKey, appSKey);
        Log.Debug("Generated PHYPayload for devaddr {devaddr}: {hexPayload}", devAddr, BitConverter.ToString(phyPayload).Replace("-", ""));

        return phyPayload;
    }

    public static CborPayload EncapsulatePhyPayload(byte[] phyPayload)
    {
        long timestamp = new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds();

        CborPayload cborPayload = new CborPayload()
        {
            RegionNumber = 5,
            Timestamp = timestamp,
            RSSI = rnd.Next(-200, -100),
            SNR = rnd.Next(5, 30),
            Frequency = 867000000,
            FrequencyDrift = rnd.Next(-400, -300),
            FrequencyInit = rnd.Next(2) == 0 ? 46 : 60,
            PayloadLength = phyPayload.Length,
            Payload = phyPayload
        };

        CborPayloads.Add(cborPayload);

        Log.Information("PHYPayload encapsulated and added to CBOR Payloads at timestamp {timestamp}", timestamp);

        return cborPayload;
    }

    public static CborHeader EncapsulateCbroPayloadWithHeader(int payloadLen)
    {

        int satelliteIdIndex = rnd.Next(0, 7);

        var config = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json")
                .Build();

        List<int> satelliteIds = config.GetSection("SatelliteIds:ConnectaIoT").Get<List<int>>();
        int satelliteId = satelliteIds[satelliteIdIndex];
        long timestamp = new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds();

        CborHeader cborHeader = new CborHeader()
        {
            SatelliteId = satelliteId,
            AggregationId = AggregationId++,
            TimeStamp = timestamp,
            PayloadLength = payloadLen
        };

        Log.Information("CBOR Payloads encapsulated with CBOR Header at timestamp {timestamp}", timestamp);

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
            writer.WriteStartArray(9);
            writer.WriteInt32(p.RegionNumber);
            writer.WriteInt64(p.Timestamp);
            writer.WriteInt32(p.RSSI);
            writer.WriteInt32(p.SNR);
            writer.WriteInt64(p.Frequency);
            writer.WriteInt32(p.FrequencyDrift);
            writer.WriteInt32(p.FrequencyInit);
            writer.WriteInt32(p.PayloadLength);
            writer.WriteByteString(p.Payload);
            writer.WriteEndArray();
        }
        writer.WriteEndArray(); // end of payloads

        writer.WriteEndArray(); // end of outer array

        return writer.Encode();
    }

    public static int GetIntInput(string prompt)
    {
        return GetIntInput(prompt, 0);
    }

    public static int GetIntInput(string prompt, int maxChoice)
    {
        while (true)
        {
            Console.WriteLine(prompt);

            try
            {
                int input = int.Parse(Console.ReadLine().Trim());

                if (maxChoice != 0)
                {
                    if (input >= 1 && input <= maxChoice)
                    {
                        return input;
                    }
                    else
                    {
                        Log.Warning($"Invalid input. Please enter a number between 1 and {maxChoice}.");
                    }
                }
                else
                {
                    return input;
                }
            }
            catch (FormatException ex)
            {
                Log.Error(ex, "Invalid input. Please enter a valid number.");
            }
        }
    }

    public static string ValidateDeviceInformationInput(string prompt, DeviceInfoType deviceInfo)
    {
        while (true)
        {
            Console.WriteLine(prompt);
            string input = Console.ReadLine().Trim();

            // int inputLength = deviceInfo.Trim().ToUpper() == "DEVADDR" ? 10 : 34;
            int inputLength = deviceInfo == DeviceInfoType.DevAddr ? 10 : 34;

            if (input.StartsWith("0x") && input.Length == inputLength && IsHex(input.Substring(2)))
            {
                return input;
            }
            else
            {
                Log.Warning($"Invalid {deviceInfo.ToString()}. Please enter a value with {inputLength} hex characters including '0x'.");
            }

        }
    }

    public static bool IsHex(string input)
    {
        foreach (char c in input)
        {
            if (!((c >= '0' && c <= '9') || (c >= 'A' && c <= 'F') || (c >= 'a' && c <= 'f')))
            {
                return false;
            }
        }
        return true;
    }

}