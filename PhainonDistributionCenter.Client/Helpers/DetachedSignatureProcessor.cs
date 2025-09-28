using System.Text;
using Org.BouncyCastle.Bcpg;
using Org.BouncyCastle.Bcpg.OpenPgp;
using PgpCore;

namespace PhainonDistributionCenter.Client.Helpers;

public static class DetachedSignatureProcessor
{

    /// <summary>
    /// 将字符串转换为 MemoryStream。
    /// </summary>
    /// <param name="s">输入字符串。</param>
    /// <returns>MemoryStream 对象。</returns>
    private static MemoryStream GenerateStreamFromString(string s)
    {
        return new MemoryStream(Encoding.UTF8.GetBytes(s));
    }


    public static string CreateSignature(
        string content,
        string keyIn,
        string passPhrase)
    {
        Stream outputStreamRaw = new MemoryStream();
        Stream outputStream = new ArmoredOutputStream(outputStreamRaw);
        

        var pgpSec = new EncryptionKeys(keyIn, passPhrase);
        PgpPrivateKey pgpPrivKey = pgpSec.PrivateKey;
        PgpSignatureGenerator sGen = new PgpSignatureGenerator(
            pgpSec.PrivateKey.PublicKeyPacket.Algorithm, HashAlgorithmTag.Sha1);

        sGen.InitSign(PgpSignature.BinaryDocument, pgpPrivKey);

        BcpgOutputStream bOut = new BcpgOutputStream(outputStream);

        Stream fIn = new MemoryStream(Encoding.UTF8.GetBytes(content));

        int ch;
        while ((ch = fIn.ReadByte()) >= 0)
        {
            sGen.Update((byte)ch);
        }

        fIn.Close();

        sGen.Generate().Encode(bOut);

        outputStream.Close();

        outputStreamRaw.Seek(0, SeekOrigin.Begin);
        return new StreamReader(outputStreamRaw).ReadToEnd();
    }
}