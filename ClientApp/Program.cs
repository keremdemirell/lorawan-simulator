// using Org.BouncyCastle.Crypto;
// using Org.BouncyCastle.Crypto.Macs;
// using Org.BouncyCastle.Crypto.Parameters;
using System.Timers;
using Microsoft.Extensions.Configuration;
using System.Formats.Cbor;
using Serilog;
using Microsoft.VisualBasic;

public static class Program
{
    public static List<CborPayload> CborPayloads = new();
    public static int AggregationId = 1;
    public static Random rnd = new Random();
    static void Main(string[] args)
    {

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.File("logs/log.txt", rollingInterval: RollingInterval.Day)
            .CreateLogger();

        Log.Information("LoRaWAN Simulator Started");

        while (true)
        {
            byte[] phyPayload = new byte[] { };

            var config = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json")
                .Build();

            List<DeviceConfig> packetDevices = config.GetSection("DeviceSources:ByPacketSize").Get<List<DeviceConfig>>();
            List<TimedDeviceConfig> timedDevices = config.GetSection("DeviceSources:ByDuration").Get<List<TimedDeviceConfig>>();

            // Console.WriteLine("Should the devices be entered by you or taken from appsettings.json? (A/C)");
            // string isCustomDevice = Console.ReadLine();
            int deviceInfoSource = GetIntInput("How should the devices be drawn?\n1 - From appsettings.json\n2 - Your custom input", 2);

            // Console.WriteLine("By packet size or by timer? (P/T)");
            // string isTimer = Console.ReadLine();
            int packetGenerationOption = GetIntInput("Choose the method to draw devices:\n1 - Packet size\n2 - Timer", 2);

            if (deviceInfoSource == 1)
            {
                if (packetGenerationOption == 1)
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

                // Console.WriteLine("How many devices?");
                // int deviceCount = int.Parse(Console.ReadLine());
                int deviceCount = GetIntInput("How many devices would you like to input?", 0);

                List<DeviceConfig> customDevices = new List<DeviceConfig>(deviceCount);
                List<TimedDeviceConfig> customTimedDevices = new List<TimedDeviceConfig>(deviceCount);

                for (int i = 0; i < deviceCount; i++)
                {
                    // Console.WriteLine($"Enter {i + 1}. DevAddr (0x..)");
                    // string customDevAddr = Console.ReadLine();

                    // Console.WriteLine($"Enter {i + 1}. NwkSKey (0x..)");
                    // string customNwkSKey = Console.ReadLine();

                    // Console.WriteLine($"Enter {i + 1}. AppSKey (0x..)");
                    // string customAppSKey = Console.ReadLine();

                    // string customDevAddr = GetStringInput($"Enter {i + 1}. DevAddr (0x..): ");
                    // string customNwkSKey = GetStringInput($"Enter {i + 1}. NwkSKey (0x..): ");
                    // string customAppSKey = GetStringInput($"Enter {i + 1}. AppSKey (0x..): ");

                    string customDevAddr = ValidateDeviceInformationInput($"Enter {i + 1}. DevAddr (0x..): ", "devAddr");
                    string customNwkSKey = ValidateDeviceInformationInput($"Enter {i + 1}. NwkSKey (0x..): ", "nwkSKey");
                    string customAppSKey = ValidateDeviceInformationInput($"Enter {i + 1}. AppSKey (0x..): ", "appSKey");

                    if (packetGenerationOption == 1)
                    {
                        // Console.WriteLine($"Enter {i + 1}. Packet Size");
                        // int customPacketSize = int.Parse(Console.ReadLine());
                        int customPacketSize = GetIntInput($"Enter {i + 1}. Packet Size: ", 0);

                        customDevices.Add(new DeviceConfig(customDevAddr, customNwkSKey, customAppSKey, customPacketSize));
                    }
                    else
                    {
                        // Console.WriteLine($"Enter {i + 1}. Duration (seconds)");
                        // int customDuration = int.Parse(Console.ReadLine());

                        // Console.WriteLine($"Enter {i + 1}. Interval (m.seconds)");
                        // int customIntervalSeconds = int.Parse(Console.ReadLine());
                        int customDuration = GetIntInput($"Enter {i + 1}. Duration (seconds): ", 0);
                        int customIntervalSeconds = GetIntInput($"Enter {i + 1}. Interval (milliseconds): ", 0);

                        customTimedDevices.Add(new TimedDeviceConfig(customDevAddr, customNwkSKey, customAppSKey, customDuration, customIntervalSeconds));
                    }
                }

                if (packetGenerationOption == 1)
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
            // Console.WriteLine(BitConverter.ToString(cborData).Replace("-", ""));
            Log.Debug("Created Cbor: {Cbor}", BitConverter.ToString(cborData).Replace("-", ""));
            File.WriteAllBytes("packet.cbor", cborData);

            ZeromqHelper.SendCbor(cborData);

            // Console.WriteLine("Would you like to start over? (Y/N)");
            // string restart = Console.ReadLine().Trim().ToUpper();

            Log.Information("Session finished");

            int restart = GetIntInput("Would you like to start over?\n1 - Yes\n2 - No", 2);
            if (restart == 2) break;

            CborPayloads = new();
        }

        Log.Information("Program Closed");
        Log.CloseAndFlush();
    }

    public static void arrangePHYPayload(List<DeviceConfig> devices)
    {
        foreach (DeviceConfig device in devices)
        {
            // Console.WriteLine($"For device DevAddr: {device.DevAddr}, creating {device.PacketSize} packets:");
            Log.Debug("For device DevAddr: {device.DevAddr}, creating {device.PacketSize} packets:", device.DevAddr, device.PacketSize);

            for (int i = 0; i < device.PacketSize; i++)
            {
                byte[] phyPayload = generatePHYPayload(device.DevAddr, device.NwkSKey, device.AppSKey);
                Log.Information("PHYPayload generated for DevAddr {DevAddr} (Packet {generatedPacket}/{packetSize})", device.DevAddr, i + 1, device.PacketSize);
                EncapsulatePhyPayload(phyPayload);
            }
        }
    }

    public static void arrangeTimedPHYPayload(List<TimedDeviceConfig> devices)
    {
        List<Task> tasks = new();

        foreach (TimedDeviceConfig device in devices)
        {
            int i = 1;

            var tcs = new TaskCompletionSource();

            System.Timers.Timer timer = new System.Timers.Timer(device.IntervalSeconds);
            timer.Elapsed += (sender, e) =>
            {
                byte[] phyPayload = generateTimedPHYPayload(sender, e, device.DevAddr, device.NwkSKey, device.AppSKey);
                Log.Information("PHYPayload generated for DevAddr {DevAddr} ({generatedPacket} seconds of {totalTime})", device.DevAddr, device.IntervalSeconds / 1000 * i++, device.Duration);
                EncapsulatePhyPayload(phyPayload);
            };
            timer.AutoReset = true;
            timer.Enabled = true;

            // Console.WriteLine($"Sending LoRaWAN packets every {device.IntervalSeconds} milliseconds for {device.DevAddr}.");
            Log.Debug("Sending LoRaWAN packets every {device.IntervalSeconds} milliseconds for {device.DevAddr}.", device.IntervalSeconds, device.DevAddr);

            Task.Delay(device.Duration * 1000).ContinueWith(_ =>
            {
                timer.Stop();
                timer.Dispose();
                // Console.WriteLine($"Finished the {device.Duration} seconds sending for {device.DevAddr}");
                Log.Debug("Finished the {device.Duration} seconds sending for {device.DevAddr}", device.Duration, device.DevAddr);
                tcs.SetResult();
            });

            tasks.Add(tcs.Task);
        }

        Task.WaitAll(tasks.ToArray());
    }


    public static byte[] generatePHYPayload(string devAddr, string nwkSKey, string appSKey) // Object source, ElapsedEventArgs e
    {
        byte[] phyPayload = PayloadBuilder.BuildPhyPayload(devAddr, nwkSKey, appSKey);
        // Console.WriteLine($"Generated PHYPayload :  for devaddr {devAddr}");
        Log.Debug("Generated PHYPayload for devaddr {devaddr}: {hexPayload}", devAddr, BitConverter.ToString(phyPayload).Replace("-", ""));
        // Console.WriteLine(BitConverter.ToString(phyPayload));

        // string hexPayload = BitConverter.ToString(phyPayload).Replace("-", "");
        // Console.WriteLine(hexPayload);

        return phyPayload;
    }

    public static byte[] generateTimedPHYPayload(Object source, ElapsedEventArgs e, string devAddr, string nwkSKey, string appSKey) // Object source, ElapsedEventArgs e
    {
        byte[] phyPayload = PayloadBuilder.BuildPhyPayload(devAddr, nwkSKey, appSKey);
        // Console.WriteLine($"Generated PHYPayload :  for devaddr {devAddr}");
        Log.Debug("Generated PHYPayload for devaddr {devaddr}: {hexPayload}", devAddr, BitConverter.ToString(phyPayload).Replace("-", ""));
        // Console.WriteLine(BitConverter.ToString(phyPayload));

        // string hexPayload = BitConverter.ToString(phyPayload).Replace("-", "");
        // Console.WriteLine(hexPayload);

        return phyPayload;
    }

    public static CborPayload EncapsulatePhyPayload(byte[] phyPayload)
    {
        // Random rnd = new Random();
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
            Payload = phyPayload
        };

        CborPayloads.Add(cborPayload);

        Log.Information("PHYPayload encapsulated and added to CBOR Payloads at timestamp {timestamp}", timestamp);

        return cborPayload;
    }

    public static CborHeader EncapsulateCbroPayloadWithHeader(int payloadLen)
    {

        // Random rn = new Random();
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

    public static int GetIntInput(string prompt, int maxChoice)
    {
        while (true)
        {
            Console.WriteLine(prompt);
            // int input = int.Parse(Console.ReadLine().Trim());

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
                        Console.WriteLine($"Invalid input. Please enter a number between 1 and {maxChoice}.");
                    }
                }
                else
                {
                    return input;
                }
            }
            catch (FormatException)
            {
                Console.WriteLine("Invalid input. Please enter a valid number.");
            }
        }
    }

    public static string GetStringInput(string prompt)
    {
        while (true)
        {
            Console.WriteLine(prompt);
            string input = Console.ReadLine().Trim();

            if (string.IsNullOrEmpty(input))
            {
                Console.WriteLine("Input cannot be empty. Please try again.");
            }
            else
            {
                return input;
            }
        }
    }

    // public static string ValidateDevAddrInput(string prompt)
    // {
    //     while (true)
    //     {
    //         Console.WriteLine(prompt);
    //         string input = Console.ReadLine().Trim();

    //         if (input.StartsWith("0x") && input.Length == 10 && IsHex(input.Substring(2)))
    //         {
    //             return input;
    //         }
    //         else
    //         {
    //             Console.WriteLine("Invalid DevAddr. Please enter a value with 8 hex characters after '0x'.");
    //         }
    //     }
    // }

    // public static string ValidateSKeyInput(string prompt)
    // {
    //     while (true)
    //     {
    //         Console.WriteLine(prompt);
    //         string input = Console.ReadLine().Trim();

    //         if (input.StartsWith("0x") && input.Length == 34 && IsHex(input.Substring(2)))
    //         {
    //             return input;
    //         }
    //         else
    //         {
    //             Console.WriteLine("Invalid Session Key. Please enter a value with 32 hex characters after '0x'.");
    //         }
    //     }
    // }

    public static string ValidateDeviceInformationInput(string prompt, string deviceInfo)
    {
        while (true)
        {
            Console.WriteLine(prompt);
            string input = Console.ReadLine().Trim();

            int inputLength = deviceInfo.Trim().ToUpper() == "DEVADDR" ? 10 : 34;

            if (input.StartsWith("0x") && input.Length == inputLength && IsHex(input.Substring(2)))
            {
                return input;
            }
            else
            {
                Console.WriteLine($"Invalid {deviceInfo.ToLower()}. Please enter a value with {inputLength} hex characters including '0x'.");
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