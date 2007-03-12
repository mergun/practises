/*
 * $Id$
 */

using System;
using System.Text;
using System.Collections;
using System.Security.Cryptography;

namespace PractiSES
{
	public static class Crypto
	{
        private static int dwKeySize = 2048;

        public static RSACryptoServiceProvider GetRSA()
        {
            RSACryptoServiceProvider rsa = new RSACryptoServiceProvider(dwKeySize);
            return rsa;
        }

        public static RSACryptoServiceProvider GetRSA(String key)
        {
            RSACryptoServiceProvider rsa = Crypto.GetRSA();
            rsa.FromXmlString(key);
            return rsa;
        }

        public static String Sign(String clearText, String privateKey)
        {
            String signedMessage = "";

            signedMessage += "-----BEGIN PRACTISES SIGNED MESSAGE-----";
            signedMessage += Environment.NewLine;
            signedMessage += clearText;
            signedMessage += Environment.NewLine;
            signedMessage += "-----BEGIN PRACTISES SIGNATURE-----";
            signedMessage += Environment.NewLine;
            signedMessage += Util.Wrap(Crypto.RSAGetSignature(clearText, privateKey), 64);
            signedMessage += Environment.NewLine;
            signedMessage += "-----END PRACTISES SIGNATURE-----";
            signedMessage += Environment.NewLine;

            return signedMessage;
        }

        public static String Encrypt(String clearText, String publicKey)
        {
            String cipherText = "";

            cipherText += "-----BEGIN PRACTISES MESSAGE-----";
            cipherText += Environment.NewLine;
            cipherText += Util.Wrap(Crypto.RSAEncrypt(clearText, publicKey), 64);
            cipherText += Environment.NewLine;
            cipherText += "-----END PRACTISES MESSAGE-----";
            cipherText += Environment.NewLine;

            return cipherText;
        }

        public static String SignAndEncrypt(String clearText, String publicKey, String privateKey)
        {
            String signedMessage = Crypto.Sign(clearText, privateKey);
            return Crypto.Encrypt(signedMessage, publicKey);
        }

        private static String RSAEncrypt(String clearText, String publicKey)
        {
            RSACryptoServiceProvider rsa = Crypto.GetRSA(publicKey);
            
            int keySize = Crypto.dwKeySize / 8;
            byte[] bytes = Encoding.UTF8.GetBytes(clearText);
            int maxLength = keySize - 42;
            int dataLength = bytes.Length;
            int iterations = dataLength / maxLength;
            StringBuilder StringBuilder = new StringBuilder();

            for (int i = 0; i <= iterations; i++)
            {
                byte[] tempBytes = new byte[(dataLength - maxLength * i > maxLength) ? maxLength : dataLength - maxLength * i];
                Buffer.BlockCopy(bytes, maxLength * i, tempBytes, 0, tempBytes.Length);
                byte[] encryptedBytes = rsa.Encrypt(tempBytes, true);
                StringBuilder.Append(Convert.ToBase64String(encryptedBytes));
            }

            return StringBuilder.ToString();
        }

        private static String RSADecrypt(String cipherText, String privateKey)
        {
            RSACryptoServiceProvider rsa = Crypto.GetRSA(privateKey);

            int base64BlockSize = ((dwKeySize / 8) % 3 != 0) ?
              (((dwKeySize / 8) / 3) * 4) + 4 : ((dwKeySize / 8) / 3) * 4;

            int iterations = cipherText.Length / base64BlockSize;

            ArrayList arrayList = new ArrayList();

            for (int i = 0; i < iterations; i++)
            {
                byte[] encryptedBytes = Convert.FromBase64String(cipherText.Substring(base64BlockSize * i, base64BlockSize));
                arrayList.AddRange(rsa.Decrypt(encryptedBytes, true));
            }

            return Encoding.UTF8.GetString(arrayList.ToArray(Type.GetType("System.Byte")) as byte[]);
        }

        private static String RSAGetSignature(String clearText, String privateKey)
        {
            RSACryptoServiceProvider rsa = Crypto.GetRSA(privateKey);

            byte[] bytes = Encoding.UTF8.GetBytes(clearText);
            byte[] signature = rsa.SignData(bytes, new SHA1CryptoServiceProvider());
            return Convert.ToBase64String(signature);
        }

        public static byte[] DeriveKey(String passphrase)
        {
            const int saltLength = 8;

            byte[] passphraseBytes = Encoding.UTF8.GetBytes(passphrase);
            byte[] salt = new byte[saltLength];
            
            Random random = new Random(); 
            random.NextBytes(salt);
            
            return PBKDF2(passphraseBytes, salt, 10000, 32);
        }
        
        /*
         * PBKDF2 as described in PKCS #5 v2.0 pp.8-10
         */
        private static byte[] PBKDF2(byte[] passphrase, byte[] salt, int c, int dkLen)
        {
            int hLen = 20;
            int l = (int)Math.Ceiling((double)dkLen / hLen);
            int r = dkLen - (l - 1) * hLen;

            ArrayList result = new ArrayList(dkLen);

            for (int i = 0; i < l; i++)
            {
                result.AddRange(F(passphrase, salt, c, i));
            }

            return (byte [])result.GetRange(0, dkLen).ToArray(System.Type.GetType("System.Byte"));
        }

        /*
         * F as described in PKCS #5 v2.0 p.9
         */
        private static byte[] F(byte[] passphrase, byte[] salt, int c, int i)
        {
            const int hLen = 20;

            HMACSHA1 hmac = new HMACSHA1(passphrase);

            byte[] result = hmac.ComputeHash(Util.Join(salt, BitConverter.GetBytes(i)));

            for(int j = 1; j < c; j++)
            {
                result = Util.XOR(result, hmac.ComputeHash(result));
            }           

            return result;
        }

        //public static String AESEncrypt(String clearText, String passphrase)
        //{
        //    try
        //    {
        //        // Create or open the specified file.
        //        FileStream fStream = File.Open(FileName, FileMode.OpenOrCreate);

        //        // Create a new Rijndael object.
        //        Rijndael RijndaelAlg = Rijndael.Create();

        //        // Create a CryptoStream using the FileStream 
        //        // and the passed key and initialization vector (IV).
        //        CryptoStream cStream = new CryptoStream(fStream,
        //            RijndaelAlg.CreateEncryptor(Key, IV),
        //            CryptoStreamMode.Write);

        //        // Create a StreamWriter using the CryptoStream.
        //        StreamWriter sWriter = new StreamWriter(cStream);

        //        try
        //        {
        //            // Write the data to the stream 
        //            // to encrypt it.
        //            sWriter.WriteLine(Data);
        //        }
        //        catch (Exception e)
        //        {
        //            Console.WriteLine("An error occurred: {0}", e.Message);
        //        }
        //        finally
        //        {
        //            // Close the streams and
        //            // close the file.
        //            sWriter.Close();
        //            cStream.Close();
        //            fStream.Close();
        //        }
        //    }
        //    catch (CryptographicException e)
        //    {
        //        Console.WriteLine("A Cryptographic error occurred: {0}", e.Message);
        //    }
        //    catch (UnauthorizedAccessException e)
        //    {
        //        Console.WriteLine("A file error occurred: {0}", e.Message);
        //    }

        //}

        //public static string AESDecrypt(String cipherText, String passphrase)
        //{
        //    try
        //    {
        //        // Create or open the specified file. 
        //        FileStream fStream = File.Open(FileName, FileMode.OpenOrCreate);

        //        // Create a new Rijndael object.
        //        Rijndael RijndaelAlg = Rijndael.Create();

        //        // Create a CryptoStream using the FileStream 
        //        // and the passed key and initialization vector (IV).
        //        CryptoStream cStream = new CryptoStream(fStream,
        //            RijndaelAlg.CreateDecryptor(Key, IV),
        //            CryptoStreamMode.Read);

        //        // Create a StreamReader using the CryptoStream.
        //        StreamReader sReader = new StreamReader(cStream);

        //        string val = null;

        //        try
        //        {
        //            // Read the data from the stream 
        //            // to decrypt it.
        //            val = sReader.ReadLine();


        //        }
        //        catch (Exception e)
        //        {
        //            Console.WriteLine("An error occurred: {0}", e.Message);
        //        }
        //        finally
        //        {

        //            // Close the streams and
        //            // close the file.
        //            sReader.Close();
        //            cStream.Close();
        //            fStream.Close();
        //        }

        //        // Return the string. 
        //        return val;
        //    }
        //    catch (CryptographicException e)
        //    {
        //        Console.WriteLine("A Cryptographic error occurred: {0}", e.Message);
        //        return null;
        //    }
        //    catch (UnauthorizedAccessException e)
        //    {
        //        Console.WriteLine("A file error occurred: {0}", e.Message);
        //        return null;
        //    }
        //}
	}
}