using System;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace Hazel.UPnP
{
    internal class NetUtility
    {
        private static IList<NetworkInterface> GetValidNetworkInterfaces()
        {
            var nics = NetworkInterface.GetAllNetworkInterfaces();
            if (nics == null || nics.Length < 1)
                return new NetworkInterface[0];

            var validInterfaces = new List<NetworkInterface>(nics.Length);

            NetworkInterface best = null;
            foreach (NetworkInterface adapter in nics)
            {
                if (adapter.NetworkInterfaceType == NetworkInterfaceType.Loopback || adapter.NetworkInterfaceType == NetworkInterfaceType.Unknown)
                    continue;
                if (!adapter.Supports(NetworkInterfaceComponent.IPv4) && !adapter.Supports(NetworkInterfaceComponent.IPv6))
                    continue;
                if (best == null)
                    best = adapter;
                if (adapter.OperationalStatus != OperationalStatus.Up)
                    continue;

                // make sure this adapter has any ip addresses
                IPInterfaceProperties properties = adapter.GetIPProperties();
                foreach (UnicastIPAddressInformation unicastAddress in properties.UnicastAddresses)
                {
                    if (unicastAddress != null && unicastAddress.Address != null)
                    {
                        // Yes it does, add this network interface.
                        validInterfaces.Add(adapter);
                        break;
                    }
                }
            }

            if (validInterfaces.Count == 0 && best != null)
                validInterfaces.Add(best);

            return validInterfaces;
        }

        /// <summary>
        /// Gets the addresses from all active network interfaces, but at most one per interface.
        /// </summary>
        /// <param name="addressFamily">The <see cref="AddressFamily"/> of the addresses to return</param>
        /// <returns>An <see cref="ICollection{T}"/> of <see cref="UnicastIPAddressInformation"/>.</returns>
        public static ICollection<UnicastIPAddressInformation> GetAddressesFromNetworkInterfaces(AddressFamily addressFamily)
        {
            var unicastAddresses = new List<UnicastIPAddressInformation>();

            foreach (NetworkInterface adapter in GetValidNetworkInterfaces())
            {
                IPInterfaceProperties properties = adapter.GetIPProperties();
                foreach (UnicastIPAddressInformation unicastAddress in properties.UnicastAddresses)
                {
                    if (unicastAddress != null && unicastAddress.Address != null && unicastAddress.Address.AddressFamily == addressFamily)
                    {
                        unicastAddresses.Add(unicastAddress);
                        break;
                    }
                }
            }

            return unicastAddresses;
        }

        /// <summary>
        /// Gets my local IPv4 address (not necessarily external) and subnet mask
        /// </summary>
        public static IPAddress GetMyAddress(out IPAddress mask)
        {
            var networkInterfaces = GetValidNetworkInterfaces();
            IPInterfaceProperties properties = null;

            if (networkInterfaces.Count > 0)
                properties = networkInterfaces[0]?.GetIPProperties();

            if (properties != null)
            {
                foreach (UnicastIPAddressInformation unicastAddress in properties.UnicastAddresses)
                {
                    if (unicastAddress != null && unicastAddress.Address != null && unicastAddress.Address.AddressFamily == AddressFamily.InterNetwork)
                    {
                        mask = unicastAddress.IPv4Mask;
                        return unicastAddress.Address;
                    }
                }
            }

            mask = null;
            return null;
        }

        /// <summary>
        /// Gets the broadcast address for the first network interface or, if not able to,
        /// the limited broadcast address.
        /// </summary>
        /// <returns>An <see cref="IPAddress"/> for broadcasting.</returns>
        public static IPAddress GetBroadcastAddress()
        {
            var networkInterfaces = GetValidNetworkInterfaces();
            IPInterfaceProperties properties = null;

            if (networkInterfaces.Count > 0)
                properties = networkInterfaces[0]?.GetIPProperties();

            if (properties != null)
            {
                foreach (UnicastIPAddressInformation unicastAddress in properties.UnicastAddresses)
                {
                    IPAddress ipAddress = GetBroadcastAddress(unicastAddress);
                    if (ipAddress != null)
                    {
                        return ipAddress;
                    }
                }
            }
            
            return IPAddress.Broadcast;
        }

        /// <summary>
        /// Gets the broadcast address for the given <paramref name="unicastAddress"/>.
        /// </summary>
        /// <param name="unicastAddress">A <see cref="UnicastIPAddressInformation"/></param>
        /// <returns>An <see cref="IPAddress"/> for broadcasting, null if the <paramref name="unicastAddress"/>
        /// is not an IPv4 address.</returns>
        public static IPAddress GetBroadcastAddress(UnicastIPAddressInformation unicastAddress)
        {
            if (unicastAddress != null && unicastAddress.Address != null && unicastAddress.Address.AddressFamily == AddressFamily.InterNetwork)
            {
                var mask = unicastAddress.IPv4Mask;
                byte[] ipAdressBytes = unicastAddress.Address.GetAddressBytes();
                byte[] subnetMaskBytes = mask.GetAddressBytes();

                if (ipAdressBytes.Length != subnetMaskBytes.Length)
                    throw new ArgumentException("Lengths of IP address and subnet mask do not match.");

                byte[] broadcastAddress = new byte[ipAdressBytes.Length];
                for (int i = 0; i < broadcastAddress.Length; i++)
                {
                    broadcastAddress[i] = (byte)(ipAdressBytes[i] | (subnetMaskBytes[i] ^ 255));
                }
                return new IPAddress(broadcastAddress);
            }

            return null;
        }
    }
}
