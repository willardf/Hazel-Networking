using System;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace Hazel.UPnP
{
    internal class NetUtility
    {
        private static NetworkInterface GetNetworkInterface()
        {
            var computerProperties = IPGlobalProperties.GetIPGlobalProperties();
            if (computerProperties == null)
                return null;

            var nics = NetworkInterface.GetAllNetworkInterfaces();
            if (nics == null || nics.Length < 1)
                return null;

            NetworkInterface best = null;
            foreach (NetworkInterface adapter in nics)
            {
                if (adapter.NetworkInterfaceType == NetworkInterfaceType.Loopback || adapter.NetworkInterfaceType == NetworkInterfaceType.Unknown)
                    continue;
                if (!adapter.Supports(NetworkInterfaceComponent.IPv4))
                    continue;
                if (best == null)
                    best = adapter;
                if (adapter.OperationalStatus != OperationalStatus.Up)
                    continue;

                // make sure this adapter has any ipv4 addresses
                IPInterfaceProperties properties = adapter.GetIPProperties();
                foreach (UnicastIPAddressInformation unicastAddress in properties.UnicastAddresses)
                {
                    if (unicastAddress != null && unicastAddress.Address != null && unicastAddress.Address.AddressFamily == AddressFamily.InterNetwork)
                    {
                        // Yes it does, return this network interface.
                        return adapter;
                    }
                }
            }
            return best;
        }

        /// <summary>
		/// Gets my local IPv4 address (not necessarily external) and subnet mask
		/// </summary>
		public static IPAddress GetMyAddress(out IPAddress mask)
        {
            var ni = GetNetworkInterface();
            if (ni == null)
            {
                mask = null;
                return null;
            }

            IPInterfaceProperties properties = ni.GetIPProperties();
            foreach (UnicastIPAddressInformation unicastAddress in properties.UnicastAddresses)
            {
                if (unicastAddress != null && unicastAddress.Address != null && unicastAddress.Address.AddressFamily == AddressFamily.InterNetwork)
                {
                    mask = unicastAddress.IPv4Mask;
                    return unicastAddress.Address;
                }
            }

            mask = null;
            return null;
        }

        public static IPAddress GetBroadcastAddress()
        {
            var ni = GetNetworkInterface();
            if (ni == null)
                return null;

            var properties = ni.GetIPProperties();
            foreach (UnicastIPAddressInformation unicastAddress in properties.UnicastAddresses)
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
            }
            return IPAddress.Broadcast;
        }
    }
}