using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;
using SSMono.Net.Sockets;
using Socket = Crestron.SimplSharp.CrestronSockets.CrestronClientSocket;
using IAsyncResult = Crestron.SimplSharp.CrestronIO.IAsyncResult;
using AsyncCallback = Crestron.SimplSharp.CrestronIO.AsyncCallback;
using SSCore.Diagnostics;

namespace Crestron.SimplSharp.CrestronSockets
	{
	using SocketException = SSMono.Net.Sockets.SocketException;

	public class CrestronClientSocket : CrestronConnectableSocket
		{
		protected TCPClient _client;
		private bool finishing;
		private readonly List<byte> dataBuffer = new List<byte> ();

		public CrestronClientSocket (TCPClient client)
			{
			_client = client;
			}

		public CrestronClientSocket ()
			{
			_client = new TCPClient
				{
				Nagle = true
				};
			}

		public CrestronClientSocket (IPAddress ipAddress, int port)
			{
			if (ipAddress == null)
				throw new ArgumentNullException ("ipAddress");

			if (port < IPEndPoint.MinPort || port > IPEndPoint.MaxPort)
				throw new ArgumentOutOfRangeException ("port");

			_client = new TCPClient
				{
					AddressClientConnectedTo = ipAddress.ToString (),
					PortNumber = port,
					Nagle = true
				};

			SetupClient ();
			}

		public CrestronClientSocket (string host, int port)
			{
			Debug.WriteLine ("Client Create and Connect ({0}:{1}", host, port);

			if (host == null)
				throw new ArgumentNullException ("host");

			if (port < IPEndPoint.MinPort || port > IPEndPoint.MaxPort)
				throw new ArgumentOutOfRangeException ("port");

			var ipaddresses = DnsEx.GetHostAddresses (host);

			if (ipaddresses.Length == 0)
				throw new SocketException (SocketError.HostNotFound);

			_client = new TCPClient
				{
					AddressClientConnectedTo = ipaddresses[0].ToString (),
					PortNumber = port,
					Nagle = true
				};

			SetupClient ();
			}

		private void SetupClient ()
			{
			var result = _client.ConnectToServer ();

			if (result != SocketErrorCodes.SOCKET_OK)
				throw new SocketException (result.ToError ());
			}

		public override AddressFamily AddressFamily
			{
			get
				{
				return AddressFamily.InterNetwork;
				}
			}

		public override bool Connected
			{
			get
				{
				var client = _client;

				return client != null && client.ClientStatus == SocketStatus.SOCKET_STATUS_CONNECTED;
				}
			}

		public override void Bind (IPEndPoint endPoint)
			{
			var client = _client;

			if (client.PortNumber != 0 && client.PortNumber != 65535)
				throw new SocketException (SocketError.InvalidArgument, "Socket already bound");

			client.PortNumber = endPoint.Port;
			}

		public override void Close ()
			{
			Debug.WriteLine (String.Format ("Client Close [address: {0}, port: {1}, disposed: {2}]", _client == null ? "<unknown>" : _client.AddressClientConnectedTo, _client == null ? "<unknown>" : _client.PortNumber.ToString (), _disposed));

			base.Close ();
			}

		public override bool DataAvailable
			{
			get
				{
				var client = _client;

				CheckDisposed ();

				return client.DataAvailable;
				}
			}

		private IPEndPoint _remoteEndPoint;
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

				if (_disposed != 0 || client == null || String.IsNullOrEmpty (client.AddressClientConnectedTo))
					return new IPEndPoint (IPAddress.None, 0);

				if (_remoteEndPoint != null)
					return _remoteEndPoint;

				var ipaddrs = DnsEx.GetHostAddresses (client.AddressClientConnectedTo);
				return _remoteEndPoint = ipaddrs.Length == 0 ? new IPEndPoint (IPAddress.None, client.PortNumber) : new IPEndPoint (ipaddrs[0], client.PortNumber);
				}
			}

		private IPEndPoint _localEndpoint;
		public override IPEndPoint LocalEndPoint
			{
			get
				{
				var client = _client;

				CheckDisposed ();

				return _localEndpoint ?? (_localEndpoint = new IPEndPoint (IPAddress.Parse (client.LocalAddressOfClient), client.LocalPortNumberOfClient));
				}
			}

		public override bool Nagle
			{
			get
				{
				var client = _client;

				CheckDisposed ();

				return client.Nagle;
				}
			set
				{
				var client = _client;

				CheckDisposed ();

				client.Nagle = value;
				}
			}

		public override ProtocolType ProtocolType
			{
			get
				{
				return ProtocolType.Tcp;
				}
			}

		public override SocketType SocketType
			{
			get
				{
				return SocketType.Stream;
				}
			}

		public TCPClient Client
			{
			get { return _client; }
			}

		protected override void CheckDisposed ()
			{
			if (_disposed != 0 || _client == null)
				throw new ObjectDisposedException (GetType ().FullName);
			}

		public override void Connect (IPEndPoint remoteEP)
			{
			var client = _client;

			Debug.WriteLine ("Client Connect ({0})", remoteEP);

			CheckDisposed ();

			if (remoteEP == null)
				throw new ArgumentNullException ("remoteEP");

			client.AddressClientConnectedTo = remoteEP.Address.ToString ();
			client.PortNumber = remoteEP.Port;

			var result = client.ConnectToServer ();

			if (result != SocketErrorCodes.SOCKET_OK)
				throw new SocketException (result.ToError ());

			_active = true;

			Debug.WriteLine ("     Client Connected ({0})", remoteEP);
			}

		public override int Send (byte[] buffer, int offset, int size, SocketFlags socketFlags)
			{
			var client = _client;

			Debug.WriteLine (String.Format ("Client ({2}): Send [offset = {1}, size = {0}]", size, offset, InternalRemoteEndPoint));

			CheckDisposed ();

			if (_shutdown.HasValue && (_shutdown == SocketShutdown.Send || _shutdown == SocketShutdown.Both))
				throw new SocketException (SocketError.Shutdown);

			if (client.ClientStatus != SocketStatus.SOCKET_STATUS_CONNECTED)
				throw new SocketException (SocketError.NotConnected);

			if (buffer == null)
				throw new ArgumentNullException ("buffer");

			if (offset < 0 || offset > buffer.Length)
				throw new ArgumentOutOfRangeException ("offset");

			if (size < 0 || size > buffer.Length - offset)
				throw new ArgumentOutOfRangeException ("size");

			if (size == 0)
				return 0;

			client.SocketSendOrReceiveTimeOutInMs = SendTimeout;

			SocketErrorCodes result = client.SendData (buffer, offset, size);

			client.SocketSendOrReceiveTimeOutInMs = 0;

			if (result != SocketErrorCodes.SOCKET_OK)
				throw new SocketException (result.ToError ());

			UpdateSentData (size);

			return size;
			}

		public override int SendTo (byte[] buffer, int offset, int size, SocketFlags socketFlags, IPEndPoint remoteEP)
			{
			return Send (buffer, offset, size, socketFlags);
			}

		public override int Receive (byte[] buffer, int offset, int size, SocketFlags socketFlags)
			{
			var client = _client;

			Debug.WriteLine (string.Format ("Client ({2}): Receive [offset = {1}, size = {0}]", size, offset, InternalRemoteEndPoint));

			CheckDisposed ();

			if (buffer == null)
				throw new ArgumentNullException ("buffer");

			if (offset < 0 || offset > buffer.Length)
				throw new ArgumentOutOfRangeException ("offset");

			if (size < 0 || size > buffer.Length - offset)
				throw new ArgumentOutOfRangeException ("size");

			if (_shutdown.HasValue && (_shutdown == SocketShutdown.Receive || _shutdown == SocketShutdown.Both))
				throw new SocketException (SocketError.Shutdown);

			if (size == 0)
				return 0;

			if (dataBuffer.Count == 0)
				{
				if (finishing || client == null || client.ClientStatus == SocketStatus.SOCKET_STATUS_NO_CONNECT)
					{
					Debug.WriteLine ("Client ({0}) Receive [0]", InternalRemoteEndPoint);

					((IDisposable)this).Dispose ();

					return 0;
					}

				CheckDisposed ();

				client.SocketSendOrReceiveTimeOutInMs = ReceiveTimeout;

				int length = client.ReceiveData ();
				Debug.WriteLine (String.Format ("TCPClient ({1}): ReceiveData [{0}]", length, InternalRemoteEndPoint));

				client.SocketSendOrReceiveTimeOutInMs = 0;

				if (length == 0)
					{
					Debug.WriteLine ("Client ({0}): Receive [0]", InternalRemoteEndPoint);

					((IDisposable)this).Dispose ();

					return 0;
					}

				dataBuffer.AddRange (client.IncomingDataBuffer.Take (length));
				}

			int retLength = dataBuffer.Count <= size ? dataBuffer.Count : size;

			Buffer.BlockCopy (dataBuffer.ToArray (), 0, buffer, offset, retLength);

			dataBuffer.RemoveRange (0, retLength);

			Debug.WriteLine (String.Format ("Client ({1}): Receive [{0}]", retLength, InternalRemoteEndPoint));

			return retLength;
			}

		private class AsyncConnectState
			{
			public SocketClientConnectAsyncResult AsyncResult;
			public AsyncCallback AsyncCallback;

			public AsyncConnectState (SocketClientConnectAsyncResult asyncResult, AsyncCallback asyncCallback)
				{
				AsyncResult = asyncResult;
				AsyncCallback = asyncCallback;
				}
			}

		private class ForState
			{
			public IPAddress[] Addresses;
			public int Index;
			public AsyncCallback Callback;
			public object State;
			public int Port;

			public ForState (IPAddress[] addresses, int port, AsyncCallback callback, object state)
				{
				Addresses = addresses;
				Port = port;
				Callback = callback;
				State = state;
				Index = 0;
				}
			}

		public IAsyncResult BeginConnect (string host, int port, AsyncCallback callback, object state)
			{
			CheckDisposed ();

			if (host == null)
				throw new ArgumentNullException ("host");

			var addresses = DnsEx.GetHostAddresses (host);

			return BeginConnect (addresses, port, callback, state);
			}

		public IAsyncResult BeginConnect (IPAddress address, int port, AsyncCallback callback, object state)
			{
			CheckDisposed ();

			if (address == null)
				throw new ArgumentNullException ("address");

			return BeginConnect (new IPEndPoint (address, port), callback, state);
			}

		public IAsyncResult BeginConnect (IPAddress[] addresses, int port, AsyncCallback callback, object state)
			{
			CheckDisposed ();

			if (addresses == null || addresses.Any (a => a == null))
				throw new ArgumentNullException ("addresses");

			if (addresses.Length == 0)
				throw new ArgumentException ("must be at least one address in list", "addresses");

			var forState = new ForState (addresses, port, callback, state);
			return BeginConnect (new IPEndPoint (addresses[0], port), ForCallback, forState);
			}

		private void ForCallback (IAsyncResult iar)
			{
			var forState = (ForState)iar.AsyncState;
			var sccar = (SocketClientConnectAsyncResult)iar;

			if (sccar.Status == SocketStatus.SOCKET_STATUS_CONNECTED || ++forState.Index >= forState.Addresses.Length)
				{
				if (forState.Callback != null)
					{
					sccar.AsyncState = forState.State;
					DoAsyncCallback (forState.Callback, iar);
					}
				return;
				}

			if (_disposed != 0)
				return;

			BeginConnect (new IPEndPoint (forState.Addresses[forState.Index], forState.Port), ForCallback, forState);
			}

		public IAsyncResult BeginConnect (IPEndPoint remoteEP, AsyncCallback callback, object state)
			{
			var client = _client;

			Debug.WriteLine ("Client BeginConnect ({0})", remoteEP);

			CheckDisposed ();

			client.AddressClientConnectedTo = remoteEP.Address.ToString ();
			client.PortNumber = remoteEP.Port;

			var scir = new SocketClientConnectAsyncResult {AsyncState = state};
			var result = client.ConnectToServerAsync (AsyncConnectComplete, new AsyncConnectState (scir, callback));

			if (result != SocketErrorCodes.SOCKET_OK && result != SocketErrorCodes.SOCKET_OPERATION_PENDING)
				throw new SocketException (result.ToError ());

			return scir;
			}

		public void EndConnect (IAsyncResult asyncResult)
			{
			var client = _client;

			Debug.WriteLine ("Client EndConnect {0}:{1}", client == null ? "<unknown>" : client.AddressClientConnectedTo, client == null ? "<unknown>" : client.PortNumber.ToString ());

			CheckDisposed ();

			if (asyncResult == null)
				throw new ArgumentNullException ("asyncResult");

			var scir = asyncResult as SocketClientConnectAsyncResult;

			if (scir == null)
				throw new ArgumentException ("asyncResult");

			if (scir.endConnectCalled)
				throw new InvalidOperationException ("EndConnect already called");
			scir.endConnectCalled = true;

			if (!scir.CompletedSynchronously)
				scir.AsyncWaitHandle.Wait ();

			if (scir.Status != SocketStatus.SOCKET_STATUS_CONNECTED)
				throw new SocketException (scir.Status.ToError ());
			}

		private void AsyncConnectComplete (TCPClient client, object state)
			{
#if DEBUG
			if (_disposed != 0 || _client == null)
				{
				var ipaddrs = DnsEx.GetHostAddresses (client.AddressClientConnectedTo);
				var remoteEndPoint = ipaddrs.Length == 0 ? new IPEndPoint (IPAddress.None, client.PortNumber) : new IPEndPoint (ipaddrs[0], client.PortNumber);

				Debug.WriteLine ("Client ({0}): AsyncConnectComplete - client disposed [{1}]", remoteEndPoint, client.ClientStatus);
				}
			else
				Debug.WriteLine ("Client ({0}): AsyncConnectComplete", InternalRemoteEndPoint);
#endif

			var acs = (AsyncConnectState)state;
			var iar = acs.AsyncResult;
			var cb = acs.AsyncCallback;

			iar.IsCompleted = true;
			((CEvent)iar.AsyncWaitHandle).Set ();

			var scir = (SocketClientConnectAsyncResult)iar;
			scir.Status = client.ClientStatus;

			if (cb != null)
				DoAsyncCallback (cb, iar);
			}

		private class AsyncSendState
			{
			public SocketClientSendAsyncResult AsyncResult;
			public AsyncCallback AsyncCallback;

			public AsyncSendState (SocketClientSendAsyncResult asyncResult, AsyncCallback asyncCallback)
				{
				AsyncResult = asyncResult;
				AsyncCallback = asyncCallback;
				}
			}

		public override IAsyncResult BeginSend (byte[] buffer, int offset, int size, SocketFlags socketFlags, AsyncCallback callback, Object state)
			{
			var client = _client;

			Debug.WriteLine ("Client ({0}): BeginSend", InternalRemoteEndPoint);

			CheckDisposed ();

			if (_shutdown.HasValue && (_shutdown == SocketShutdown.Send || _shutdown == SocketShutdown.Both))
				throw new SocketException (SocketError.Shutdown);

			if (client.ClientStatus != SocketStatus.SOCKET_STATUS_CONNECTED)
				throw new SocketException (SocketError.NotConnected);

			if (buffer == null)
				throw new ArgumentNullException ("buffer");

			if (offset < 0 || offset > buffer.Length)
				throw new ArgumentOutOfRangeException ("offset");

			if (size < 0 || size > buffer.Length - offset)
				throw new ArgumentOutOfRangeException ("size");

			var ssir = new SocketClientSendAsyncResult { AsyncState = state };

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
				var result = client.SendDataAsync (buffer, offset, size, AsyncSendComplete, new AsyncSendState (ssir, callback));

				if (result != SocketErrorCodes.SOCKET_OK && result != SocketErrorCodes.SOCKET_OPERATION_PENDING)
					throw new SocketException (result.ToError ());
				}

			return ssir;
			}

		public override int EndSend (IAsyncResult asyncResult)
			{
			Debug.WriteLine ("Client ({0}): EndSend", InternalRemoteEndPoint);

			CheckDisposed ();

			if (asyncResult == null)
				throw new ArgumentNullException ("asyncResult");

			var ssir = asyncResult as SocketClientSendAsyncResult;

			if (ssir == null)
				throw new ArgumentException ("asyncResult");

			if (ssir.endSendCalled)
				throw new InvalidOperationException ("EndSend already called");
			ssir.endSendCalled = true;

			if (!ssir.CompletedSynchronously)
				ssir.AsyncWaitHandle.Wait ();

			if (ssir.errorCode != SocketErrorCodes.SOCKET_OK)
				throw new SocketException (ssir.errorCode.ToError ());

			return ssir.dataSent;
			}

		private void AsyncSendComplete (TCPClient cbClient, int length, object state)
			{
			Debug.WriteLine (string.Format ("Client ({1}): AsyncSendComplete [{0}]", length, InternalRemoteEndPoint));

			var ass = (AsyncSendState)state;
			var iar = ass.AsyncResult;
			var cb = ass.AsyncCallback;

			iar.IsCompleted = true;
			((CEvent)iar.AsyncWaitHandle).Set ();
			iar.dataSent = length;

			if (length > 0)
				{
				iar.errorCode = SocketErrorCodes.SOCKET_OK;

				UpdateSentData (length);
				}
			else
				{
				iar.errorCode = SocketErrorCodes.SOCKET_NOT_CONNECTED;
				iar.Status = cbClient.ClientStatus;
				}

			if (cb != null)
				DoAsyncCallback (cb, iar);
			}

		private class AsyncReceiveState
			{
			public SocketClientReceiveAsyncResult AsyncResult;
			public AsyncCallback AsyncCallback;
			public byte[] Buffer;
			public int Offset;
			public int ReqLength;

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
			var client = _client;

			Debug.WriteLine ("Client ({0}): BeginReceive", InternalRemoteEndPoint);

			CheckDisposed ();

			if (buffer == null)
				throw new ArgumentNullException ("buffer");

			if (offset < 0 || offset > buffer.Length)
				throw new ArgumentOutOfRangeException ("offset");

			if (size < 0 || size > buffer.Length - offset)
				throw new ArgumentOutOfRangeException ("size");

			if (_shutdown.HasValue && (_shutdown == SocketShutdown.Receive || _shutdown == SocketShutdown.Both))
				throw new SocketException (SocketError.Shutdown);

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
				if (dataBuffer.Count == 0)
					{
					if (finishing)
						{
						srir.CompletedSynchronously = true;
						srir.IsCompleted = true;
						((CEvent)srir.AsyncWaitHandle).Set ();
						if (callback != null)
							DoAsyncCallback (callback, srir);
						}
					else
						{
						var result = client.ReceiveDataAsync (AsyncReceiveComplete,
							new AsyncReceiveState (srir, callback, buffer, offset, size));

						if (result != SocketErrorCodes.SOCKET_OK && result != SocketErrorCodes.SOCKET_OPERATION_PENDING)
							throw new SocketException (result.ToError ());
						}
					}
				else
					{
					int retLength = dataBuffer.Count <= size ? dataBuffer.Count : size;

					Debug.WriteLine (string.Format ("Client ({1}): SyncReceiveComplete [{0}]", retLength, InternalRemoteEndPoint));

					Buffer.BlockCopy (dataBuffer.ToArray (), 0, buffer, offset, retLength);

					dataBuffer.RemoveRange (0, retLength);

					srir.dataReceived = retLength;

					srir.CompletedSynchronously = true;
					srir.IsCompleted = true;
					((CEvent)srir.AsyncWaitHandle).Set ();
					if (callback != null)
						DoAsyncCallback (callback, srir);
					}
				}

			return srir;
			}

		private void AsyncReceiveComplete (TCPClient cbClient, int length, object state)
			{
			Debug.WriteLine (string.Format ("Client ({1}): AsyncReceiveComplete [{0}]", length, InternalRemoteEndPoint));

			var ars = (AsyncReceiveState)state;
			var iar = ars.AsyncResult;
			var cb = ars.AsyncCallback;
			var buff = ars.Buffer;
			var offset = ars.Offset;
			var reqLength = ars.ReqLength;

			iar.IsCompleted = true;
			((CEvent)iar.AsyncWaitHandle).Set ();
			iar.dataReceived = length;

			if (length > 0)
				{
				iar.errorCode = SocketErrorCodes.SOCKET_OK;

				dataBuffer.AddRange (cbClient.IncomingDataBuffer.Take (length));

				int retLength = dataBuffer.Count <= reqLength ? dataBuffer.Count : reqLength;

				Buffer.BlockCopy (dataBuffer.ToArray (), 0, buff, offset, retLength);

				dataBuffer.RemoveRange (0, retLength);

				iar.dataReceived = retLength;
				}
			else
				{
				iar.errorCode = SocketErrorCodes.SOCKET_NOT_CONNECTED;
				iar.Status = cbClient.ClientStatus;
				finishing = true;
				}

			if (cb != null)
				DoAsyncCallback (cb, iar);
			}

		public override int EndReceive (IAsyncResult asyncResult)
			{
			Debug.Write (String.Format ("Client ({0}): EndReceive", InternalRemoteEndPoint));

			bool inhibitWriteLine = false;

			try
				{
				CheckDisposed ();

				if (asyncResult == null)
					throw new ArgumentNullException ("asyncResult");

				var srir = asyncResult as SocketClientReceiveAsyncResult;

				if (srir == null)
					throw new ArgumentException ("asyncResult");

				if (srir.endreceiveCalled)
					throw new InvalidOperationException ("EndReceive already called");
				srir.endreceiveCalled = true;

				if (!srir.CompletedSynchronously)
					srir.AsyncWaitHandle.Wait ();

				if (finishing || srir.errorCode == SocketErrorCodes.SOCKET_NOT_CONNECTED)
					{
					Debug.WriteLine (" returns 0");
					inhibitWriteLine = true;

					((IDisposable)this).Dispose ();

					return 0;
					}

				if (srir.errorCode != SocketErrorCodes.SOCKET_OK)
					throw new SocketException (srir.errorCode.ToError ());

				Debug.Write (String.Format (" returns {0}", srir.dataReceived));

				return srir.dataReceived;
				}
			finally
				{
				Debug.WriteLineIf (!inhibitWriteLine, String.Empty);
				}
			}

		protected override void Dispose (bool disposing)
			{
			Debug.WriteLine (String.Format ("ClientSocket ({2}): Dispose({0}) [_disposed = {1}]", disposing, _disposed, _disposed != 0 ? "<disposed>" : InternalRemoteEndPoint.ToString ()));

			if (Interlocked.CompareExchange (ref _disposed, 1, 0) != 0)
				return;

			finishing = false;

			if (disposing)
				{
				var client = _client;

				if (client != null)
					{
					client.DisconnectFromServer ();
					client.Dispose ();
					_client = null;
					_active = false;
					}
				}

			base.Dispose (disposing);
			}

		private class SocketClientConnectAsyncResult : IAsyncResult
			{
			private readonly CEventHandle waitHandle = new CEvent (false, false);
			internal bool endConnectCalled;

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

		private class SocketClientSendAsyncResult : IAsyncResult
			{
			private readonly CEventHandle waitHandle = new CEvent (false, false);
			internal SocketErrorCodes errorCode;
			internal int dataSent;
			internal bool endSendCalled;

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
			internal int dataReceived;
			internal bool endreceiveCalled;

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