public class CborHeader
{
    public int SatelliteId { get; set; }
    public int AggregationId { get; set; }
    public long TimeStamp { get; set; }
    public int PayloadLength { get; set; }

    public CborHeader() {}

    public CborHeader(int satelliteId, int aggregationId, long timestamp, int payloadlen)
    {
        SatelliteId = satelliteId;
        AggregationId = aggregationId;
        TimeStamp = timestamp;
        PayloadLength = payloadlen;
    }
    
}