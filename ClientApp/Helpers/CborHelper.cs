using System.Timers;
using Microsoft.Extensions.Configuration;
using System.Formats.Cbor;
using Serilog;
public static class CborHelper
{
    public static Random rnd = new Random();
    public static List<CborPayload> CborPayloads = new();
    public static int AggregationId = 1;

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

    public static byte[] GenerateCborPacket()
    {

        CborHeader cborHeader = CborHelper.EncapsulateCbroPayloadWithHeader(CborPayloads.Count);

        CborPacket cborPacket = new CborPacket()
        {
            Header = cborHeader,
            Payloads = CborPayloads
        };

        byte[] cborData = CborHelper.SerializeToCbor(cborPacket);
        Log.Debug("Created Cbor: {Cbor}", BitConverter.ToString(cborData).Replace("-", ""));

        CborPayloads = new();

        return cborData;
    }
}