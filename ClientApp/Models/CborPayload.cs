public class CborPayload
{
    public int RegionNumber { get; set; }
    public long Timestamp { get; set; }
    public int RSSI { get; set; }
    public int SNR { get; set; }
    public long Frequency { get; set; }
    public int FrequencyDrift { get; set; }
    public int FrequencyInit { get; set; }
    public int PayloadLength { get; set; }
    public byte[] Payload { get; set; }

    public CborPayload() {}

    public CborPayload(int regionNumber, long timestamp, int rSSI, int sNR, long frequency, int frequencyDrift, int frequencyInit,int payloadLength, byte[] payload)
    {
        RegionNumber = regionNumber;
        Timestamp = timestamp;
        RSSI = rSSI;
        SNR = sNR;
        Frequency = frequency;
        FrequencyDrift = frequencyDrift;
        PayloadLength = payloadLength;
        Payload = payload;

    }   
}