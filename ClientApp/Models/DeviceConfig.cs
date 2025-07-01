public class DeviceConfig
{
    public string DevAddr { get; set; }
    public string NwkSKey { get; set; }
    public string AppSKey { get; set; }
    public int PacketSize { get; set; }

    public DeviceConfig(string devAddr, string nwkSKey, string appSKey, int packetSize)
    {
        DevAddr = devAddr;
        NwkSKey = nwkSKey;
        AppSKey = appSKey;
        PacketSize = packetSize;
    }
}
