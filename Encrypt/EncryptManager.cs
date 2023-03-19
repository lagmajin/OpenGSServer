

using System.Security.Cryptography;

namespace OpenGSServer
{ 
    
    
    public sealed class EncryptManager
    {
        public static EncryptManager Instance { get; set; }=new();

        private readonly Aes aes = Aes.Create();
        byte[] key = new byte[32];
        //private readonly byte[] key;
        private EncryptManager()
        { 
            
            
            
            using var rng = new RNGCryptoServiceProvider();

            rng.GetBytes(key);
        }






    }
}
