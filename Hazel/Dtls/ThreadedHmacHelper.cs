using System;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Threading;

namespace Hazel.Dtls
{
    internal class ThreadedHmacHelper : IDisposable
    {
        private static readonly TimeSpan CookieHmacRotationTimeout = TimeSpan.FromHours(1.0);

        private readonly ILogger logger;
        private readonly ConcurrentDictionary<int, HMAC> currentHmacs;
        private readonly ConcurrentDictionary<int, HMAC> previousHmacs;

        private DateTime nextCookieHmacRotation;

        public ThreadedHmacHelper(ILogger logger)
        {
            this.currentHmacs = new ConcurrentDictionary<int, HMAC>();
            this.previousHmacs = new ConcurrentDictionary<int, HMAC>();
            this.nextCookieHmacRotation = DateTime.UtcNow + CookieHmacRotationTimeout;

            this.logger = logger;
        }

        /// <summary>
        /// [ThreadSafe] Get the current cookie hmac for the current thread.
        /// </summary>
        public HMAC GetCurrentCookieHmacsForThread()
        {
            int threadId = Thread.CurrentThread.ManagedThreadId;
            RotateKeys(threadId);

            if (!this.currentHmacs.TryGetValue(threadId, out HMAC currentCookieHmac))
            {
                currentCookieHmac = CreateNewCookieHMAC();
                if (!this.currentHmacs.TryAdd(threadId, currentCookieHmac))
                {
                    this.logger.WriteError($"Cannot add currentCookieHmac to currentHmacs! - Should never happen - should be accessed only by a single thread");
                }
            }

            return currentCookieHmac;
        }

        /// <summary>
        /// [ThreadSafe] Get the previous cookie hmac for the current thread.
        /// </summary>
        public HMAC GetPreviousCookieHmacsForThread()
        {
            int threadId = Thread.CurrentThread.ManagedThreadId;
            RotateKeys(threadId);

            if (!this.previousHmacs.TryGetValue(threadId, out HMAC previousCookieHmac))
            {
                previousCookieHmac = CreateNewCookieHMAC();
                if (!this.previousHmacs.TryAdd(threadId, previousCookieHmac))
                {
                    this.logger.WriteError($"Cannot add previousCookieHmac to previousHmacs! - Should never happen - should be accessed only by a single thread");
                }
            }

            return previousCookieHmac;
        }

        public void Dispose()
        {
            foreach (var threadIdToHmac in this.currentHmacs)
            {
                threadIdToHmac.Value.Dispose();
            }

            this.currentHmacs.Clear();

            foreach (var threadIdToHmac in this.previousHmacs)
            {
                threadIdToHmac.Value.Dispose();
            }

            this.previousHmacs.Clear();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="threadId">Managed thread Id of thread calling this method.</param>
        private void RotateKeys(int threadId)
        {
            // Do we need to rotate the HMAC key?
            DateTime now = DateTime.UtcNow;
            if (now <= this.nextCookieHmacRotation)
            {
                return;
            }

            if (this.previousHmacs.TryRemove(threadId, out HMAC previousHmac)) 
            {
                previousHmac.Dispose();
            }

            if (!this.currentHmacs.TryGetValue(threadId, out HMAC currentHmac))
            {
                currentHmac = CreateNewCookieHMAC();
                this.logger.WriteError($"currentHmac did not exist when rotating keys - Should not happen");
            }

            if (!this.previousHmacs.TryAdd(threadId, currentHmac))
            {
                this.logger.WriteError($"Cannot add currentHmac to previousHmacs during rotation! - Should never happen - should be accessed only by a single thread");
            };

            currentHmac = CreateNewCookieHMAC();

            if (!this.currentHmacs.TryAdd(threadId, currentHmac))
            {
                this.logger.WriteError($"Cannot add currentHmac to currentHmacs during rotation! - Should never happen - should be accessed only by a single thread");
            };

            this.nextCookieHmacRotation = now + CookieHmacRotationTimeout;
        }

        /// <summary>
        /// Create a new cookie HMAC signer
        /// </summary>
        private static HMAC CreateNewCookieHMAC()
        {
            const string HMACProvider = "System.Security.Cryptography.HMACSHA1";
            return HMAC.Create(HMACProvider);
        }
    }
}
