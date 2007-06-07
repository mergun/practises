﻿/*
 * $Id$
 */

using System;
using System.IO;
using System.Text;
using System.Collections;
using System.Security.Cryptography;

namespace PractiSES
{
	public static class Crypto
	{
        public const int RSAKeySize = 2048;
        public const int AESKeySize = 256;
        public const int AESIVSize = 128;
        public const int Wrap = 64;

        public const String BeginSignedMessage = "-----BEGIN PRACTISES SIGNED MESSAGE-----";
        public const String BeginSignature = "-----BEGIN PRACTISES SIGNATURE-----";
        public const String EndSignature = "-----END PRACTISES SIGNATURE-----";
        public const String BeginMessage = "-----BEGIN PRACTISES MESSAGE-----";
        public const String EndMessage = "-----END PRACTISES MESSAGE-----";

        public static RSACryptoServiceProvider GetRSA()
        {
            RSACryptoServiceProvider rsa = new RSACryptoServiceProvider(Crypto.RSAKeySize);
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
            StringWriter sw = new StringWriter();

            sw.WriteLine(BeginSignedMessage);
            sw.WriteLine();
            sw.WriteLine(clearText);
            sw.WriteLine(BeginSignature);
            sw.WriteLine(Util.Wrap(Crypto.RSAGetSignature(clearText, privateKey), Wrap));
            sw.WriteLine(EndSignature);

            return sw.ToString();
        }

        public static byte[] Sign(byte[] cleartext, String privatekey)
        {
            RSACryptoServiceProvider rsa = Crypto.GetRSA(privatekey);
            return rsa.SignData(cleartext, new SHA1CryptoServiceProvider());
        }

        public static String SignDetached(byte[] data, String privateKey)
        {
            StringWriter sw = new StringWriter();

            sw.WriteLine(BeginSignature);
            sw.WriteLine(Util.Wrap(Crypto.RSAGetSignature(data, privateKey), Wrap));
            sw.WriteLine(EndSignature);

            return sw.ToString();
        }

        public static String Encrypt(byte[] clearText, String publicKey)
        {
            Rijndael aes = Rijndael.Create();
            return Crypto.Encrypt(clearText, publicKey, aes);
        }

        public static String Encrypt(byte[] clearText, String publicKey, Rijndael aes)
        {
            StringWriter cipherText = new StringWriter();

            ArrayList message = new ArrayList();
            message.AddRange(Crypto.RSAEncrypt(aes.Key, publicKey));
            message.AddRange(Crypto.RSAEncrypt(aes.IV, publicKey));
            message.AddRange(Crypto.AESEncrypt(clearText, aes.CreateEncryptor()));

            String messageArmor = Convert.ToBase64String((byte[])message.ToArray(Type.GetType("System.Byte")));

            cipherText.WriteLine(Crypto.BeginMessage);
            cipherText.WriteLine("Version: PractiSES {0} (Win32)", System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString(2));
            cipherText.WriteLine();
            cipherText.WriteLine(Util.Wrap(messageArmor, Wrap));
            cipherText.WriteLine(Crypto.EndMessage);

            return cipherText.ToString();
        }

        public static byte[] RAWEncrypt(byte[] clearText, String publicKey)
        {
            Rijndael aes = Rijndael.Create();
            ArrayList message = new ArrayList();

            message.AddRange(Crypto.RSAEncrypt(aes.Key, publicKey));
            message.AddRange(Crypto.RSAEncrypt(aes.IV, publicKey));
            message.AddRange(Crypto.AESEncrypt(clearText, aes.CreateEncryptor()));

            return (byte[])message.ToArray(Type.GetType("System.Byte"));
        }

        public static byte[] RAWDecrypt(byte[] cipherText, String privateKey)
        {
            int ebs = RSAKeySize / 8;
            Rijndael aes = Rijndael.Create();
            ArrayList bytes = new ArrayList(cipherText);
            byte[] keyPart = (byte[])bytes.GetRange(0, ebs).ToArray(Type.GetType("System.Byte"));

            byte[] key = RSADecrypt(keyPart, privateKey);
            byte[] IV = RSADecrypt((byte[])bytes.GetRange(ebs, ebs).ToArray(Type.GetType("System.Byte")), privateKey);
            byte[] message = (byte[])bytes.GetRange(ebs * 2, bytes.Count - ebs * 2).ToArray(Type.GetType("System.Byte"));

            return AESDecrypt(message, aes.CreateDecryptor(key, IV));
        }

        public static byte[] Decrypt(String cipherText, String privateKey)
        {
            Rijndael aes = Rijndael.Create();

            AESInfo message = Destruct(cipherText, privateKey);

            return AESDecrypt(message.message, aes.CreateDecryptor(message.key, message.IV));
        }

        public static AESInfo Destruct(String message, String privateKey)
        {
            int ebs = RSAKeySize / 8;

            AESInfo result = new AESInfo();

            ArrayList bytes = new ArrayList(Convert.FromBase64String(StripMessage(message)));

            byte[] keyPart = (byte[])bytes.GetRange(0, ebs).ToArray(Type.GetType("System.Byte"));

            result.key = RSADecrypt(keyPart, privateKey);
            result.IV = RSADecrypt((byte[])bytes.GetRange(ebs, ebs).ToArray(Type.GetType("System.Byte")), privateKey);
            result.message = (byte[])bytes.GetRange(ebs * 2, bytes.Count - ebs * 2).ToArray(Type.GetType("System.Byte"));

            return result;
        }

        public static String SignAndEncrypt(String clearText, String publicKey, String privateKey)
        {
            String signedMessage = Crypto.Sign(clearText, privateKey);
            return Crypto.Encrypt(Encoding.ASCII.GetBytes(signedMessage), publicKey);
        }

        private static byte[] RSAEncrypt(byte[] rgb, String publicKey)
        {
            RSACryptoServiceProvider rsa = Crypto.GetRSA(publicKey);
            return rsa.Encrypt(rgb, true);
        }

        public static byte[] RSADecrypt(byte[] rgb, String privateKey)
        {
            RSACryptoServiceProvider rsa = Crypto.GetRSA(privateKey);
            return rsa.Decrypt(rgb, true);
        }

        public static bool Verify(byte[] data, byte[] signature, String publicKey)
        {
            RSACryptoServiceProvider rsa = Crypto.GetRSA(publicKey);
            return rsa.VerifyData(data, new SHA1CryptoServiceProvider(), signature);
        }

        private static String RSAGetSignature(String clearText, String privateKey)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(clearText);
            return Crypto.RSAGetSignature(bytes, privateKey);
        }

        private static String RSAGetSignature(byte[] data, String privateKey)
        {
            return Convert.ToBase64String(Sign(data, privateKey));
        }

        private static AESInfo DeriveKeyAndIV(String passphrase, byte[] salt, int keyLen, int IVLen)
        {
            const int saltLength = 8;
            int k = keyLen / 8;
            int i = IVLen / 8;

            AESInfo result = new AESInfo();

            byte[] passphraseBytes = Encoding.UTF8.GetBytes(passphrase);

            if (salt == null)
            {
                result.salt = new byte[saltLength];
                Random random = new Random();
                random.NextBytes(result.salt);
            }
            else
            {
                result.salt = salt;
            }

            ArrayList keyAndIV = new ArrayList(k + i);
            keyAndIV.AddRange(PBKDF2(passphraseBytes, result.salt, 10000, k + i));

            result.key = (byte[])keyAndIV.GetRange(0, k).ToArray(Type.GetType("System.Byte"));
            result.IV = (byte[])keyAndIV.GetRange(k, i).ToArray(Type.GetType("System.Byte"));

            return result;
        }
        
        /*
         * PBKDF2 as described in PKCS #5 v2.0 pp.8-10
         */
        private static byte[] PBKDF2(byte[] passphrase, byte[] salt, int c, int dkLen)
        {
            const int hLen = 20;
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
            HMACSHA1 hmac = new HMACSHA1(passphrase);

            byte[] result = hmac.ComputeHash(Util.Join(salt, BitConverter.GetBytes(i)));

            for(int j = 1; j < c; j++)
            {
                result = Util.XOR(result, hmac.ComputeHash(result));
            }           

            return result;
        }

        public static AESInfo AESEncrypt(byte[] clearText, String passphrase)
        {            
            Rijndael aes = Rijndael.Create();

            AESInfo aesInfo = Crypto.DeriveKeyAndIV(passphrase, null, Crypto.AESKeySize, Crypto.AESIVSize);
            aesInfo.message = Crypto.AESEncrypt(clearText, aes.CreateEncryptor(aesInfo.key, aesInfo.IV));

            return aesInfo;
        }

        public static byte[] AESDecrypt(byte[] cipherText, String passphrase, byte[] salt)
        {
            Rijndael aes = Rijndael.Create();

            AESInfo aesInfo = Crypto.DeriveKeyAndIV(passphrase, salt, Crypto.AESKeySize, Crypto.AESIVSize);

            return AESDecrypt(cipherText, aes.CreateDecryptor(aesInfo.key, aesInfo.IV));
        }
        
        public static byte[] AESEncrypt(byte[] clearText, ICryptoTransform transform)
        {
            Rijndael aes = Rijndael.Create();
            
            MemoryStream memoryStream = new MemoryStream();
            
            CryptoStream cryptoStream = new CryptoStream(memoryStream, transform, CryptoStreamMode.Write);
            cryptoStream.Write(clearText, 0, clearText.Length);
            cryptoStream.Close();

            byte[] result = memoryStream.ToArray();
            memoryStream.Close();

            return result;
        }

        public static byte[] AESDecrypt(byte[] cipherText, ICryptoTransform transform)
        {
            Rijndael aes = Rijndael.Create();

            MemoryStream memoryStream = new MemoryStream(cipherText);
            CryptoStream cryptoStream = new CryptoStream(memoryStream, transform, CryptoStreamMode.Read);

            byte[] clearText = new byte[cipherText.Length];

            int count = cryptoStream.Read(clearText, 0, clearText.Length);

            byte[] result = new byte[count];
            Array.Copy(clearText, result, count);

            cryptoStream.Close();
            memoryStream.Close();

            return result;
        }

        public static String StripMessage(String message)
        {
            StringReader sr = new StringReader(message);
            String contents = "";
            String line;
            Boolean inMessage = false;
            Boolean signedMessage = false;

            while (true)
            {
                line = sr.ReadLine();
                if (line == null)
                {
                    break;
                }

                line.Trim();

                if (line == BeginMessage || line == BeginSignedMessage)
                {
                    if (line == BeginSignedMessage)
                    {
                        signedMessage = true;
                    }

                    do
                    {
                        line = sr.ReadLine();
                        if(line != null)
                            line.Trim();
                    }
                    while (line != "" && line != null);

                    inMessage = true;
                }
                else if (line == EndMessage || line == BeginSignature)
                {
                    break;
                }
                else if (inMessage)
                {
                    contents += line;

                    if (signedMessage)
                    {
                        contents += Environment.NewLine;
                    }
                }
            }

            return contents.Trim();
        }
	}
}
