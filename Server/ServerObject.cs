using System;
using System.Collections.Generic;
using System.Text;
using MySql.Data.MySqlClient;
namespace PractiSES
{
    public class ServerObject : MarshalByRefObject
    {
        //complete the userID-email verification query
        public string InitKeySet_AskQuestions(string userID, string email)
        {
            Core core = new Core();
            string questions = core.ReadQuestions();

            string signQuestions = Crypto.Sign(questions, core.PrivateKey);

            questions = string.Concat(questions, signQuestions);
            return questions;

            /*Console.WriteLine("Connected");
            DatabaseConnection connection = new DatabaseConnection();
            string result = connection.setPublicKey(email);
            connection.close();
            return result;*/
        }

        public string InitKeySet_EncryptMACPass(string userID, string email)
        {
            //HashMAC mac = new HashMAC();
            //string hmac = mac.HMAC(;
//            Encryption encryption = new Encryption();
//            encryption.EncryptString
            return "";
        }
        
        public string KeyObt(string email) //get public key of a user ( complete )
        {
            Console.WriteLine("Connected");
            DatabaseConnection connection = new DatabaseConnection();
            string result = connection.getPublicKey(email);
            connection.close();
            return result;
            
        }

        public bool KeyRem(string userID, string email, string signedMessage)
        {
            Console.WriteLine("Connected");
            DatabaseConnection connection = new DatabaseConnection();
            bool result = connection.removeEntry(email, userID);
            connection.close();
            return result;
        }

        public bool KeyUpdate(string userID, string email, string signedMessage)
        {
            return true;
        }

        public void USKeyRem(string userID, string email)
        {
        }

        public void USKeyUpdate(string userID, string email, string newKey)
        {
        }

    }
}
