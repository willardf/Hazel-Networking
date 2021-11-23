using System;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Threading;

namespace Hazel.Dtls
{
    internal class ThreadedHmacHelper : IDisposable
    {
        private class ThreadHmacs
        {
            public HMAC currentHmac;
            public HMAC previousHmac;
            public HMAC hmacToDispose;
        }

        private static readonly int CookieHmacRotationTimeout = (int)TimeSpan.FromHours(1.0).TotalMilliseconds;

        private readonly ILogger logger;
        private readonly ConcurrentDictionary<int, ThreadHmacs> hmacs;
        private Timer rotateKeyTimer;
        private RandomNumberGenerator cryptoRandom;
        private byte[] currentHmacKey;

        public ThreadedHmacHelper(ILogger logger)
        {
            this.hmacs = new ConcurrentDictionary<int, ThreadHmacs>();
            this.rotateKeyTimer = new Timer(RotateKeys, null, CookieHmacRotationTimeout, CookieHmacRotationTimeout);
            this.cryptoRandom = RandomNumberGenerator.Create();

            this.logger = logger;
            SetHmacKey();
        }

        /// <summary>
        /// [ThreadSafe] Get the current cookie hmac for the current thread.
        /// </summary>
        public HMAC GetCurrentCookieHmacsForThread()
        {
            return GetHmacsForThread().currentHmac;
        }

        /// <summary>
        /// [ThreadSafe] Get the previous cookie hmac for the current thread.
        /// </summary>
        public HMAC GetPreviousCookieHmacsForThread()
        {
            return GetHmacsForThread().previousHmac;
        }

        public void Dispose()
        {
            ManualResetEvent signalRotateKeyTimerEnded = new ManualResetEvent(false);
            this.rotateKeyTimer.Dispose(signalRotateKeyTimerEnded);
            signalRotateKeyTimerEnded.WaitOne();
            signalRotateKeyTimerEnded.Dispose();
            signalRotateKeyTimerEnded = null;
            this.rotateKeyTimer = null;

            this.cryptoRandom.Dispose();
            this.cryptoRandom = null;

            foreach (var threadIdToHmac in this.hmacs)
            {
                ThreadHmacs threadHmacs = threadIdToHmac.Value;
                threadHmacs.currentHmac?.Dispose();
                threadHmacs.currentHmac = null;
                threadHmacs.previousHmac?.Dispose();
                threadHmacs.previousHmac = null;
                threadHmacs.hmacToDispose?.Dispose();
                threadHmacs.hmacToDispose = null;
            }

            this.hmacs.Clear();
        }

        private ThreadHmacs GetHmacsForThread()
        {
            int threadId = Thread.CurrentThread.ManagedThreadId;

            if (!this.hmacs.TryGetValue(threadId, out ThreadHmacs threadHmacs))
            {
                threadHmacs = CreateNewThreadHmacs();

                if (!this.hmacs.TryAdd(threadId, threadHmacs))
                {
                    this.logger.WriteError($"Cannot add threadHmacs for thread {threadId} during GetHmacsForThread! Should never happen!");
                }
            }

            return threadHmacs;
        }

        /// <summary>
        /// Rotates the hmacs of all active threads
        /// </summary>
        private void RotateKeys(object _)
        {
            SetHmacKey();

            foreach (var threadIds in this.hmacs)
            {
                RotateKey(threadIds.Key);
            }
        }

        /// <summary>
        /// Rotate hmacs of single thread
        /// </summary>
        /// <param name="threadId">Managed thread Id of thread calling this method.</param>
        private void RotateKey(int threadId)
        {
            ThreadHmacs threadHmacs;

            if (!this.hmacs.TryGetValue(threadId, out threadHmacs))
            {
                this.logger.WriteError($"Cannot find thread {threadId} in hmacs during rotation! Should never happen!");
                return;
            }

            // No thread should still have a reference to hmacToDispose, which should now have a lifetime of > 1 hour
            threadHmacs.hmacToDispose?.Dispose();
            threadHmacs.hmacToDispose = threadHmacs.previousHmac;
            threadHmacs.previousHmac = threadHmacs.currentHmac;
            threadHmacs.currentHmac = CreateNewCookieHMAC();
        }

        private ThreadHmacs CreateNewThreadHmacs()
        {
            return new ThreadHmacs
            {
                previousHmac = CreateNewCookieHMAC(),
                currentHmac = CreateNewCookieHMAC()
            };
        }

        /// <summary>
        /// Create a new cookie HMAC signer
        /// </summary>
        private HMAC CreateNewCookieHMAC()
        {
            const string HMACProvider = "System.Security.Cryptography.HMACSHA1";
            HMAC hmac = HMAC.Create(HMACProvider);
            hmac.Key = this.currentHmacKey;
            return hmac;
        }

        /// <summary>
        /// Creates a new cryptographically secure random Hmac key
        /// </summary>
        private void SetHmacKey()
        {
            // MSDN recommends 64 bytes key for HMACSHA-1
            byte[] newKey = new byte[64];
            this.cryptoRandom.GetBytes(newKey);
            this.currentHmacKey = newKey;
        }
    }
}
