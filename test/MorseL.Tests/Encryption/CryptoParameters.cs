using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace MorseL.Tests.Encryption
{
    public class CryptoParameters
    {
        public byte[] Key { get; set; }
        public byte[] IV { get; set; }

        public CryptoParameters() { }

        public CryptoParameters(SymmetricAlgorithm algorithm) : this(algorithm.Key, algorithm.IV) { }

        public CryptoParameters(byte[] key, byte[] iv)
        {
            Key = key;
            IV = iv;
        }

        public byte[] ToBytes()
        {
            byte[] bytes = new byte[Key.Length + IV.Length];
            Buffer.BlockCopy(Key, 0, bytes, 0, Key.Length);
            Buffer.BlockCopy(IV, 0, bytes, Key.Length, IV.Length);
            return bytes;
        }

        public static CryptoParameters FromBytes(byte[] bytes)
        {
            byte[] key = new byte[bytes.Length - 16];
            byte[] iv = new byte[16];
            Buffer.BlockCopy(bytes, 0, key, 0, key.Length);
            Buffer.BlockCopy(bytes, key.Length, iv, 0, iv.Length);
            return new CryptoParameters()
            {
                Key = key,
                IV = iv
            };
        }
    }
}
