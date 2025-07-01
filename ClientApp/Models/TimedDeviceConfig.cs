public class TimedDeviceConfig
{
    public string DevAddr { get; set; }
    public string NwkSKey { get; set; }
    public string AppSKey { get; set; }
    public int Duration { get; set; }
    public int IntervalSeconds { get; set; }

    public TimedDeviceConfig(string devAddr, string nwkSKey, string appSKey, int duration, int intervalSeconds)
    {
        DevAddr = devAddr;
        NwkSKey = nwkSKey;
        AppSKey = appSKey;
        Duration = duration;
        IntervalSeconds = intervalSeconds;
    }
}