

using System.Security.Cryptography;

namespace OpenGSServer
{ 
    
    
    public sealed class EncryptManager
    {
        public static EncryptManager Instance { get; set; }=new();

        private readonly Aes aes = Aes.Create();
        byte[] key = new byte[32];

        //private RNGCryptoServiceProvider provier = new();

        private RSACryptoServiceProvider rsa;
        //private readonly byte[] key;
        private EncryptManager()
        {
            byte[] publicModules;
            byte[] publicExponent;

            rsa = new RSACryptoServiceProvider(1024);

            var test=rsa.ToXmlString(true);

            RSAParameters publicParam = rsa.ExportParameters(false);
            publicModules = publicParam.Modulus;
            publicExponent = publicParam.Exponent;
            //rsa.ToString(false);



        }

        public string GetRSAPublicKey()
        {
            var publicKey = rsa.ToXmlString(false);

            return publicKey;
        }

        public string GetRSAPrivateKey()
        {
            var privateKey = rsa.ToXmlString(false);
            return privateKey;
        }

        void Dispose()
        {

        }




    }
}
