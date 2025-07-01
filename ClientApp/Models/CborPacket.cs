public class CborPacket
{
    public CborHeader Header { get; set; }
    public List<CborPayload> Payloads { get; set; }

    public CborPacket() {}

    public CborPacket(CborHeader header, List<CborPayload> payloads)
    {
        Header = header;
        Payloads = payloads;
    }
}