using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;
using SSMono.Net;
using SSMono.Net.Sockets;
using Socket = Crestron.SimplSharp.CrestronSockets.CrestronClientSocket;
using IAsyncResult = Crestron.SimplSharp.CrestronIO.IAsyncResult;
using AsyncCallback = Crestron.SimplSharp.CrestronIO.AsyncCallback;
using SSCore.Diagnostics;

namespace Crestron.SimplSharp.CrestronSockets
	{
	using SocketException = SSMono.Net.Sockets.SocketException;

	public class CrestronUdpSocket : CrestronConnectableSocket
		{
		protected UDPServer _client;
		protected bool _messageReceived;
		protected IPAddress _multicastGroup;
		private string _savedLocalAddress;
		private readonly object _lockUDP = new object ();
		private bool _connected;
		private int _activeCount;
		private readonly ManualResetEvent _activeEvent = new ManualResetEvent (false);
		private AddressFamily _addressFamily = AddressFamily.Unknown;

		public CrestronUdpSocket (UDPServer client)
			{
			_client = client;
			}

		public CrestronUdpSocket ()
			: this (AddressFamily.InterNetwork, EthernetAdapterType.EthernetUnknownAdapter)
			{
			}

		public CrestronUdpSocket (EthernetAdapterType adapterType)
			: this (AddressFamily.InterNetwork, adapterType)
			{
			}

		public CrestronUdpSocket (EthernetAdapterType adapterType, int bufferSize)
			: this (AddressFamily.InterNetwork, adapterType, bufferSize)
			{
			}

		public CrestronUdpSocket (AddressFamily family, SocketType socketType, ProtocolType protocolType)
			: this (family, socketType, protocolType, 4096)
			{
			}

		public CrestronUdpSocket (AddressFamily family, SocketType socketType, ProtocolType protocolType, int bufferSize)
			: this (family, EthernetAdapterType.EthernetUnknownAdapter, bufferSize)
			{
			if (socketType != SocketType.Dgram)
				throw new ArgumentException ("Only Dgram allowed.", "socketType");
			if (protocolType != ProtocolType.Udp)
				throw new ArgumentException ("Only Udp allowed", "protocolType");
			}

		public CrestronUdpSocket (AddressFamily family, EthernetAdapterType adapterType)
			: this (family, adapterType, 4096)
			{
			}

		public CrestronUdpSocket (AddressFamily family, EthernetAdapterType adapterType, int bufferSize)
			{
			Debug.WriteLine ("UdpSocket Create ({1}, {0}, {2})", adapterType, family, bufferSize);

			_client = new UDPServer (IPAddress.Any, 0, bufferSize, adapterType)
				{
					RemotePortNumber = 65535,
				};

			_addressFamily = family;
			}

		public CrestronUdpSocket (IPAddress ipAddress, int port, EthernetAdapterType adapterType)
			: this (ipAddress, port, 4096, adapterType)
			{

			}

		public CrestronUdpSocket (IPAddress ipAddress, int port, int bufferSize, EthernetAdapterType adapterType)
			{
			Debug.WriteLine ("UdpSocket Create ({0}:{1}, {3}, {2})", ipAddress, port, adapterType, bufferSize);

			if (ipAddress == null)
				throw new ArgumentNullException ("ipaddress");

			if (port < IPEndPoint.MinPort || port > IPEndPoint.MaxPort)
				throw new ArgumentOutOfRangeException ("port");

			_addressFamily = ipAddress.AddressFamily;

			_client = new UDPServer (ipAddress, port, bufferSize, adapterType)
			{
				RemotePortNumber = 65535,
			};
			}

		public CrestronUdpSocket (IPAddress ipAddress, int port, int bufferSize)
			: this (ipAddress, port, bufferSize, ipAddress == null ? EthernetAdapterType.EthernetUnknownAdapter : ipAddress.GetAdapterTypeForAddress ())
			{
			}

		public CrestronUdpSocket (IPAddress ipAddress, int port)
			: this (ipAddress, port, ipAddress == null ? EthernetAdapterType.EthernetUnknownAdapter : ipAddress.GetAdapterTypeForAddress ())
			{
			}

		public CrestronUdpSocket (string host, int port)
			: this (host, port, EthernetAdapterType.EthernetUnknownAdapter)
			{
			}

		public CrestronUdpSocket (string host, int port, int bufferSize)
			: this (host, port, bufferSize, EthernetAdapterType.EthernetUnknownAdapter)
			{
			}

		public CrestronUdpSocket (int port)
			: this (port, AddressFamily.InterNetwork)
			{
			}

		public CrestronUdpSocket (int port, int bufferSize)
			: this (port, AddressFamily.InterNetwork, bufferSize)
			{
			}

		public CrestronUdpSocket (int port, AddressFamily family)
			: this (port, family, 4096)
			{
			}

		public CrestronUdpSocket (int port, AddressFamily family, int bufferSize)
			{
			if (port < IPEndPoint.MinPort || port > IPEndPoint.MaxPort)
				throw new ArgumentOutOfRangeException ("port");

			_client = new UDPServer (IPAddress.Any, port, bufferSize)
			{
				RemotePortNumber = 65535,
			};

			_addressFamily = family;
			}

		public CrestronUdpSocket (IPEndPoint endPoint)
			: this (endPoint, endPoint == null ? EthernetAdapterType.EthernetUnknownAdapter : endPoint.Address.GetAdapterTypeForAddress ())
			{
			}

		public CrestronUdpSocket (IPEndPoint endPoint, int bufferSize)
			: this (endPoint, bufferSize, endPoint == null ? EthernetAdapterType.EthernetUnknownAdapter : endPoint.Address.GetAdapterTypeForAddress ())
			{
			}

		public CrestronUdpSocket (IPEndPoint endPoint, EthernetAdapterType adapterType)
			: this (endPoint, 4096, adapterType)
			{
			}

		public CrestronUdpSocket (IPEndPoint endPoint, int bufferSize, EthernetAdapterType adapterType)
			{
			Debug.WriteLine ("UdpSocket Create ({0}, {2}, {1})", endPoint, adapterType, bufferSize);

			if (endPoint == null)
				throw new ArgumentNullException ("endPoint");

			if (endPoint.Port < IPEndPoint.MinPort || endPoint.Port > IPEndPoint.MaxPort)
				throw new ArgumentOutOfRangeException ("endPoint");

			_addressFamily = endPoint.Address.AddressFamily;

			_client = new UDPServer (endPoint.Address, endPoint.Port, bufferSize, adapterType)
				{
					RemotePortNumber = 65535,
				};
			}

		public CrestronUdpSocket (string host, int port, EthernetAdapterType adapterType)
			: this (host, port, 4096, adapterType)
			{
			}

		public CrestronUdpSocket (string host, int port, int bufferSize, EthernetAdapterType adapterType)
			{
			Debug.WriteLine ("UdpSocket Create and Connect ({0}:{1}, {3}, {2})", host, port, adapterType, bufferSize);

			if (host == null)
				throw new ArgumentNullException ("host");

			if (port < IPEndPoint.MinPort || port > IPEndPoint.MaxPort)
				throw new ArgumentOutOfRangeException ("port");

			var ipaddresses = DnsEx.GetHostAddresses (host);
			if (ipaddresses.Length == 0)
				throw new SocketException (SocketError.HostNotFound);

			var ipaddress = ipaddresses[0];
			_addressFamily = ipaddress.AddressFamily;

			_client = new UDPServer (ipaddress, port, bufferSize, adapterType);

			Connect (ipaddress, port);
			}

		private SocketErrorCodes EnableUDPServer (UDPServer client)
			{
			Debug.WriteLine ("UdpSocket ({1}, {2}): EnableUDPServer ({0}, [remoteEndpoint = {1}])", "client", InternalLocalEndPoint, AdapterType, InternalRemoteEndPoint);

			lock (_lockUDP)
				return client.EnableUDPServer ();
			}

#if false
		private SocketErrorCodes EnableUDPServer (UDPServer client, int remotePort)
			{
			Debug.WriteLine ("UdpSocket ({1}, {2}): EnableUDPServer ({0}, {1})", "client", EndpointToAcceptConnectionFrom, AdapterType, remotePort);

			lock (_lockUDP)
				{
				client.RemotePortNumber = remotePort;

				return client.EnableUDPServer ();
				}
			}

		private SocketErrorCodes EnableUDPServer (UDPServer client, IPEndPoint endPoint)
			{
			Debug.WriteLine ("UdpSocket ({1}, {2}): EnableUDPServer ({0}, {3})", "client", EndpointToAcceptConnectionFrom, AdapterType, endPoint);

			lock (_lockUDP)
				return client.EnableUDPServer (endPoint);
			}

		private SocketErrorCodes EnableUDPServer (UDPServer client, IPAddress address, int localAndRemotePort)
			{
			Debug.WriteLine ("UdpSocket ({1}, {2}): EnableUDPServer ({0}, {3}, {4})", "client", EndpointToAcceptConnectionFrom, AdapterType, address, localAndRemotePort);

			lock (_lockUDP)
				return client.EnableUDPServer (address, localAndRemotePort);
			}
#endif

		private SocketErrorCodes EnableUDPServer (UDPServer client, IPAddress address, int localPort, int remotePort)
			{
			Debug.WriteLine ("UdpSocket ({1}, {2}): EnableUDPServer ({0}, {3}, {4}, {5})", "client", InternalLocalEndPoint, AdapterType, address, localPort, remotePort);

			lock (_lockUDP)
				return client.EnableUDPServer (address, localPort, remotePort);
			}

		private SocketErrorCodes DisableUDPServer (UDPServer client)
			{
			Debug.WriteLine ("UdpSocket ({1}, {2}): DisableUDPServer ({0})", "client", InternalLocalEndPoint, AdapterType);

			lock (_lockUDP)
				return client.DisableUDPServer ();
			}

		public override void Bind (IPEndPoint endPoint)
			{
			var client = _client;

			CheckDisposed ();

			if (endPoint == null)
				throw new ArgumentNullException ("endPoint");

			client.PortNumber = endPoint.Port;

			if (client.EthernetAdapterToBindTo == EthernetAdapterType.EthernetUnknownAdapter)
				client.EthernetAdapterToBindTo = endPoint.Address.GetAdapterTypeForAddress ();

			Debug.WriteLine ("UdpSocket Bind ({0}, {1})", endPoint, client.EthernetAdapterToBindTo);
			}

		public void JoinMulticastGroup (IPAddress multicastAddr)
			{
			Debug.WriteLine ("UdpSocket ({1}, {2}): JoinMulticastGroup ({0})", multicastAddr, InternalLocalEndPoint, AdapterType);

			var client = _client;

			CheckDisposed ();

			if (multicastAddr == null)
				throw new ArgumentNullException ("multicastAddr");

			if (multicastAddr.AddressFamily != _addressFamily)
				throw new ArgumentException ("incorrect AddressFamily", "multicastAddr");

			if (_connected || _active)
				throw new SocketException (SocketError.IsConnected);

			if (client.PortNumber == 65535 || client.PortNumber == 0)
				throw new SocketException (SocketError.InvalidArgument, "port must be bound before joining multicast group");

			_multicastGroup = multicastAddr;

			_savedLocalAddress = client.AddressToAcceptConnectionFrom;
			client.AddressToAcceptConnectionFrom = _multicastGroup.ToString ();
			}

		public void DropMulticastGroup (IPAddress multicastAddr)
			{
			Debug.WriteLine ("UdpSocket ({1}, {2}): DropMulticastGroup ({0})", multicastAddr, InternalLocalEndPoint, AdapterType);

			var client = _client;

			CheckDisposed ();

			if (multicastAddr == null)
				throw new ArgumentNullException ("multicastAddr");

			if (multicastAddr.AddressFamily != _addressFamily)
				throw new ArgumentException ("incorrect AddressFamily", "multicastAddr");

			if (!multicastAddr.Equals (_multicastGroup))
				return;

			_multicastGroup = null;

			if (_active)
				{
				while (Interlocked.Decrement (ref _activeCount) != 0)
					{
					Interlocked.Increment (ref _activeCount);
					CrestronEnvironment.Sleep (1);
					}

				if (_active)
					{
					DisableUDPServer (client);

					_activeCount = 0;
					_active = false;
					_activeEvent.Reset ();
					}
				}

			client.AddressToAcceptConnectionFrom = _savedLocalAddress;
			}

		public override void Close ()
			{
			Debug.WriteLine (String.Format ("UdpSocket ({0}, {2}): Close [disposed: {1}]", InternalLocalEndPoint, _disposed, _client == null ? "<unknown>" : _client.EthernetAdapterToBindTo.ToString ()));

			base.Close ();
			}

		public bool Active
			{
			get
				{
				CheckDisposed ();

				return _connected;
				}
			}

		public override bool Connected
			{
			get
				{
				return _connected || _active;
				}
			}

		public override bool DataAvailable
			{
			get
				{
				CheckDisposed ();

				var client = _client;

				return client.DataAvailable;
				}
			}

		private IPEndPoint _remoteEndPoint;
		private string _lastAddressToAcceptConnectionFrom;
		public override IPEndPoint RemoteEndPoint
			{
			get
				{
				CheckDisposed ();

				return InternalRemoteEndPoint;
				}
			}

		internal override IPEndPoint InternalRemoteEndPoint
			{
			get
				{
				var client = _client;

				if (_disposed != 0 || client == null || !_connected || String.IsNullOrEmpty (client.AddressToAcceptConnectionFrom))
					return new IPEndPoint (IPAddress.None, 0);

				if (_remoteEndPoint != null && client.AddressToAcceptConnectionFrom.Equals (_lastAddressToAcceptConnectionFrom, StringComparison.InvariantCultureIgnoreCase))
					return _remoteEndPoint;

				_lastAddressToAcceptConnectionFrom = client.AddressToAcceptConnectionFrom;

				var ipaddrs = DnsEx.GetHostAddresses (client.AddressToAcceptConnectionFrom);
				return _remoteEndPoint = ipaddrs.Length == 0 ? new IPEndPoint (IPAddress.None, client.PortNumber) : new IPEndPoint (ipaddrs[0], client.RemotePortNumber);
				}
			}

		private IPEndPoint LastReceivedFromEndPoint
			{
			get
				{
				var client = _client;

				return (client == null || !_messageReceived || String.IsNullOrEmpty (client.IPAddressLastMessageReceivedFrom) ? null : new IPEndPoint (IPAddress.Parse (client.IPAddressLastMessageReceivedFrom), client.IPPortLastMessageReceivedFrom));
				}
			}

		private IPEndPoint _localEndpoint;
		public override IPEndPoint LocalEndPoint
			{
			get
				{
				CheckDisposed ();

				return InternalLocalEndPoint;
				}
			}

		protected IPEndPoint InternalLocalEndPoint
			{
			get
				{
				var client = _client;

				return _localEndpoint ?? (client == null ? null : String.IsNullOrEmpty (client.LocalAddressOfServer) ? new IPEndPoint(IPAddress.Any, client.PortNumber) : (_localEndpoint = new IPEndPoint (IPAddress.Parse (client.LocalAddressOfServer), client.PortNumber)));
				}
			}

		private IPEndPoint EndpointToAcceptConnectionFrom
			{
			get
				{
				var client = _client;

				return (client == null || String.IsNullOrEmpty (client.AddressToAcceptConnectionFrom) ? null : new IPEndPoint (IPAddress.Parse (client.AddressToAcceptConnectionFrom), client.RemotePortNumber));
				}
			}

		public EthernetAdapterType AdapterType
			{
			get
				{
				var client = _client;

				return client == null ? EthernetAdapterType.EthernetUnknownAdapter : client.EthernetAdapterToBindTo;
				}
			}

		public override ProtocolType ProtocolType
			{
			get
				{
				return ProtocolType.Udp;
				}
			}

		public override SocketType SocketType
			{
			get
				{
				return SocketType.Dgram;
				}
			}

		public override AddressFamily AddressFamily
			{
			get
				{
				return _addressFamily;
				}
			}

		public UDPServer Client
			{
			get { return _client; }
			}

		public override void Connect (IPEndPoint remoteEP)
			{
			Debug.WriteLine ("UdpSocket ({1}, {2}): Connect ({0})", remoteEP, InternalLocalEndPoint, AdapterType);

			CheckDisposed ();

			if (remoteEP == null)
				throw new ArgumentNullException ("remoteEP");

			var client = _client;

			if (_multicastGroup != null)
				throw new SocketException (SocketError.OperationNotSupported);

			if (_connected)
				{
				DisableUDPServer (client);
				_activeEvent.Reset ();
				_active = false;
				_connected = false;
				_activeCount = 0;
				}

			var result = EnableUDPServer (client, remoteEP.Address, client.PortNumber, remoteEP.Port);

			if (result != SocketErrorCodes.SOCKET_OK)
				throw new SocketException (result.ToError ());

			_active = true;
			_activeCount = 1;
			_connected = true;
			_activeEvent.Set ();

			Debug.WriteLine ("     UdpSocket Connected ({0}) [localEndpoint = {1}]", remoteEP, InternalLocalEndPoint);
			}

		public override void Connect (IPAddress[] addresses, int port)
			{
			CheckDisposed ();

			if (addresses == null)
				throw new ArgumentNullException ("addresses");

			if (addresses.Length != 1)
				throw new ArgumentOutOfRangeException ("addresses", "only single address allowed");

			base.Connect (addresses, port);
			}

		public override int Send (byte[] buffer, int offset, int size, SocketFlags socketFlags)
			{
			Debug.WriteLine (String.Format ("UdpSocket ({2}, {4}): Send [offset = {1}, size = {0}, remoteEndpoint = {3}, multicastGroup = {5}]", size, offset, InternalLocalEndPoint, InternalRemoteEndPoint, AdapterType, _multicastGroup));

			var client = _client;

			CheckDisposed ();

			if (_shutdown.HasValue && (_shutdown == SocketShutdown.Send || _shutdown == SocketShutdown.Both))
				throw new SocketException (SocketError.Shutdown);

			if (buffer == null)
				throw new ArgumentNullException ("buffer");

			if (offset < 0 || offset > buffer.Length)
				throw new ArgumentOutOfRangeException ("offset");

			if (size < 0 || size > buffer.Length - offset)
				throw new ArgumentOutOfRangeException ("size");

			if (!_connected)
				throw new SocketException (SocketError.NotConnected);

			if (size == 0)
				return 0;

			client.SocketSendOrReceiveTimeOutInMs = SendTimeout;

			SocketErrorCodes result;
			if (offset == 0)
				result = client.SendData (buffer, size);
			else
				{
				var data = new byte[size];
				Buffer.BlockCopy (buffer, offset, data, 0, size);
				result = client.SendData (data, size);
				}

			client.SocketSendOrReceiveTimeOutInMs = 0;

			if (result != SocketErrorCodes.SOCKET_OK)
				throw new SocketException (result.ToError ());

			return size;
			}

		private void EnableMulticastSendUDPServer (UDPServer client, IPEndPoint remoteEndpoint)
			{
			Debug.WriteLine ("UdpSocket ({3}, {4}): EnableMulticastSendUDPServer [_activeCount = {0}, remoteEndPoint = {1}, multicastGroup = {2}]", _activeCount, remoteEndpoint, _multicastGroup, InternalLocalEndPoint, AdapterType);

			if (Interlocked.Increment (ref _activeCount) != 1)
				{
				_activeEvent.Wait ();

				return;
				}

			//SocketErrorCodes error = _multicastGroup == null ? (remoteEndpoint.Address.IsIPv4Multicast () ? EnableUDPServer (client, remoteEndpoint.Address, client.PortNumber, remoteEndpoint.Port) : EnableUDPServer (client)) : EnableUDPServer (client);
			//SocketErrorCodes error = _multicastGroup == null ? EnableUDPServer (client) : EnableUDPServer (client, _multicastGroup, remoteEndpoint.Port);
			SocketErrorCodes error = _multicastGroup == null ? EnableUDPServer (client, remoteEndpoint.Address, client.PortNumber, remoteEndpoint.Port) : EnableUDPServer (client);

			if (error != SocketErrorCodes.SOCKET_OK)
				throw new SocketException (error.ToError ());

			if (_multicastGroup != null)
				Interlocked.Increment (ref _activeCount);
			_active = true;
			_activeEvent.Set ();
			}

#if false
		private void EnableMulticastReceiveUDPServer (UDPServer client, IPEndPoint remoteEndpoint)
			{
			Debug.WriteLine ("UdpSocket ({3}, {4}): EnableMulticastReceiveUDPServer [_activeCount = {0}, remoteEndPoint = {1}, multicastGroup = {2}]", _activeCount, remoteEndpoint, _multicastGroup, EndpointToAcceptConnectionFrom, AdapterType);

			if (Interlocked.Increment (ref _activeCount) != 1)
				{
				_activeEvent.Wait ();

				return;
				}

			if (client.PortNumber == 0)
				if (_multicastGroup == null)
					client.PortNumber = remoteEndpoint.Port;
				else
					throw new InvalidOperationException ("multicast client must be bound to a port");

			if (_multicastGroup == null)
				{
				if (client.RemotePortNumber == 65535)
					client.RemotePortNumber = remoteEndpoint.Port;

				client.AddressToAcceptConnectionFrom = remoteEndpoint.Address.ToString ();
				}

			SocketErrorCodes error = EnableUDPServer (client);

			if (error != SocketErrorCodes.SOCKET_OK)
				throw new SocketException (error.ToError ());

			if (_multicastGroup != null)
				Interlocked.Increment (ref _activeCount);
			_active = true;
			_activeEvent.Set ();
			}
#endif

		private void EnableMulticastReceiveUDPServer (UDPServer client)
			{
			Debug.WriteLine ("UdpSocket ({2}, {3}): EnableMulticastReceiveUDPServer [_activeCount = {0}, multicastGroup = {1}]", _activeCount, _multicastGroup, InternalLocalEndPoint, AdapterType);

			if (Interlocked.Increment (ref _activeCount) != 1)
				{
				_activeEvent.Wait ();

				return;
				}
			if (String.IsNullOrEmpty (client.AddressToAcceptConnectionFrom))
				client.AddressToAcceptConnectionFrom = IPAddress.Any.ToString ();

			SocketErrorCodes error = EnableUDPServer (client);

			if (error != SocketErrorCodes.SOCKET_OK)
				throw new SocketException (error.ToError ());

			if (_multicastGroup != null)
				Interlocked.Increment (ref _activeCount);
			_active = true;
			_activeEvent.Set ();
			}

		private void DisableMulticastSendUDPServer (UDPServer client)
			{
			Debug.WriteLine ("UdpSocket ({1}, {2}): DisableMulticastSendUDPServer [_activeCount = {0}, multicastGroup = {3}]", _activeCount, InternalLocalEndPoint, AdapterType, _multicastGroup);

			if (Interlocked.Decrement (ref _activeCount) != 0)
				return;

			DisableUDPServer (client);

			_activeEvent.Reset ();
			_active = false;
			}

		private void DisableMulticastReceiveUDPServer (UDPServer client)
			{
			Debug.WriteLine ("UdpSocket ({1}, {2}): DisableMulticastReceiveUDPServer [_activeCount = {0}, multicastGroup = {3}]", _activeCount, InternalLocalEndPoint, AdapterType, _multicastGroup);

			if (Interlocked.Decrement (ref _activeCount) != 0)
				return;

			DisableUDPServer (client);

			_activeEvent.Reset ();
			_active = false;
			}

		public int Send (byte[] buffer, int size, string hostname, int port)
			{
			return Send (buffer, 0, size, hostname, port, true);
			}

		public int Send (byte[] buffer, int offset, int size, string hostname, int port)
			{
			return Send (buffer, offset, size, hostname, port, true);
			}

		private int Send (byte[] buffer, int offset, int size, string hostname, int port, bool throwIfConnected)
			{
			Debug.WriteLine (String.Format ("UdpSocket ({6}, {4}): Send [size = {0}, endpoint = {1}, hostname = \"{2}\", port = {3}, multicastGroup = {5}]", size, InternalLocalEndPoint, hostname, port, AdapterType, _multicastGroup, InternalLocalEndPoint));

			var client = _client;

			CheckDisposed ();

			if (_shutdown.HasValue && (_shutdown == SocketShutdown.Send || _shutdown == SocketShutdown.Both))
				throw new SocketException (SocketError.Shutdown);

			if (buffer == null)
				throw new ArgumentNullException ("buffer");

			if (size < 0 || size > buffer.Length)
				throw new ArgumentOutOfRangeException ("size");

			if (size == 0)
				return 0;

			if (_connected && throwIfConnected)
				throw new SocketException (SocketError.IsConnected, "default remote host already established");

			var remoteEndpoint = new IPEndPoint (DnsEx.GetHostAddresses (hostname)[0], port);

			EnableMulticastSendUDPServer (client, remoteEndpoint);

			client.SocketSendOrReceiveTimeOutInMs = SendTimeout;

			SocketErrorCodes result;
			if (offset == 0)
				result = client.SendData (buffer, size, remoteEndpoint);
			else
				{
				var data = new byte[size];
				Buffer.BlockCopy (buffer, offset, data, 0, size);
				result = client.SendData (data, size, remoteEndpoint);
				}

			client.SocketSendOrReceiveTimeOutInMs = 0;

			DisableMulticastSendUDPServer (client);

			if (result != SocketErrorCodes.SOCKET_OK)
				throw new SocketException (result.ToError ());

			return size;
			}

		public int Send (byte[] buffer, int size)
			{
			return Send (buffer, 0, size, SocketFlags.None);
			}

		public int Send (byte[] buffer, int size, IPEndPoint endPoint)
			{
			return Send (buffer, 0, size, endPoint, true);
			}

		public int Send (byte[] buffer, int offset, int size, IPEndPoint endPoint)
			{
			return Send (buffer, offset, size, endPoint, true);
			}

		private int Send (byte[] buffer, int offset, int size, IPEndPoint endPoint, bool throwOnConnected)
			{
			Debug.WriteLine (String.Format ("UdpSocket ({1}, {3}): Send [size = {0}, endpoint = {2}, multicastGroup = {4}, throwOnConnected = {5}]", size, InternalLocalEndPoint, endPoint, AdapterType, _multicastGroup, throwOnConnected));

			if (endPoint == null)
				return Send (buffer, offset, size, SocketFlags.None);

			var client = _client;

			CheckDisposed ();

			if (_shutdown.HasValue && (_shutdown == SocketShutdown.Send || _shutdown == SocketShutdown.Both))
				throw new SocketException (SocketError.Shutdown);

			if (buffer == null)
				throw new ArgumentNullException ("buffer");

			if (size < 0 || size > buffer.Length)
				throw new ArgumentOutOfRangeException ("size");

			if (_connected && throwOnConnected)
				throw new SocketException (SocketError.IsConnected, "default remote host already established");

			if (size == 0)
				return 0;

			EnableMulticastSendUDPServer (client, endPoint);

			client.SocketSendOrReceiveTimeOutInMs = SendTimeout;

			SocketErrorCodes result;
			if (offset == 0)
				result = client.SendData (buffer, size, endPoint);
			else
				{
				var data = new byte[size];
				Buffer.BlockCopy (buffer, offset, data, 0, size);
				result = client.SendData (data, size, endPoint);
				}

			client.SocketSendOrReceiveTimeOutInMs = 0;

			DisableMulticastSendUDPServer (client);

			if (result != SocketErrorCodes.SOCKET_OK)
				throw new SocketException (result.ToError ());

			return size;
			}

		public override int SendTo (byte[] buffer, int offset, int size, SocketFlags flags, IPEndPoint endPoint)
			{
			return Send (buffer, offset, size, endPoint, false);
			}

		public override int Receive (byte[] buffer, int offset, int size, SocketFlags socketFlags)
			{
			Debug.WriteLine (string.Format ("UdpSocket ({2}, {4}): Receive [offset = {1}, size = {0}, remoteEndpoint = {3}, multicastGroup = {4}]", size, offset, InternalLocalEndPoint, InternalRemoteEndPoint, AdapterType, _multicastGroup));

			var client = _client;

			CheckDisposed ();

			if (buffer == null)
				throw new ArgumentNullException ("buffer");

			if (offset < 0 || offset > buffer.Length)
				throw new ArgumentOutOfRangeException ("offset");

			if (size < 0 || size > buffer.Length - offset)
				throw new ArgumentOutOfRangeException ("size");

			if (_shutdown.HasValue && (_shutdown == SocketShutdown.Receive || _shutdown == SocketShutdown.Both))
				throw new SocketException (SocketError.Shutdown);

			if (_connected)
				{
				if (_multicastGroup != null)
					throw new SocketException (SocketError.IsConnected);
				}
			else
				{
				if (_multicastGroup == null)
					throw new SocketException (SocketError.NotConnected);

				EnableMulticastReceiveUDPServer (client);
				}

			if (size == 0)
				return 0;

			client.SocketSendOrReceiveTimeOutInMs = ReceiveTimeout;

			int length = client.ReceiveData ();

			client.SocketSendOrReceiveTimeOutInMs = 0;

			if (!_connected)
				DisableMulticastReceiveUDPServer (client);

			if (length == 0)
				{
				Debug.WriteLine ("UdpSocket ({0}, {1}): Receive [length = 0]", InternalLocalEndPoint, AdapterType);

				if (client.ServerStatus == SocketStatus.SOCKET_STATUS_NO_CONNECT)
					{
					if (_connected)
						{
						var result = client.EnableUDPServer ();

						if (result != SocketErrorCodes.SOCKET_OK)
							throw new SocketException (result.ToError ());
						}

					throw new SocketException (SocketError.TimedOut);
					}

				((IDisposable)this).Dispose ();

				return 0;
				}

			_messageReceived = true;

			int retLength = Math.Min (buffer.Length, length);

			Buffer.BlockCopy (client.IncomingDataBuffer, 0, buffer, offset, retLength);

			Debug.WriteLine (String.Format ("UdpSocket ({1}, {3}): Receive [length = {0}, remoteEndpoint = {2}]", retLength, InternalLocalEndPoint, LastReceivedFromEndPoint, AdapterType));

			if (retLength < length)
				throw new SocketException (SocketError.MessageSize);

			return retLength;
			}

		public byte[] Receive (ref IPEndPoint remoteEndpoint)
			{
			Debug.WriteLine (string.Format ("UdpSocket ({0}, {2}): Receive [remoteEndpoint = {1}, multicastGroup = {3}]", InternalLocalEndPoint, remoteEndpoint, AdapterType, _multicastGroup));

			var client = _client;

			CheckDisposed ();

			if (_shutdown.HasValue && (_shutdown == SocketShutdown.Receive || _shutdown == SocketShutdown.Both))
				throw new SocketException (SocketError.Shutdown);

			if (_connected)
				{
				if (_multicastGroup != null)
					throw new SocketException (SocketError.IsConnected);
				}
			else
				{
				if (_multicastGroup == null)
					throw new SocketException (SocketError.NotConnected);

				client.RemotePortNumber = remoteEndpoint.Port;
				EnableMulticastReceiveUDPServer (client);
				}

			client.SocketSendOrReceiveTimeOutInMs = ReceiveTimeout;

			int length = _client.ReceiveData ();

			if (!_connected)
				DisableMulticastReceiveUDPServer (client);

			client.SocketSendOrReceiveTimeOutInMs = 0;

			if (length != 0)
				{
				remoteEndpoint = LastReceivedFromEndPoint;

				Debug.WriteLine (String.Format ("UdpSocket ({1}, {3}): Receive [length = {0}, remoteEndpoint = {2}]", length, EndpointToAcceptConnectionFrom, LastReceivedFromEndPoint, AdapterType));

				_messageReceived = true;
				}
			else
				Debug.WriteLine ("UdpSocket ({0}, {1}): Receive [length = 0]", InternalLocalEndPoint, AdapterType);

			var ret = client.IncomingDataBuffer.Take (length).ToArray ();

			return ret;
			}

		public int ReceiveFrom (byte[] buffer, ref IPEndPoint endPoint)
			{
			var buffIn = Receive (ref endPoint);

			var copyLength = Math.Min (buffIn.Length, buffer.Length);

			Buffer.BlockCopy (buffIn, 0, buffer, 0, copyLength);

			return copyLength;
			}

		private class AsyncSendState
			{
			public readonly SocketClientSendAsyncResult AsyncResult;
			public readonly AsyncCallback AsyncCallback;

			public AsyncSendState (SocketClientSendAsyncResult asyncResult, AsyncCallback asyncCallback)
				{
				AsyncResult = asyncResult;
				AsyncCallback = asyncCallback;
				}
			}

		public override IAsyncResult BeginSend (byte[] buffer, int offset, int size, SocketFlags socketFlags, AsyncCallback callback, Object state)
			{
			Debug.WriteLine ("UdpSocket ({0}, {1}): BeginSend [size = {3}, remoteEndpoint = {2}, multicastGroup = {4}]", InternalLocalEndPoint, AdapterType, InternalRemoteEndPoint, size, _multicastGroup);

			var client = _client;

			CheckDisposed ();

			if (_shutdown.HasValue && (_shutdown == SocketShutdown.Send || _shutdown == SocketShutdown.Both))
				throw new SocketException (SocketError.Shutdown);

			if (buffer == null)
				throw new ArgumentNullException ("buffer");

			if (offset < 0 || offset > buffer.Length)
				throw new ArgumentOutOfRangeException ("offset");

			if (size < 0 || size > buffer.Length - offset)
				throw new ArgumentOutOfRangeException ("size");

			var ssir = new SocketClientSendAsyncResult { AsyncState = state, remoteEndpoint = InternalRemoteEndPoint };

			if (size == 0)
				{
				ssir.CompletedSynchronously = true;
				((CEvent)ssir.AsyncWaitHandle).Set ();
				ssir.IsCompleted = true;
				ssir.errorCode = SocketErrorCodes.SOCKET_OK;
				if (callback != null)
					DoAsyncCallback (callback, ssir);
				}
			else
				{
				var result = client.SendDataAsync (buffer.Skip (offset).ToArray (), size, AsyncSendComplete, new AsyncSendState (ssir, callback));

				if (result != SocketErrorCodes.SOCKET_OK && result != SocketErrorCodes.SOCKET_OPERATION_PENDING)
					throw new SocketException (result.ToError ());
				}

			return ssir;
			}

		public IAsyncResult BeginSend (byte[] buffer, int size, AsyncCallback callback, Object state)
			{
			return BeginSend (buffer, 0, size, SocketFlags.None, callback, state);
			}

		public IAsyncResult BeginSend (byte[] buffer, int size, IPEndPoint endPoint, AsyncCallback callback, Object state)
			{
			return BeginSend (buffer, size, endPoint, callback, state, true);
			}

		private IAsyncResult BeginSend (byte[] buffer, int size, IPEndPoint endPoint, AsyncCallback callback, Object state, bool throwIfConnected)
			{
			Debug.WriteLine ("UdpSocket ({0}, {2}): BeginSend [size = {3}, endPoint = {1}, multicastGroup = {4}]", InternalLocalEndPoint, endPoint, AdapterType, size, _multicastGroup);

			if (endPoint == null)
				return BeginSend (buffer, size, callback, state);

			var client = _client;

			CheckDisposed ();

			if (_shutdown.HasValue && (_shutdown == SocketShutdown.Send || _shutdown == SocketShutdown.Both))
				throw new SocketException (SocketError.Shutdown);

			if (buffer == null)
				throw new ArgumentNullException ("buffer");

			if (size < 0 || size > buffer.Length)
				throw new ArgumentOutOfRangeException ("size");

			if (_connected && throwIfConnected)
					throw new InvalidOperationException ("default remote host already established");

			var ssir = new SocketClientSendAsyncResult { AsyncState = state, remoteEndpoint = endPoint };

			if (size == 0)
				{
				ssir.CompletedSynchronously = true;
				((CEvent)ssir.AsyncWaitHandle).Set ();
				ssir.IsCompleted = true;
				ssir.errorCode = SocketErrorCodes.SOCKET_OK;
				if (callback != null)
					DoAsyncCallback (callback, ssir);
				}
			else
				{
				EnableMulticastSendUDPServer (client, endPoint);
				ssir.disableNeeded = true;

				Debug.WriteLine ("UdpSocket ({0}, {3}): SendDataAsync from 0.0.0.0:{1} to {2}", InternalLocalEndPoint, client.PortNumber, endPoint, AdapterType);

				var result = client.SendDataAsync (buffer, size, endPoint, AsyncSendComplete, new AsyncSendState (ssir, callback));

				if (result != SocketErrorCodes.SOCKET_OK && result != SocketErrorCodes.SOCKET_OPERATION_PENDING)
					throw new SocketException (result.ToError ());
				}

			return ssir;
			}

		public IAsyncResult BeginSendTo (byte[] buffer, int offset, int size, SocketFlags socketFlags, IPEndPoint endPoint, AsyncCallback callback, Object state)
			{
			return BeginSend (buffer.Skip (offset).ToArray (), size, endPoint, callback, state, false);
			}

		public IAsyncResult BeginSend (byte[] buffer, int size, string hostname, int port, AsyncCallback callback, Object state)
			{
			return BeginSend (buffer, size, hostname, port, callback, state, true);
			}

		private IAsyncResult BeginSend (byte[] buffer, int size, string hostname, int port, AsyncCallback callback, Object state, bool throwIfConnected)
			{
			Debug.WriteLine ("UdpSocket ({0}, {3}): BeginSend [size = {4}, hostname = \"{1}\", port = {2}, multicastGroup = {5}]", InternalLocalEndPoint, hostname, port, AdapterType, size, _multicastGroup);

			var client = _client;

			CheckDisposed ();

			if (_shutdown.HasValue && (_shutdown == SocketShutdown.Send || _shutdown == SocketShutdown.Both))
				throw new SocketException (SocketError.Shutdown);

			if (buffer == null)
				throw new ArgumentNullException ("buffer");

			if (size < 0 || size > buffer.Length)
				throw new ArgumentOutOfRangeException ("size");

			if (_connected && throwIfConnected)
				throw new InvalidOperationException ("default remote host already established");

			var remoteEndpoint = new IPEndPoint (DnsEx.GetHostAddresses (hostname)[0], port);

			var ssir = new SocketClientSendAsyncResult { AsyncState = state, remoteEndpoint = remoteEndpoint };

			if (size == 0)
				{
				ssir.CompletedSynchronously = true;
				((CEvent)ssir.AsyncWaitHandle).Set ();
				ssir.IsCompleted = true;
				ssir.errorCode = SocketErrorCodes.SOCKET_OK;
				if (callback != null)
					DoAsyncCallback (callback, ssir);
				}
			else
				{
				EnableMulticastSendUDPServer (client, remoteEndpoint);
				ssir.disableNeeded = true;

				Debug.WriteLine ("UdpSocket ({0}, {4}): SendDataAsync from 0.0.0.0:{1} to {2}:{3}", InternalLocalEndPoint, client.PortNumber, hostname, port, AdapterType);

				var result = client.SendDataAsync (buffer, size, hostname, port, AsyncSendComplete, new AsyncSendState (ssir, callback));

				if (result != SocketErrorCodes.SOCKET_OK && result != SocketErrorCodes.SOCKET_OPERATION_PENDING)
					throw new SocketException (result.ToError ());
				}

			return ssir;
			}

		public IAsyncResult BeginSendTo (byte[] buffer, int offset, int size, SocketFlags socketFlags, string hostname, int port, AsyncCallback callback, Object state)
			{
			return BeginSend (buffer.Skip (offset).ToArray (), size, hostname, port, callback, state, false);
			}

		public override int EndSend (IAsyncResult asyncResult)
			{
			CheckDisposed ();

			if (asyncResult == null)
				throw new ArgumentNullException ("asyncResult");

			var ssir = asyncResult as SocketClientSendAsyncResult;

			if (ssir == null)
				throw new ArgumentException ("asyncResult");

			Debug.WriteLine ("UdpSocket ({0}, {1}): EndSend [remoteEndpoint = {2}]", InternalLocalEndPoint, AdapterType, ssir.remoteEndpoint);

			if (ssir.endSendCalled)
				throw new InvalidOperationException ("EndSend already called");
			ssir.endSendCalled = true;

			if (!ssir.CompletedSynchronously)
				ssir.AsyncWaitHandle.Wait ();

			if (ssir.errorCode != SocketErrorCodes.SOCKET_OK)
				throw new SocketException (ssir.errorCode.ToError ());

			return ssir.dataSent;
			}

		public int EndSendTo (IAsyncResult asyncResult)
			{
			return EndSend (asyncResult);
			}

		private void AsyncSendComplete (UDPServer cbClient, int length, object state)
			{
			var ass = (AsyncSendState)state;
			var iar = ass.AsyncResult;

			Debug.WriteLine (string.Format ("UdpSocket ({1}, {3}): AsyncSendComplete [length = {0}, remoteEndpoint = {2}]", length, InternalLocalEndPoint, iar.remoteEndpoint, AdapterType));

			var cb = ass.AsyncCallback;

			if (iar.disableNeeded)
				{
				DisableMulticastSendUDPServer (cbClient);
				iar.disableNeeded = false;
				}

			iar.IsCompleted = true;
			((CEvent)iar.AsyncWaitHandle).Set ();
			iar.dataSent = length;

			iar.errorCode = length > 0 ? SocketErrorCodes.SOCKET_OK : SocketErrorCodes.SOCKET_NOT_CONNECTED;

			if (cb != null)
				DoAsyncCallback (cb, iar);
			}

		private class AsyncReceiveState
			{
			public readonly SocketClientReceiveAsyncResult AsyncResult;
			public readonly AsyncCallback AsyncCallback;
			public readonly byte[] Buffer;
			public readonly int Offset;
			public readonly int ReqLength;

			public AsyncReceiveState (SocketClientReceiveAsyncResult asyncResult, AsyncCallback asyncCallback, byte[] buffer, int offset, int reqLength)
				{
				AsyncResult = asyncResult;
				AsyncCallback = asyncCallback;
				Buffer = buffer;
				Offset = offset;
				ReqLength = reqLength;
				}
			}

		public override IAsyncResult BeginReceive (byte[] buffer, int offset, int size, SocketFlags socketFlags, AsyncCallback callback, Object state)
			{
			Debug.WriteLine ("UdpSocket ({0}, {2}): BeginReceive [remoteEndpoint = {1}, multicastGroup = {3}]", InternalLocalEndPoint, InternalRemoteEndPoint, AdapterType, _multicastGroup);

			var client = _client;

			CheckDisposed ();

			if (buffer == null)
				throw new ArgumentNullException ("buffer");

			if (offset < 0 || offset > buffer.Length)
				throw new ArgumentOutOfRangeException ("offset");

			if (size < 0 || size > buffer.Length - offset)
				throw new ArgumentOutOfRangeException ("size");

			if (_shutdown.HasValue && (_shutdown == SocketShutdown.Receive || _shutdown == SocketShutdown.Both))
				throw new SocketException (SocketError.Shutdown);

			if (_connected)
				{
				if (_multicastGroup != null)
					throw new SocketException (SocketError.IsConnected);
				}
			else if (size != 0)
				EnableMulticastReceiveUDPServer (client);

			var srir = new SocketClientReceiveAsyncResult { AsyncState = state };

			if (size == 0)
				{
				srir.CompletedSynchronously = true;
				((CEvent)srir.AsyncWaitHandle).Set ();
				srir.IsCompleted = true;
				srir.errorCode = SocketErrorCodes.SOCKET_OK;
				if (callback != null)
					DoAsyncCallback (callback, srir);
				}
			else
				{
				var result = client.ReceiveDataAsync (AsyncReceiveComplete,
					new AsyncReceiveState (srir, callback, buffer, offset, size));

				if (result != SocketErrorCodes.SOCKET_OK && result != SocketErrorCodes.SOCKET_OPERATION_PENDING)
					{
					if (!_connected)
						DisableMulticastReceiveUDPServer (client);

					throw new SocketException (result.ToError ());
					}
				}

			return srir;
			}

		public IAsyncResult BeginReceiveFrom (byte[] buffer, int offset, int size, SocketFlags socketFlags, ref IPEndPoint endPoint, AsyncCallback callback, Object state)
			{
			Debug.WriteLine ("UdpSocket ({0}, {2}): BeginReceiveFrom [endPoint = {1}, multicastGroup = {3}]", InternalLocalEndPoint, endPoint, AdapterType, _multicastGroup);

			var client = _client;

			CheckDisposed ();

			if (buffer == null)
				throw new ArgumentNullException ("buffer");

			if (offset < 0 || offset > buffer.Length)
				throw new ArgumentOutOfRangeException ("offset");

			if (size < 0 || size > buffer.Length - offset)
				throw new ArgumentOutOfRangeException ("size");

			if (_shutdown.HasValue && (_shutdown == SocketShutdown.Receive || _shutdown == SocketShutdown.Both))
				throw new SocketException (SocketError.Shutdown);

			if (_connected)
				{
				if (_multicastGroup != null)
					throw new SocketException (SocketError.IsConnected);
				}
			else
				{
				if (_multicastGroup == null)
					client.AddressToAcceptConnectionFrom = endPoint.Address.ToString ();
				else if (!endPoint.Address.Equals (_multicastGroup) && !endPoint.Address.Equals (IPAddress.Any))
					throw new SocketException (SocketError.InvalidArgument);

				client.RemotePortNumber = endPoint.Port;

				if (size != 0)
					EnableMulticastReceiveUDPServer (client);
				}

			var srir = new SocketClientReceiveAsyncResult { AsyncState = state };

			if (size == 0)
				{
				srir.CompletedSynchronously = true;
				((CEvent)srir.AsyncWaitHandle).Set ();
				srir.IsCompleted = true;
				srir.errorCode = SocketErrorCodes.SOCKET_OK;
				if (callback != null)
					DoAsyncCallback (callback, srir);
				}
			else
				{
				var result = client.ReceiveDataAsync (AsyncReceiveComplete,
					new AsyncReceiveState (srir, callback, buffer, offset, size));

				if (result != SocketErrorCodes.SOCKET_OK && result != SocketErrorCodes.SOCKET_OPERATION_PENDING)
					{
					if (!_connected)
						DisableMulticastReceiveUDPServer (client);

					throw new SocketException (result.ToError ());
					}
				}

			return srir;
			}

		private void AsyncReceiveComplete (UDPServer cbClient, int length, object state)
			{
			Debug.WriteLine (string.Format ("UdpSocket ({1}, {3}): AsyncReceiveComplete [length = {0}, remoteEndpoint = {2}]", length, InternalLocalEndPoint, LastReceivedFromEndPoint, AdapterType));

			if (!_connected)
				DisableMulticastReceiveUDPServer (cbClient);

			var ars = (AsyncReceiveState)state;
			var iar = ars.AsyncResult;
			var cb = ars.AsyncCallback;
			var buff = ars.Buffer;
			var offset = ars.Offset;
			var reqLength = ars.ReqLength;

			iar.IsCompleted = true;
			((CEvent)iar.AsyncWaitHandle).Set ();
			iar.dataReceived = length;

			if (length != 0)
				{
				iar.errorCode = SocketErrorCodes.SOCKET_OK;

				iar.remoteEndpoint = new IPEndPoint (IPAddress.Parse (cbClient.IPAddressLastMessageReceivedFrom), cbClient.IPPortLastMessageReceivedFrom);

				int retLength = Math.Min (reqLength, length);

				Buffer.BlockCopy (cbClient.IncomingDataBuffer, 0, buff, offset, retLength);

				iar.dataReceived = retLength;

				if (retLength != length)
					iar.error = SocketError.MessageSize;

				_messageReceived = true;
				}
			else
				{
				if (cbClient.ServerStatus == SocketStatus.SOCKET_STATUS_NO_CONNECT)
					{
					iar.errorCode = _connected ? cbClient.EnableUDPServer () : SocketErrorCodes.SOCKET_OK;

					iar.error = SocketError.TimedOut;
					}
				else
					iar.errorCode = SocketErrorCodes.SOCKET_NOT_CONNECTED;
				}

			if (cb != null)
				DoAsyncCallback (cb, iar);
			}

		public override int EndReceive (IAsyncResult asyncResult)
			{
			CheckDisposed ();

			if (asyncResult == null)
				throw new ArgumentNullException ("asyncResult");

			var srir = asyncResult as SocketClientReceiveAsyncResult;

			if (srir == null)
				throw new ArgumentException ("asyncResult");

			Debug.WriteLine ("UdpSocket ({0}, {3}): EndReceive [length = {2}, remoteEndpoint = {1}]", InternalLocalEndPoint, srir.remoteEndpoint, srir.dataReceived, AdapterType);

			if (srir.endreceiveCalled)
				throw new InvalidOperationException ("EndReceive already called");
			srir.endreceiveCalled = true;

			if (!srir.CompletedSynchronously)
				srir.AsyncWaitHandle.Wait ();

			if (srir.error != SocketError.Success)
				throw new SocketException (srir.error);

			if (srir.errorCode == SocketErrorCodes.SOCKET_NOT_CONNECTED)
				{
				((IDisposable)this).Dispose ();

				return 0;
				}

			if (srir.errorCode != SocketErrorCodes.SOCKET_OK)
				throw new SocketException (srir.errorCode.ToError ());

			return srir.dataReceived;
			}

		public int EndReceiveFrom (IAsyncResult asyncResult, ref IPEndPoint endPoint)
			{
			CheckDisposed ();

			if (asyncResult == null)
				throw new ArgumentNullException ("asyncResult");

			var srir = asyncResult as SocketClientReceiveAsyncResult;

			if (srir == null)
				throw new ArgumentException ("asyncResult");

			Debug.WriteLine ("UdpSocket ({0}, {3}): EndReceiveFrom [length = {2}, remoteEndpoint = {1}]", InternalLocalEndPoint, srir.remoteEndpoint, srir.dataReceived, AdapterType);

			if (srir.endreceiveCalled)
				throw new InvalidOperationException ("EndReceive already called");
			srir.endreceiveCalled = true;

			if (!srir.CompletedSynchronously)
				srir.AsyncWaitHandle.Wait ();

			if (srir.error != SocketError.Success)
				throw new SocketException (srir.error);

			if (srir.errorCode == SocketErrorCodes.SOCKET_NOT_CONNECTED)
				{
				((IDisposable)this).Dispose ();

				return 0;
				}

			if (srir.errorCode != SocketErrorCodes.SOCKET_OK)
				throw new SocketException (srir.errorCode.ToError ());

			endPoint = srir.remoteEndpoint;

			return srir.dataReceived;
			}

		public IAsyncResult BeginReceive (AsyncCallback callback, object state)
			{
			Debug.WriteLine ("UdpSocket ({0}, {2}): BeginReceive [remoteEndpoint = {1}, multicastGroup = {3}]", InternalLocalEndPoint, InternalRemoteEndPoint, AdapterType, _multicastGroup);

			var client = _client;

			CheckDisposed ();

			if (_shutdown.HasValue && (_shutdown == SocketShutdown.Receive || _shutdown == SocketShutdown.Both))
				throw new SocketException (SocketError.Shutdown);

			if (_connected)
				{
				if (_multicastGroup != null)
					throw new SocketException (SocketError.IsConnected);
				}
			else
				EnableMulticastReceiveUDPServer (client);

			var srir = new SocketClientReceiveAsyncResult { AsyncState = state };

			var result = client.ReceiveDataAsync (AsyncReceiveComplete2,
															  new AsyncReceiveState (srir, callback, null, 0, 0));

			if (result != SocketErrorCodes.SOCKET_OK && result != SocketErrorCodes.SOCKET_OPERATION_PENDING)
				{
				if (!_connected)
					DisableMulticastReceiveUDPServer (client);

				throw new SocketException (result.ToError ());
				}

			return srir;
			}

		private void AsyncReceiveComplete2 (UDPServer cbClient, int length, object state)
			{
			Debug.WriteLine (string.Format ("UdpSocket ({1}, {3}): AsyncReceiveComplete2 [length = {0}, remoteEndpoint = {2}]", length, InternalLocalEndPoint, LastReceivedFromEndPoint, AdapterType));

			if (!_connected)
				DisableMulticastReceiveUDPServer (cbClient);

			var ars = (AsyncReceiveState)state;
			var iar = ars.AsyncResult;
			var cb = ars.AsyncCallback;

			iar.IsCompleted = true;
			((CEvent)iar.AsyncWaitHandle).Set ();
			iar.dataReceived = length;

			if (length != 0)
				{
				iar.errorCode = SocketErrorCodes.SOCKET_OK;
				iar.remoteEndpoint = new IPEndPoint (IPAddress.Parse (cbClient.IPAddressLastMessageReceivedFrom), cbClient.IPPortLastMessageReceivedFrom);
				iar.data = new byte[length];
				Buffer.BlockCopy (cbClient.IncomingDataBuffer, 0, iar.data, 0, length);
				iar.dataReceived = length;

				_messageReceived = true;
				}
			else
				{
				if (cbClient.ServerStatus == SocketStatus.SOCKET_STATUS_NO_CONNECT)
					{
					iar.errorCode = _connected ? cbClient.EnableUDPServer () : SocketErrorCodes.SOCKET_OK;

					iar.error = SocketError.TimedOut;
					}
				else
					iar.errorCode = SocketErrorCodes.SOCKET_NOT_CONNECTED;
				}

			if (cb != null)
				DoAsyncCallback (cb, iar);
			}

		public byte[] EndReceive (IAsyncResult asyncResult, ref IPEndPoint remoteEndpoint)
			{
			CheckDisposed ();

			if (asyncResult == null)
				throw new ArgumentNullException ("asyncResult");

			var srir = asyncResult as SocketClientReceiveAsyncResult;

			if (srir == null)
				throw new ArgumentException ("asyncResult");

			Debug.WriteLine ("UdpSocket ({0}, {3}): EndReceive [length = {2}, remoteEndPoint = {1}", InternalLocalEndPoint, srir.remoteEndpoint, srir.dataReceived, AdapterType);

			if (srir.endreceiveCalled)
				throw new InvalidOperationException ("EndReceive already called");
			srir.endreceiveCalled = true;

			if (!srir.CompletedSynchronously)
				srir.AsyncWaitHandle.Wait ();

			if (srir.error != SocketError.Success)
				throw new SocketException (srir.error);

			if (srir.errorCode == SocketErrorCodes.SOCKET_NOT_CONNECTED)
				{
				return new byte[0];
				}

			if (srir.errorCode != SocketErrorCodes.SOCKET_OK)
				throw new SocketException (srir.errorCode.ToError ());

			remoteEndpoint = srir.remoteEndpoint;

			return srir.data;
			}

		protected override void Dispose (bool disposing)
			{
			Debug.WriteLine (String.Format ("UdpSocket ({2}, {3}): Dispose({0}) [_disposed = {1}]", disposing, _disposed, InternalLocalEndPoint, AdapterType));

			if (Interlocked.CompareExchange (ref _disposed, 1, 0) != 0)
				return;

			if (disposing)
				{
				if (_client != null)
					{
					if (_client.ServerStatus == SocketStatus.SOCKET_STATUS_CONNECTED)
						_client.DisableUDPServer ();
					_activeEvent.Reset ();
					_client.Dispose ();
					_client = null;
					_active = false;
					_activeCount = 0;
					_multicastGroup = null;
					}
				}

			_active = false;
			}

		private class SocketClientSendAsyncResult : IAsyncResult
			{
			private readonly CEventHandle waitHandle = new CEvent (false, false);
			internal SocketErrorCodes errorCode;
			internal int dataSent;
			internal bool endSendCalled;
			internal bool disableNeeded;
			internal IPEndPoint remoteEndpoint;

			public SocketStatus Status
				{
				get;
				internal set;
				}

			#region IAsyncResult Members

			public object AsyncState
				{
				get;
				internal set;
				}

			public CEventHandle AsyncWaitHandle
				{
				get { return waitHandle; }
				}

			public bool CompletedSynchronously
				{
				get;
				internal set;
				}

			public bool IsCompleted
				{
				get;
				internal set;
				}

			public object InnerObject
				{
				get { return null; }
				}

			#endregion
			}

		private class SocketClientReceiveAsyncResult : IAsyncResult
			{
			private readonly CEventHandle waitHandle = new CEvent (false, false);
			internal SocketErrorCodes errorCode;
			internal SocketError error = SocketError.Success;
			internal int dataReceived;
			internal bool endreceiveCalled;
			internal IPEndPoint remoteEndpoint;
			internal byte[] data;

			public SocketStatus Status
				{
				get;
				internal set;
				}

			#region IAsyncResult Members

			public object AsyncState
				{
				get;
				internal set;
				}

			public CEventHandle AsyncWaitHandle
				{
				get { return waitHandle; }
				}

			public bool CompletedSynchronously
				{
				get;
				internal set;
				}

			public bool IsCompleted
				{
				get;
				internal set;
				}

			public object InnerObject
				{
				get { return null; }
				}

			#endregion
			}
		}
	}