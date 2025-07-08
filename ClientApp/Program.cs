using Microsoft.Extensions.Configuration;
using Serilog;

public static class Program
{
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
                InputHelper.GetIntInput("How should the devices be drawn?\n1 - From appsettings.json\n2 - Your custom input", 2) : 1;

            if (deviceInfoSource == 1)
            {
                PayloadBuilder.ArrangePHYPayload(packetDevices);
                PayloadBuilder.ArrangeTimedPHYPayload(timedDevices);
            }

            else
            {

                int packetGenerationOption = InputHelper.GetIntInput("Choose the method to generate packages:\n1 - Packet size\n2 - Timer", 2);

                int deviceCount = InputHelper.GetIntInput("How many devices would you like to input?");

                List<DeviceConfig> customDevices = new List<DeviceConfig>(deviceCount);
                List<TimedDeviceConfig> customTimedDevices = new List<TimedDeviceConfig>(deviceCount);

                for (int i = 0; i < deviceCount; i++)
                {

                    string customDevAddr = InputHelper.ValidateDeviceInformationInput($"Enter {i + 1}. DevAddr (0x..): ", DeviceInfoType.DevAddr);
                    string customNwkSKey = InputHelper.ValidateDeviceInformationInput($"Enter {i + 1}. NwkSKey (0x..): ", DeviceInfoType.NwkSKey);
                    string customAppSKey = InputHelper.ValidateDeviceInformationInput($"Enter {i + 1}. AppSKey (0x..): ", DeviceInfoType.AppSKey);

                    if (packetGenerationOption == 1)
                    {
                        int customPacketSize = InputHelper.GetIntInput($"Enter {i + 1}. Packet Size: ");

                        customDevices.Add(new DeviceConfig(customDevAddr, customNwkSKey, customAppSKey, customPacketSize));
                    }
                    else
                    {
                        int customDuration = InputHelper.GetIntInput($"Enter {i + 1}. Duration (seconds): ");
                        int customIntervalSeconds = InputHelper.GetIntInput($"Enter {i + 1}. Interval (milliseconds): ");

                        customTimedDevices.Add(new TimedDeviceConfig(customDevAddr, customNwkSKey, customAppSKey, customDuration, customIntervalSeconds));
                    }
                }

                if (packetGenerationOption == 1)
                {
                    PayloadBuilder.ArrangePHYPayload(customDevices);
                }
                else
                {
                    PayloadBuilder.ArrangeTimedPHYPayload(customTimedDevices);
                }

            }

            CborHelper.TriggerCborPacketCreation();

            Log.Information("Session finished");

            int restart = isInteractive == true ? InputHelper.GetIntInput("Would you like to start over?\n1 - Yes\n2 - No", 2) : 2;
            if (restart == 2) break;

        }

        Log.Information("Program Closed");
        Log.CloseAndFlush();
    }
}