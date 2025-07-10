using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Macs;
using Org.BouncyCastle.Crypto.Parameters;

public static class EncrypytHelper
{
    public static byte[] CreateKeystreamBlock(
        byte[] appSKey,     // 16 bytes AES key
        byte dir,     // 0 = uplink, 1 = downlink
        byte[] devAddr,     // 4 bytes, little-endian
        int fcnt,        // frame counter
        byte i     // 1-based block number (1, 2, ...)
    )
    {
        // Building block A
        byte[] blockA = new byte[16];
        blockA[0] = 0x01;

        blockA[1] = 0x00;
        blockA[2] = 0x00;
        blockA[3] = 0x00;
        blockA[4] = 0x00;

        blockA[5] = dir;

        blockA[6] = devAddr[0];
        blockA[7] = devAddr[1];
        blockA[8] = devAddr[2];
        blockA[9] = devAddr[3];

        blockA[10] = (byte)(fcnt & 0xFF);
        blockA[11] = (byte)((fcnt >> 8) & 0xFF);
        blockA[12] = 0x00;
        blockA[13] = 0x00;

        blockA[14] = 0x00;

        blockA[15] = i;

        // Building Si (key stream block)
        var aes = new AesEngine();
        aes.Init(true, new KeyParameter(appSKey));

        byte[] Si = new byte[16];
        aes.ProcessBlock(blockA, 0, Si, 0);

        return Si;
    }
    public static byte[] EncryptPayload(byte[] payload, byte[] keystream)
    {

        byte[] encrypted = new byte[payload.Length];

        int j = 0;
        int keyStreamLength = keystream.Length;


        for (int i = 0; i < payload.Length; i++)
        {
            encrypted[i] = (byte)(payload[i] ^ keystream[j]);

            j++;
            if (j == keyStreamLength)
            {
                j = 0;
            }
        }

        return encrypted;
                
                /*
        byte[] encrypted = new byte[payload.Length];

        for (int i = 0; i < payload.Length; i++)
        {
            encrypted[i] = (byte)(payload[i] ^ keystream[i]);
        }

        return encrypted;
        */

    }

    public static byte[] CalculateMIC(
        byte[] key,
        byte[] devAddr,
        int fcnt,
        byte[] msg,
        byte dir
    )
    {
        // Building block B
        byte[] blockB = new byte[16];

        blockB[0] = 0x49;

        blockB[1] = 0x00;
        blockB[2] = 0x00;
        blockB[3] = 0x00;
        blockB[4] = 0x00;

        blockB[5] = dir;

        blockB[6] = devAddr[0];
        blockB[7] = devAddr[1];
        blockB[8] = devAddr[2];
        blockB[9] = devAddr[3];

        blockB[10] = (byte)(fcnt & 0xFF);
        blockB[11] = (byte)((fcnt >> 8) & 0xFF);
        blockB[12] = 0x00;
        blockB[13] = 0x00;

        blockB[14] = 0x00;

        blockB[15] = (byte) msg.Length;

        // Calculating MIC
        byte[] input = new byte[blockB.Length + msg.Length];
        Array.Copy(blockB, 0, input, 0, blockB.Length);
        Array.Copy(msg, 0, input, blockB.Length, msg.Length);

        CMac cmac = new CMac(new AesEngine());
        cmac.Init(new KeyParameter(key));

        cmac.BlockUpdate(input, 0, input.Length);

        byte[] finalMIC = new byte[16];
        cmac.DoFinal(finalMIC, 0);

        return finalMIC.Take(4).ToArray();
    }
}