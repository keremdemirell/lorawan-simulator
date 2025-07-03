using System.Text;

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
        for (int i = 0; i < hex.Length; i+=2)
        {
            bytes[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
        }

        return bytes;
    }
}
