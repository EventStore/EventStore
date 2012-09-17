using System.IO;
using System.Security.Cryptography;

namespace EventStore.Core.Utilities
{
    public class MD5Hash
    {

         public static byte[] GetHashFor(Stream s)
         {
             //when using this, it will calculate from this point to the END of the stream!
             MD5 md5 = MD5.Create();
             return md5.ComputeHash(s);
         }
    }
}