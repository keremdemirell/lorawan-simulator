using System.Text;
using Serilog;
using Microsoft.Extensions.Configuration;
using System.Timers;

public static class PayloadBuilder
{

    // **PHYPayload**
    // **├── MHDR (MAC Header)**
    // **├── MACPayload**
    // **│   ├── FHDR (Frame Header)**
    // **│   │   ├── DevAddr (4 bytes)**
    // **│   │   ├── FCtrl (1 byte – flags like ADR, ACK, etc.)**
    // **│   │   ├── FCnt (2 bytes – frame counter)**
    // **│   │   ├── FOpts (0–15 bytes – MAC commands)**
    // **│   ├── FPort (1 byte – says what kind of data is in FRMPayload)**
    // **│   └── FRMPayload (actual data or MAC commands)**
    // **└── MIC (Message Integrity Code – 4 bytes)**

    private static ushort us_fcnt = 0;

    private static readonly IConfiguration Config = new ConfigurationBuilder()
        .SetBasePath(Directory.GetCurrentDirectory())
        .AddJsonFile("appsettings.json")
        .Build();
    private static readonly int TriggerChance = Config.GetValue<int>("CBORTriggerChance");

    public static byte[] BuildPhyPayload(string devAddrString, string nwkSKeyString, string appSKeyString)
    {

        // MHDR
        byte mhdr = 0x40; // 0100 000 (010 000 00) -> 010 (MType) means unconfirmed uplink, 000 (rfu) is kind of the protocol here, 00 (major) is LoRaWAN version which is 1.0 (i couldnt find the major for 2.1.0.4?)

        // MACPayload

        // // FHDR

        // // // DevAddr
        // byte[] devAddr = new byte[] { 0xDA, 0X1B, 0X01, 0X26 }; //0x26011BDA from TTN which is kind of a global lorawan environment (range is 0x2600000-0x27FFFFFF and 0x26011BDA is a known address for no reason)
        byte[] devAddr = convertToBytes(devAddrString);
        Array.Reverse(devAddr); // little endien

        // // // FCtrl
        byte fctrl = 0x00; // 0000 0000 (0 0 0 0 0000) -> no adr, no adrackreq, no ack, no class b, no fopts since no mac command

        // // // FCnt
        us_fcnt++;
        byte[] fcnt = BitConverter.GetBytes(us_fcnt);

        // // // FOpts
        // -

        // // FPort
        byte fport = 0x01; // since app data is stored in frmpayload

        // // FRMPayload
        Random rnd = new Random();
        int randomFactor = rnd.Next(0, 3);
        string[] factors = new string[] { "TEMP", "HUMD", "PRESR", "PRECP" };
        int randomValue = rnd.Next(0, 50);
        string appData = factors[randomFactor] + "=" + randomValue;
        byte[] frmpayload = Encoding.ASCII.GetBytes(appData);

        //MIC

        List<byte> phyPayload = new();

        phyPayload.Add(mhdr);

        List<byte> macPayload = new();

        macPayload.AddRange(devAddr);
        macPayload.Add(fctrl);
        macPayload.AddRange(fcnt);
        macPayload.Add(fport);

        // frmpayloadEncryption
        byte[] appSKey = convertToBytes(appSKeyString);

        byte[] Si = EncrypytHelper.CreateKeystreamBlock(appSKey, 0x00, devAddr, us_fcnt, 1);

        byte[] encryptedPayload = EncrypytHelper.EncryptPayload(frmpayload, Si);
        macPayload.AddRange(encryptedPayload);

        phyPayload.AddRange(macPayload);

        // calculating MIC

        byte[] nwkSKey = convertToBytes(nwkSKeyString);

        byte[] micInput = new byte[1 + macPayload.Count];
        micInput[0] = mhdr;
        macPayload.CopyTo(micInput, 1);

        byte[] mic = EncrypytHelper.CalculateMIC(fport != 0x00 ? nwkSKey : appSKey, devAddr, us_fcnt, micInput, 0x00);
        phyPayload.AddRange(mic);

        return phyPayload.ToArray();
    }

    public static byte[] convertToBytes(string hex)
    {

        hex = hex.Substring(2);

        byte[] bytes = new byte[hex.Length / 2];
        for (int i = 0; i < hex.Length; i += 2)
        {
            bytes[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
        }

        return bytes;
    }


    public static void ArrangePHYPayload(List<DeviceConfig> devices)
    {
        Random rnd = new Random();

        foreach (DeviceConfig device in devices)
        {
            Log.Debug("For device DevAddr: {device.DevAddr}, creating {device.PacketSize} packets:", device.DevAddr, device.PacketSize);

            for (int i = 0; i < device.PacketSize; i++)
            {
                byte[] phyPayload = GeneratePHYPayload(device.DevAddr, device.NwkSKey, device.AppSKey);
                Log.Information("PHYPayload generated for DevAddr {DevAddr} (Packet {generatedPacket}/{packetSize})", device.DevAddr, i + 1, device.PacketSize);
                CborHelper.EncapsulatePhyPayload(phyPayload);
                if (rnd.Next(0, 100) < TriggerChance)
                {
                    CborHelper.TriggerCborPacketCreation();
                }
            }
        }
    }

    public static void ArrangeTimedPHYPayload(List<TimedDeviceConfig> devices)
    {
        List<Task> tasks = new();
        Random rnd = new Random();

        foreach (TimedDeviceConfig device in devices)
        {
            int i = 1;

            var tcs = new TaskCompletionSource();

            System.Timers.Timer timer = new System.Timers.Timer(device.IntervalSeconds);
            timer.Elapsed += (sender, e) =>
            {
                byte[] phyPayload = GenerateTimedPHYPayload(sender, e, device.DevAddr, device.NwkSKey, device.AppSKey);
                Log.Information("PHYPayload generated for DevAddr {DevAddr} ({generatedPacket} seconds of {totalTime})", device.DevAddr, device.IntervalSeconds / 1000 * i++, device.Duration);
                CborHelper.EncapsulatePhyPayload(phyPayload);
                if (rnd.Next(0, 100) < TriggerChance)
                {
                    CborHelper.TriggerCborPacketCreation();
                }
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

    public static byte[] GeneratePHYPayload(string devAddr, string nwkSKey, string appSKey)
    {
        byte[] phyPayload = PayloadBuilder.BuildPhyPayload(devAddr, nwkSKey, appSKey);
        Log.Debug("Generated PHYPayload for devaddr {devaddr}: {hexPayload}", devAddr, BitConverter.ToString(phyPayload).Replace("-", ""));

        return phyPayload;
    }

    public static byte[] GenerateTimedPHYPayload(Object source, ElapsedEventArgs e, string devAddr, string nwkSKey, string appSKey)
    {
        byte[] phyPayload = PayloadBuilder.BuildPhyPayload(devAddr, nwkSKey, appSKey);
        Log.Debug("Generated PHYPayload for devaddr {devaddr}: {hexPayload}", devAddr, BitConverter.ToString(phyPayload).Replace("-", ""));

        return phyPayload;
    }

}
