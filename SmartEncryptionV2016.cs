// Copyright © 2010-2027 Principal Research Corporation Ltd. All rights reserved.
// No part of this software may be reproduced, stored in a retrieval system, or transmitted
// in any form or by any means, electronic, mechanical, photocopying, recording, or otherwise,
// without the prior written permission of Principal Research Corporation Ltd.

using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

// Decoding Base64 with padding
// When decoding Base64 text, four characters are typically
// converted back to three bytes. The only exceptions are when
// padding characters exist. A single = indicates that the
// four characters will decode to only two bytes,
// while == indicates that the four characters will decode to
// only a single byte. For example:
// See https://en.wikipedia.org/wiki/Base64#Output_padding
// (That's why every one of our encrypted fields ends in == 
// so now you know =;-)     )

namespace SmartCubeMobile
{
    public class Potential
    {
        // Indigo, Charlie and a Schema name are all that
        // is needed to make a PDEK, FDEK and UDEK
        public int Random_Key { get; set; }     // Used to encrypt time
        public string November { get; set; }    // Time Now
        public string Uniform { get; set; }     // Username
        public string Lima { get; set; }        // Last Login Time
        public string Tango { get; set; }       // Trace
        public string Sierra { get; set; }      // Subscriber
        public string Mike { get; set; }        // Multi-Meter
        public string Alpha { get; set; }       // Administrator
        public string Echo { get; set; }        // Electricity Expiration
        public string Golf { get; set; }        // Gas Expiration
        public string Whisky { get; set; }      // Water Expiration
        public string Indigo { get; set; }      // User Id
        public string Papa { get; set; }        // Previous logon
        public string Charlie { get; set; }     // PasswordHash last 'ClearPassword.Length' chars        
    }

    public static class SmartEncryptionV2016
    {
        internal static string DoTheBiz(string source,
                                        string private_key,
                                        string rays,
                                        bool encrypt_or_decrypt)
        {
            try
            {
                //string original = "Here is some data to encrypt!;

                // Create a new instance of the Aes
                // class.  This generates a new key and initialization 
                // vector (IV).
                //using (
                Aes myAes = Aes.Create();
                string user_key = "";
                while (user_key.Length < 32)
                {
                    user_key += private_key;
                }
                // Encrypt the string to an array of bytes.
                // mykey_aes needs to be based on the Id (first 32 of 36 or 37 bytes) of the User
                //
                // If you follow the advice of THE CHIMPS and take out the '0' below
                // then you only get FOUR bytes instead of the THIRTY-TWO you need
                // Moral of this story? ALWAYS IGNORE ANY SO-CALLED 'ADVICE' FROM THE CHIMPS
                //
                byte[] mykey_aes = Encoding.ASCII.GetBytes(user_key.Substring(0, 32));
                // mykey_iv needs to be based on the Username (first 16 bytes of 18 bytes) of the User
                byte[] mykey_iv = Encoding.ASCII.GetBytes(user_key.Substring(user_key.Length - 16, 16));
                if (encrypt_or_decrypt)
                {
                    byte[] encrypted = EncryptStringToBytes_Aes(source, mykey_aes, mykey_iv);
                    return Convert.ToBase64String(encrypted);
                }
                else
                {
                    // Decrypt the bytes to a string.
                    byte[] decrypted = Convert.FromBase64String(rays);
                    return DecryptStringFromBytes_Aes(decrypted, mykey_aes, mykey_iv);
                }
            }
            catch (ArgumentNullException exception)
            {
                //ourviewmodel.errorMessage = exception.Message;
                return SmartParametersV2016.errorPrefix + exception.Message;
            }
            catch (Exception exception)
            {
                // If this gives you "Safe handle has been closed then the Key is WRONG i.e.
                // you have 'annidani' instead of 'ANNIDANI'
                //ourviewmodel.errorMessage = exception.Message;
                return SmartParametersV2016.errorPrefix + exception.Message;
            }
            //return "";
        }

        internal static byte[] EncryptStringToBytes_Aes(string plainText, byte[] Key, byte[] IV)
        {
            // What an ABSOLUTE B this was to get going
            // Wouldn't have done it without setting things to 'null' as per the
            // example in http://stackoverflow.com/questions/3831676/ca2202-how-to-solve-this-case

            // Check arguments.
            if (string.IsNullOrEmpty(plainText))
            {
                throw new ArgumentNullException(nameof(plainText));
            }
            if (Key == null || Key.Length <= 0)
            {
                throw new ArgumentNullException(nameof(Key));
            }
            if (IV == null || IV.Length <= 0)
            {
                throw new ArgumentNullException(nameof(Key));
            }
            byte[] encrypted;
            // Create an Aes object
            // with the specified key and IV.

            Aes aesAlg = null;
            MemoryStream msEncrypt = null;
            StreamWriter swEncrypt = null;
            CryptoStream csEncrypt = null;
            ICryptoTransform encryptor = null;

            try
            {
                aesAlg = Aes.Create();

                aesAlg.Key = Key;
                aesAlg.IV = IV;

                // Create a encrytor to perform the stream transform.
                encryptor = aesAlg.CreateEncryptor(aesAlg.Key, aesAlg.IV);

                // Create the streams used for encryption.
                msEncrypt = new MemoryStream();
                csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write);
                MemoryStream bastards = msEncrypt;
                msEncrypt = null;               // DON'T take this statement out
                swEncrypt = new StreamWriter(csEncrypt);
                CryptoStream cstream = csEncrypt;
                csEncrypt = null;               // DON'T take this statement out
                //Write all data to the stream.
                swEncrypt.Write(plainText);
                swEncrypt.Flush();              // DON'T take this statement out
                cstream.FlushFinalBlock();      // Flush out the stream or nothing works
                encrypted = bastards.ToArray();
            }
            finally
            {
                if (msEncrypt != null)
                {
                    msEncrypt?.Dispose();
                }
                if (encryptor != null)
                    encryptor?.Dispose();
                if (csEncrypt != null)
                    csEncrypt?.Dispose();
                if (swEncrypt != null)
                    swEncrypt?.Dispose();
                if (aesAlg != null)
                    aesAlg?.Dispose();
            }
            // Return the encrypted bytes from the memory stream.
            return encrypted;
        }

        internal static string DecryptStringFromBytes_Aes(byte[] cipherText, byte[] Key, byte[] IV)
        {
            // Check arguments.
            if (cipherText == null || cipherText.Length <= 0)
            {
                throw new ArgumentNullException(nameof(cipherText));
            }
            if (Key == null || Key.Length <= 0)
            {
                throw new ArgumentNullException(nameof(Key));
            }
            if (IV == null || IV.Length <= 0)
            {
                throw new ArgumentNullException(nameof(Key));
            }
            // Declare the string used to hold
            // the decrypted text.
            string plaintext = "";

            // Clear all these down
            Aes aesAlg = null;
            MemoryStream msDecrypt = null;
            CryptoStream csDecrypt = null;
            ICryptoTransform decryptor = null;
            StreamReader srDecrypt = null;

            try
            {
                // Create an Aes object
                // with the specified key and IV.
                aesAlg = Aes.Create();
                aesAlg.Key = Key;
                aesAlg.IV = IV;

                // Create a decrytor to perform the stream transform.
                decryptor = aesAlg.CreateDecryptor(aesAlg.Key, aesAlg.IV);

                // Create the streams used for decryption.
                msDecrypt = new MemoryStream(cipherText);
                csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read);
                msDecrypt = null;       // DON'T take this statement out
                srDecrypt = new StreamReader(csDecrypt);
                csDecrypt = null;       // DON'T take this statement out
                // Read the decrypted bytes from the decrypting stream
                // and place them in a string
                plaintext = srDecrypt.ReadToEnd();
            }
            finally
            {
                //if (msDecrypt != null)
                msDecrypt?.Dispose();
                //if (decryptor != null)
                decryptor?.Dispose();
                //if (csDecrypt != null)
                csDecrypt?.Dispose();
                //if (srDecrypt != null)
                srDecrypt?.Dispose();
                //if (aesAlg != null)
                aesAlg?.Dispose();
            }
            return plaintext;
        }

        // Called in Keasdon_Energy/Account/Login.aspx.cs and SmartCookies_V2016.login.cs
        internal static string MangleGuidKey(string guid)
        {
            // guid has already been checked to ensure it contains something
            string[] components = guid.Split(SmartParametersV2016.dashSplit);
            int components_count = components.Length;
            string guid_key = "";
            while (components_count > 0)
            {
                components_count--;// = components_count - 1;
                if (!string.IsNullOrEmpty(guid_key))
                {
                    switch (components_count)
                    {
                        case 3:
                            guid_key += SmartParametersV2016.equivalent;
                            break;
                        case 2:
                            guid_key += SmartParametersV2016.comma;
                            break;
                        case 1:
                            guid_key += SmartParametersV2016.ampersand;
                            break;
                        case 0:
                            guid_key += SmartParametersV2016.colon;
                            break;
                        default:
                            guid_key += SmartParametersV2016.dash;
                            break;
                    }
                }
                guid_key += components[components_count];
            }
            return guid_key;
        }
    }
}