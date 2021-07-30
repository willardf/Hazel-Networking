using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hazel.Crypto
{
    public static class CryptoProvider
    {
        public delegate IAes CreateAesOverrideDelegate(ByteSpan key);

        /// <summary>
        /// Override the default AES creation function
        /// </summary>
        public static CreateAesOverrideDelegate OverrideCreateAes = null;

        /// <summary>
        /// Create a new AES cipher
        /// </summary>
        /// <param name="key">Encrtyption key</param>
        public static IAes CreateAes(ByteSpan key)
        {
            if (OverrideCreateAes != null)
            {
                IAes result = OverrideCreateAes(key);
                if (null != result)
                {
                    return result;
                }
            }

            return new DefaultAes(key);
        }
    }
}
