//
// CrestronNetExtensions.cs
//
// Author:
//	Neil Colvin
//
// (C) 2019 Nivloc Enterprises Ltd.
//

//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//

using SSMono.Net.Sockets;
using Crestron.SimplSharp.CrestronSockets;

namespace Crestron.SimplSharp.CrestronSockets
	{
	public static class CrestronNetExtensions
		{
		public static SocketErrorCodes SendData (this UDPServer udpServer, byte[] pBufferToSend, int numBytesToSend, IPEndPoint ipEndPointToSendTo)
			{
			return udpServer.SendData (pBufferToSend, numBytesToSend, ipEndPointToSendTo.Address.ToString (), ipEndPointToSendTo.Port);
			}

		public static SocketErrorCodes SendDataAsync (this UDPServer udpServer, byte[] pBufferToSend, int numBytesToSend, IPEndPoint ipEndPointToSendTo, UDPServerSendCallback pFunctionCallback)
			{
			return udpServer.SendDataAsync (pBufferToSend, numBytesToSend, ipEndPointToSendTo.Address.ToString (), ipEndPointToSendTo.Port, pFunctionCallback);
			}

		public static SocketErrorCodes EnableUDPServer (this UDPServer udpServer, IPEndPoint ipEndPointToAcceptConnectionFrom)
			{
			return udpServer.EnableUDPServer (ipEndPointToAcceptConnectionFrom.Address.ToString (), ipEndPointToAcceptConnectionFrom.Port, ipEndPointToAcceptConnectionFrom.Port == 0 ? 65535 : ipEndPointToAcceptConnectionFrom.Port);
			}

		public static SocketErrorCodes EnableUDPServer (this UDPServer udpServer, IPEndPoint ipEndPointToAcceptConnectionFrom, int localPort)
			{
			return udpServer.EnableUDPServer (ipEndPointToAcceptConnectionFrom.Address.ToString (), localPort, ipEndPointToAcceptConnectionFrom.Port);
			}

		public static NetworkStream GetStream (this CrestronSocket cs)
			{
			return new NetworkStream (cs);
			}

		public static NetworkStream GetStream (this TCPClient tcp)
			{
			return new NetworkStream (new CrestronClientSocket (tcp), true);
			}

		public static NetworkStream GetStream (this TCPServer tcs, uint clientIndex)
			{
			return new NetworkStream (new CrestronServerSocket (new CrestronListenerSocket (tcs), clientIndex), true);
			}

		public static void Close (this TCPClient client)
			{
			client.DisconnectFromServer ();
			}
		}
	}