using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;

namespace SSMono.Net
	{
	public static class IPUtilities
		{
#if SSHARP
		/// <summary>
		/// Determines whether the specified <see cref="System.Net.IPAddress"/> represents
		/// a local IP address.
		/// </summary>
		/// <returns>
		/// <c>true</c> if <paramref name="address"/> represents a local IP address;
		/// otherwise, <c>false</c>.
		/// <remarks>
		/// This local means NOT REMOTE for the current host.
		/// </remarks>
		/// </returns>
		/// <param name="address">
		/// A <see cref="System.Net.IPAddress"/> to test.
		/// </param>
		public static bool IsLocal (this IPAddress address)
			{
			return address.IsLocal (EthernetAdapterType.EthernetUnknownAdapter);
			}

		/// <summary>
		/// Determines whether the specified <see cref="System.Net.IPAddress"/> represents
		/// a local IP address.
		/// </summary>
		/// <returns>
		/// <c>true</c> if <paramref name="address"/> represents a local IP address;
		/// otherwise, <c>false</c>.
		/// <remarks>
		/// This local means NOT REMOTE for the current host.
		/// </remarks>
		/// </returns>
		/// <param name="address">
		/// A <see cref="System.Net.IPAddress"/> to test.
		/// </param>
		/// <param name="adapter">adapter number to test against</param>
		/// <param name="adapterType">EthernetAdapterType to test against</param>
		public static bool IsLocal (this IPAddress address, EthernetAdapterType adapterType)
			{
			return
				address.IsLocal (adapterType == EthernetAdapterType.EthernetUnknownAdapter
											? (short)-1 : CrestronEthernetHelper.GetAdapterdIdForSpecifiedAdapterType (adapterType));
			}

		/// <summary>
		/// Determines whether the specified <see cref="System.Net.IPAddress"/> represents
		/// a local IP address.
		/// </summary>
		/// <returns>
		/// <c>true</c> if <paramref name="address"/> represents a local IP address;
		/// otherwise, <c>false</c>.
		/// <remarks>
		/// This local means NOT REMOTE for the current host.
		/// </remarks>
		/// </returns>
		/// <param name="address">
		/// A <see cref="System.Net.IPAddress"/> to test.
		/// </param>
		/// <param name="adapter">adapter number to test against</param>
		public static bool IsLocal (this IPAddress address, short adapter)
#endif
			{
			if (address == null)
				return false;

			if (address.Equals (IPAddress.Any))
				return true;

			if (address.Equals (IPAddress.Loopback))
				return true;

#if SSHARP
#else
			if (Socket.OSSupportsIPv6)
#endif
				{
				if (address.Equals (IPAddress.IPv6Any))
					return true;

				if (address.Equals (IPAddress.IPv6Loopback))
					return true;
				}

#if SSHARP
			if (adapter == -1)
				{
				var numAdapters = InitialParametersClass.NumberOfEthernetInterfaces;
				for (short ix = 0; ix < numAdapters; ++ix)
					{
					var addr = CrestronEthernetHelper.GetEthernetParameter (CrestronEthernetHelper.ETHERNET_PARAMETER_TO_GET.GET_CURRENT_IP_ADDRESS, ix);
					var ipAddr = IPAddress.Parse (addr);
					if (address.Equals (ipAddr))
						return true;
					}
				}
			else
				{
				var addr = CrestronEthernetHelper.GetEthernetParameter (CrestronEthernetHelper.ETHERNET_PARAMETER_TO_GET.GET_CURRENT_IP_ADDRESS, adapter);
				var ipAddr = IPAddress.Parse (addr);
				if (address.Equals (ipAddr))
					return true;
				}
#else
			var host = Dns.GetHostName ();
			var addrs = Dns.GetHostAddresses (host);
			foreach (var addr in addrs)
				{
				if (address.Equals (addr))
					return true;
				}
#endif

			return false;
			}

		public static bool IsIPv4Multicast (this IPAddress address)
			{
			if (address == null)
				throw new ArgumentNullException ("address");

			var msb = address.GetAddressBytes ()[0];
			return msb >= 224 && msb <= 239;
			}

		public static short GetAdapterIdForAddress (this IPAddress address)
			{
			if (address == null)
				throw new ArgumentNullException ("address");

			if (address.Equals (IPAddress.Broadcast) || address.Equals (IPAddress.Any) || address.Equals (IPAddress.Loopback) || address.Equals (IPAddress.None) || address.IsIPv4Multicast())
				return -1;

			var numAdapters = InitialParametersClass.NumberOfEthernetInterfaces;
			for (short ix = 0; ix < numAdapters; ++ix)
				{
				var addr = CrestronEthernetHelper.GetEthernetParameter (CrestronEthernetHelper.ETHERNET_PARAMETER_TO_GET.GET_CURRENT_IP_ADDRESS, ix);
				var mask = CrestronEthernetHelper.GetEthernetParameter (CrestronEthernetHelper.ETHERNET_PARAMETER_TO_GET.GET_CURRENT_IP_MASK, ix);
				var ipAddr = IPAddress.Parse (addr);
				var ipMask = IPAddress.Parse (mask);

				if (address.IsInSameSubnet (ipAddr, ipMask))
					return ix;
				}

			return CrestronEthernetHelper.GetAdapterdIdForSpecifiedAdapterType (EthernetAdapterType.EthernetLANAdapter);
			}

		public static EthernetAdapterType GetAdapterTypeForAddress (this IPAddress address)
			{
			if (address == null)
				throw new ArgumentNullException ("address");

			if (address.Equals (IPAddress.Broadcast) || address.Equals (IPAddress.Any) || address.Equals (IPAddress.Loopback) || address.Equals (IPAddress.None) || address.IsIPv4Multicast ())
				return EthernetAdapterType.EthernetUnknownAdapter;

			return CrestronEthernetHelper.GetAdapterTypeForSpecifiedId (address.GetAdapterIdForAddress ());
			}

		public static IPAddress GetBroadcastAddress (this IPAddress address, IPAddress subnetMask)
			{
			if (address == null)
				throw new ArgumentNullException ("address");

			byte[] ipAdressBytes = address.GetAddressBytes ();
			byte[] subnetMaskBytes = subnetMask.GetAddressBytes ();

			if (ipAdressBytes.Length != subnetMaskBytes.Length)
				throw new ArgumentException ("Lengths of IP address and subnet mask do not match.");

			byte[] broadcastAddress = new byte[ipAdressBytes.Length];
			for (int i = 0; i < broadcastAddress.Length; i++)
				broadcastAddress[i] = (byte)(ipAdressBytes[i] | (subnetMaskBytes[i] ^ 255));
			return new IPAddress (broadcastAddress);
			}

		public static IPAddress GetNetworkAddress (this IPAddress address, IPAddress subnetMask)
			{
			if (address == null)
				throw new ArgumentNullException ("address");

			byte[] ipAdressBytes = address.GetAddressBytes ();
			byte[] subnetMaskBytes = subnetMask.GetAddressBytes ();

			if (ipAdressBytes.Length != subnetMaskBytes.Length)
				throw new ArgumentException ("Lengths of IP address and subnet mask do not match.");

			byte[] broadcastAddress = new byte[ipAdressBytes.Length];
			for (int i = 0; i < broadcastAddress.Length; i++)
				broadcastAddress[i] = (byte)(ipAdressBytes[i] & (subnetMaskBytes[i]));
			return new IPAddress (broadcastAddress);
			}

		public static bool IsInSameSubnet (this IPAddress address2, IPAddress address, IPAddress subnetMask)
			{
			IPAddress network1 = address.GetNetworkAddress (subnetMask);
			IPAddress network2 = address2.GetNetworkAddress (subnetMask);

			return network1.Equals (network2);
			}
		}
	}
