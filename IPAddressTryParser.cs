using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;

namespace SSMono.Net
	{
	public static class IPAddressTryParser
		{
		public static bool IPAddressTryParse (string ipAddressStr, out IPAddress ipAddress)
			{
			if (ipAddressStr != null && ipAddressStr.Length != 0 && (Char.IsDigit(ipAddressStr[0]) || ipAddressStr[0] == ':'))
				{
				try
					{
					ipAddress = IPAddress.Parse (ipAddressStr);
					return true;
					}
				catch (Exception)
					{
					}
				}

			ipAddress = null;
			return false;
			}
		}
	}