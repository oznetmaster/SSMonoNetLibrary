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