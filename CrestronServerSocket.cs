//
// CrestronServerSocket.cs
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

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;
using IAsyncResult = Crestron.SimplSharp.CrestronIO.IAsyncResult;
using AsyncCallback = Crestron.SimplSharp.CrestronIO.AsyncCallback;
using SSMono.Net.Sockets;
using SSCore.Diagnostics;
using SSMono.Threading;

namespace Crestron.SimplSharp.CrestronSockets
	{
	using SocketException = SSMono.Net.Sockets.SocketException;

	public class CrestronServerSocket : CrestronSocket
		{
		internal CrestronListenerSocket Listener;
		internal uint ClientIndex;
		internal bool PendingReceive;
		internal bool PendingSend;
		internal bool WaitingClose;

		private readonly List<byte> _dataBuffer = new List<byte> ();
		private bool _finishing;

		public CrestronServerSocket (CrestronListenerSocket socket, uint clientIndex)
			{
			Listener = socket;
			ClientIndex = clientIndex;
			Debug.WriteLine (string.Format ("Server ({2}, {3}): Slave socket created for clientId: {0} at {1}", clientIndex, _disposed != 0 || Listener._disposed != 0 ? "<unknown>" : InternalRemoteEndPoint.ToString (), Listener.LocalEndPointDebug, _disposed != 0 || Listener._disposed != 0 ? "<unknown>" : Listener.Server.EthernetAdapterToBindTo.ToString ()));
			}

		private CrestronServerSocket ()
			{
			}

		internal bool Disposed
			{
			get { return _disposed != 0; }
			set { _disposed = value ? 1 : 0; }
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
				var listener = Listener;
				var server = listener == null ? null : listener.Server;

				if (_disposed != 0 || listener == null || listener._disposed != 0 || server == null)
					return false;

				lock (listener.SyncLock)
					{
					if (_disposed != 0 || listener._disposed != 0 || server.GetServerSocketStatusForSpecificClient (ClientIndex) == SocketStatus.SOCKET_STATUS_SOCKET_NOT_EXIST)
						return false;

					return server.ClientConnected (ClientIndex);
					}
				}
			}

		public override bool DataAvailable
			{
			get
				{
				var listener = Listener;
				var server = listener == null ? null : listener.Server;

				CheckDisposed ();

// ReSharper disable PossibleNullReferenceException
				return server.GetIfDataAvailableForSpecificClient (ClientIndex);
// ReSharper restore PossibleNullReferenceException
				}
			}

		protected override void CheckDisposed ()
			{
			var listener = Listener;
			var server = listener == null ? null : listener.Server;

			if (_disposed != 0 ||listener == null || listener._disposed != 0 || server == null)
				throw new ObjectDisposedException (GetType ().FullName);
			}

		public override void Close ()
			{
			var listener = Listener;
			var server = listener == null ? null : listener.Server;

// ReSharper disable PossibleNullReferenceException
			Debug.WriteLine (String.Format ("Server ({5}, {6}): Close for client index: {0} [endpoint = {4}, waitingClose = {1}, pendingSend = {2}, pendingReceive = {3}]", ClientIndex, WaitingClose, PendingSend, PendingReceive, _disposed != 0 || listener._disposed != 0 ? "<unknown>" : InternalRemoteEndPoint.ToString (), listener != null ? listener.LocalEndPointDebug.ToString () : "<unknown>", listener != null && listener._disposed == 0 ? server.EthernetAdapterToBindTo.ToString () : "<unknown>"));
// ReSharper restore PossibleNullReferenceException

			if (_disposed != 0 || WaitingClose)
				return;

			lock (this)
				{
				if (_disposed != 0 || WaitingClose)
					return;

				if (PendingReceive || PendingSend)
					{
					WaitingClose = true;
					return;
					}
				}

			base.Close ();
			}

		private void CheckForClose ()
			{
			var listener = Listener;
			var server = listener == null ? null : listener.Server;

// ReSharper disable PossibleNullReferenceException
			Debug.WriteLine (String.Format ("Server ({4}, {5}): CheckForClose for client index: {0} [waitingClose = {1}, pendingSend = {2}, pendingReceive = {3}]", ClientIndex, WaitingClose, PendingSend, PendingReceive, listener.LocalEndPointDebug, server.EthernetAdapterToBindTo));
// ReSharper restore PossibleNullReferenceException

			if (!WaitingClose || PendingSend || PendingReceive)
				return;

			lock (this)
				{
				if (!WaitingClose || PendingSend || PendingReceive)
					return;

				WaitingClose = false;
				}

			listener.CloseServerSocket (this);
			}

		private IPEndPoint _remoteEndpoint;
		public override IPEndPoint RemoteEndPoint
			{
			get
				{
				var listener = Listener;
				var server = listener == null ? null : listener.Server;

				CheckDisposed ();

				if (_remoteEndpoint != null)
					return _remoteEndpoint;

// ReSharper disable PossibleNullReferenceException
				lock (listener.SyncLock)
// ReSharper restore PossibleNullReferenceException
					{
// ReSharper disable PossibleNullReferenceException
					if (_disposed != 0 || listener._disposed != 0 || server.GetServerSocketStatusForSpecificClient (ClientIndex) == SocketStatus.SOCKET_STATUS_SOCKET_NOT_EXIST)
// ReSharper restore PossibleNullReferenceException
						throw new ObjectDisposedException (GetType ().FullName);

					return _remoteEndpoint = new IPEndPoint (IPAddress.Parse (server.GetAddressServerAcceptedConnectionFromForSpecificClient (ClientIndex)), server.GetPortNumberServerAcceptedConnectionFromForSpecificClient (ClientIndex));
					}
				}
			}

		internal override IPEndPoint InternalRemoteEndPoint
			{
			get
				{
				var listener = Listener;
				var server = listener == null ? null : listener.Server;

				if (_remoteEndpoint != null)
					return _remoteEndpoint;

				if (listener == null)
					return new IPEndPoint (IPAddress.Any, 0);

				lock (listener.SyncLock)
					{
					if (_disposed != 0 || listener._disposed != 0 || server == null || server.GetServerSocketStatusForSpecificClient (ClientIndex) == SocketStatus.SOCKET_STATUS_SOCKET_NOT_EXIST)
						return new IPEndPoint (IPAddress.Any, 0);

					return _remoteEndpoint = new IPEndPoint (IPAddress.Parse (listener.Server.GetAddressServerAcceptedConnectionFromForSpecificClient (ClientIndex)), server.GetPortNumberServerAcceptedConnectionFromForSpecificClient (ClientIndex));
					}
				}
			}

		private IPEndPoint _localEndpoint;
		public override IPEndPoint LocalEndPoint
			{
			get
				{
				var listener = Listener;
				var server = listener == null ? null : listener.Server;

				if (_localEndpoint != null)
					return _localEndpoint;

				CheckDisposed ();

// ReSharper disable PossibleNullReferenceException
				lock (listener.SyncLock)
// ReSharper restore PossibleNullReferenceException
					{
					if (_disposed != 0 || listener._disposed != 0)
						throw new ObjectDisposedException (GetType ().FullName);

// ReSharper disable PossibleNullReferenceException
					if (server.GetServerSocketStatusForSpecificClient (ClientIndex) == SocketStatus.SOCKET_STATUS_SOCKET_NOT_EXIST)
// ReSharper restore PossibleNullReferenceException
						throw new ObjectDisposedException (GetType ().FullName);

					var localAddress = server.GetLocalAddressServerAcceptedConnectionFromForSpecificClient (ClientIndex);
					if (localAddress == String.Empty)
						localAddress = CrestronEthernetHelper.GetEthernetParameter (CrestronEthernetHelper.ETHERNET_PARAMETER_TO_GET.GET_CURRENT_IP_ADDRESS, 0);

					return _localEndpoint = new IPEndPoint (IPAddress.Parse (localAddress), server.PortNumber);
					}
				}
			}

		public override bool Nagle
			{
			get
				{
				var listener = Listener;

				CheckDisposed ();

				return listener.Nagle;
				}
			set
				{
				var listener = Listener;

				CheckDisposed ();

				if (value != listener.Nagle)
					throw new NotSupportedException ("Cannot change server socket Nagle value");
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

		public override int Send (byte[] buffer, int offset, int size, SocketFlags socketFlags)
			{
			var listener = Listener;
			var server = listener == null ? null : listener.Server;

			Debug.WriteLine (String.Format ("Server ({3}, {4}): Send [offset = {2}, size = {1}] for clientId: {0}", ClientIndex, size, offset, listener == null ? "<unlnown>" : listener.LocalEndPointDebug.ToString (), server == null ? "<unknown>" : server.EthernetAdapterToBindTo.ToString ()));

			CheckDisposed ();

			if (_shutdown.HasValue && (_shutdown == SocketShutdown.Send || _shutdown == SocketShutdown.Both))
				throw new SocketException (SocketError.Shutdown);

			if (buffer == null)
				throw new ArgumentNullException ("buffer");

			if (offset < 0 || offset > buffer.Length)
				throw new ArgumentOutOfRangeException ("offset");

			if (size < 0 || size > buffer.Length - offset)
				throw new ArgumentOutOfRangeException ("size");

			if (size == 0)
				return 0;

// ReSharper disable PossibleNullReferenceException
			server.SocketSendOrReceiveTimeOutInMs = SendTimeout;
// ReSharper restore PossibleNullReferenceException

			SocketErrorCodes result = server.SendData (ClientIndex, buffer, offset, size);

			server.SocketSendOrReceiveTimeOutInMs = 0;

			if (result != SocketErrorCodes.SOCKET_OK)
				throw new SocketException (result.ToError ());

			UpdateSentData (size);

			return size;
			}

		public override int SendTo (byte[] buffer, int offset, int size, SocketFlags socketFlags, IPEndPoint remoteEp)
			{
			return Send (buffer, offset, size, socketFlags);
			}

		public override int Receive (byte[] buffer, int offset, int size, SocketFlags socketFlags)
			{
			var listener = Listener;
			var server = listener == null ? null : listener.Server;

			Debug.WriteLine (string.Format ("Server ({3}, {4}): Receive [offset = {2}, size = {1}] for client: {0}", ClientIndex, size, offset, listener == null ? "<unlnown>" : listener.LocalEndPointDebug.ToString (), server == null ? "<unknown>" : server.EthernetAdapterToBindTo.ToString ()));

			if (_dataBuffer.Count == 0)
				CheckDisposed ();

			if (_shutdown.HasValue && (_shutdown == SocketShutdown.Receive || _shutdown == SocketShutdown.Both))
				throw new SocketException (SocketError.Shutdown);

			if (buffer == null)
				throw new ArgumentNullException ("buffer");

			if (offset < 0 || offset > buffer.Length)
				throw new ArgumentOutOfRangeException ("offset");

			if (size < 0 || size > buffer.Length - offset)
				throw new ArgumentOutOfRangeException ("size");

			if (size == 0)
				return 0;

			if (_dataBuffer.Count == 0)
				{
// ReSharper disable PossibleNullReferenceException
				lock (listener.SyncLock)
// ReSharper restore PossibleNullReferenceException
					{
// ReSharper disable PossibleNullReferenceException
					if (_finishing || server.GetServerSocketStatusForSpecificClient (ClientIndex) == SocketStatus.SOCKET_STATUS_NO_CONNECT)
// ReSharper restore PossibleNullReferenceException
						{
// ReSharper disable PossibleNullReferenceException
						Debug.WriteLine (String.Format ("Server ({1}, {2}): Receive data [0] for client {0}", ClientIndex, listener.LocalEndPointDebug, server.EthernetAdapterToBindTo));
// ReSharper restore PossibleNullReferenceException

						((IDisposable)this).Dispose ();

						return 0;
						}
					}

				CheckDisposed ();

				server.SocketSendOrReceiveTimeOutInMs = ReceiveTimeout;

				int length = server.ReceiveData (ClientIndex);
				Debug.WriteLine (String.Format ("Server ({2}, {3}): Receive data [{0}] for client {1}", length, ClientIndex, listener.LocalEndPointDebug, server.EthernetAdapterToBindTo));

				server.SocketSendOrReceiveTimeOutInMs = 0;

				if (length == 0)
					{
					Debug.WriteLine (String.Format ("Server ({1}, {2}): Receive data [0] for client {0}", ClientIndex, listener.LocalEndPointDebug, server.EthernetAdapterToBindTo));

					((IDisposable)this).Dispose ();

					return 0;
					}

				lock (listener.SyncLock)
					{
					_dataBuffer.AddRange (server.GetIncomingDataBufferForSpecificClient (ClientIndex).Take (length));
					}
				}

			int retLength = _dataBuffer.Count <= size ? _dataBuffer.Count : size;

			Buffer.BlockCopy (_dataBuffer.ToArray (), 0, buffer, offset, retLength);

			_dataBuffer.RemoveRange (0, retLength);

			Debug.WriteLine (String.Format ("Server ({2}, {3}): Returning receive data [{0}] for client {1}", retLength, ClientIndex, listener == null ? "<unlnown>" : listener.LocalEndPointDebug.ToString (), server == null ? "<unknown>" : server.EthernetAdapterToBindTo.ToString ()));

			return retLength;
			}

		private class AsyncSendState
			{
			public readonly SocketSendAsyncResult AsyncResult;
			public readonly AsyncCallback AsyncCallback;

			public AsyncSendState (SocketSendAsyncResult asyncResult, AsyncCallback asyncCallback)
				{
				AsyncResult = asyncResult;
				AsyncCallback = asyncCallback;
				}
			}

		public override IAsyncResult BeginSend (byte[] buffer, int offset, int size, SocketFlags socketFlags, AsyncCallback callback, Object state)
			{
			var listener = Listener;
			var server = listener == null ? null : listener.Server;

			Debug.WriteLine (string.Format ("Server ({1}, {2}): BeginSend for clientId: {0}", ClientIndex, listener == null ? "<unlnown>" : listener.LocalEndPointDebug.ToString (), server == null ? "<unknown>" : server.EthernetAdapterToBindTo.ToString ()));

			CheckDisposed ();

			if (_shutdown.HasValue && (_shutdown == SocketShutdown.Send || _shutdown == SocketShutdown.Both))
				throw new SocketException (SocketError.Shutdown);

			if (buffer == null)
				throw new ArgumentNullException ("buffer");

			if (offset < 0 || offset > buffer.Length)
				throw new ArgumentOutOfRangeException ("offset");

			if (size < 0 || size > buffer.Length - offset)
				throw new ArgumentOutOfRangeException ("size");

			var ssir = new SocketSendAsyncResult { AsyncState = state };

			if (size == 0)
				{
				ssir.CompletedSynchronously = true;
				((CEvent)ssir.AsyncWaitHandle).Set ();
				ssir.IsCompleted = true;
				ssir.ErrorCode = SocketErrorCodes.SOCKET_OK;
				if (callback != null)
					DoAsyncCallback (callback, ssir);
				}
			else
				{
// ReSharper disable PossibleNullReferenceException
				lock (listener.SyncLock)
// ReSharper restore PossibleNullReferenceException
					{
// ReSharper disable PossibleNullReferenceException
					if (_disposed != 0 || listener._disposed != 0 || server.GetServerSocketStatusForSpecificClient (ClientIndex) == SocketStatus.SOCKET_STATUS_SOCKET_NOT_EXIST)
// ReSharper restore PossibleNullReferenceException
						throw new ObjectDisposedException (GetType ().FullName);

					PendingSend = true;

					var result = server.SendDataAsync (ClientIndex, buffer, offset, size, AsyncSendComplete, new AsyncSendState (ssir, callback));

					if (result != SocketErrorCodes.SOCKET_OK && result != SocketErrorCodes.SOCKET_OPERATION_PENDING)
						{
						PendingSend = false;

						throw new SocketException (result.ToError ());
						}
					}
				}

			return ssir;
			}

		public override int EndSend (IAsyncResult asyncResult)
			{
			var listener = Listener;
			var server = listener == null ? null : listener.Server;

			Debug.WriteLine (string.Format ("Server ({1}, {2}): EndSend for clientId: {0}", ClientIndex, listener == null ? "<unknown>" : listener.LocalEndPointDebug.ToString (), server == null ? "<unknown>" : server.EthernetAdapterToBindTo.ToString ()));

			CheckDisposed ();

			if (asyncResult == null)
				throw new ArgumentNullException ("asyncResult");

			var ssir = asyncResult as SocketSendAsyncResult;

			if (ssir == null)
				throw new ArgumentException ("asyncResult");

			if (ssir.EndsendCalled)
				throw new InvalidOperationException ("EndSend already called");
			ssir.EndsendCalled = true;

			if (!ssir.CompletedSynchronously)
				ssir.AsyncWaitHandle.Wait ();

			if (ssir.ErrorCode == SocketErrorCodes.SOCKET_NOT_CONNECTED)
				{
				((IDisposable)this).Dispose ();
				}

			if (ssir.ErrorCode != SocketErrorCodes.SOCKET_OK)
				throw new SocketException (ssir.ErrorCode.ToError ());

			return ssir.DataSent;
			}

		private void AsyncSendComplete (TCPServer cbServer, uint user, int length, object state)
			{
			Debug.WriteLine (string.Format ("Server ({2}, {3}): AsyncSendComplete for clientId: {0} [{1}]", user, length, CrestronListenerSocket.GetLocalEndPointDebug(cbServer), cbServer.EthernetAdapterToBindTo));

			PendingSend = false;

			CheckForClose ();

			var ass = (AsyncSendState)state;
			var iar = ass.AsyncResult;
			var cb = ass.AsyncCallback;

			iar.IsCompleted = true;
			((CEvent)iar.AsyncWaitHandle).Set ();
			iar.DataSent = length;

			if (length > 0)
				{
				iar.ErrorCode = SocketErrorCodes.SOCKET_OK;

				UpdateSentData (length);
				}
			else
				{
				iar.ErrorCode = SocketErrorCodes.SOCKET_NOT_CONNECTED;
				iar.Status = SocketStatus.SOCKET_STATUS_BROKEN_REMOTELY;
				}

			if (cb != null)
				DoAsyncCallback (cb, iar);
			}

		private class AsyncReceiveState
			{
			public readonly SocketReceiveAsyncResult AsyncResult;
			public readonly AsyncCallback AsyncCallback;
			public readonly byte[] Buffer;
			public readonly int Offset;
			public readonly int ReqLength;

			public AsyncReceiveState (SocketReceiveAsyncResult asyncResult, AsyncCallback asyncCallback, byte[] buffer, int offset, int reqLength)
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
			var listener = Listener;
			var server = listener == null ? null : listener.Server;

			Debug.WriteLine (string.Format ("Server ({3}, {4}): BeginReceive [offset = {2}, size = {1}] for clientId: {0}", ClientIndex, size, offset, listener == null ? "<unlnown>" : listener.LocalEndPointDebug.ToString (), server == null ? "<unknown>" : server.EthernetAdapterToBindTo.ToString ()));

			if (_dataBuffer.Count == 0)
				CheckDisposed ();

			if (_shutdown.HasValue && (_shutdown == SocketShutdown.Receive || _shutdown == SocketShutdown.Both))
				throw new SocketException (SocketError.Shutdown);

			if (buffer == null)
				throw new ArgumentNullException ("buffer");

			if (offset < 0 || offset > buffer.Length)
				throw new ArgumentOutOfRangeException ("offset");

			if (size < 0 || size > buffer.Length - offset)
				throw new ArgumentOutOfRangeException ("size");

			var srir = new SocketReceiveAsyncResult { AsyncState = state };

			if (size == 0)
				{
				srir.CompletedSynchronously = true;
				((CEvent)srir.AsyncWaitHandle).Set ();
				srir.IsCompleted = true;
				srir.ErrorCode = SocketErrorCodes.SOCKET_OK;
				if (callback != null)
					DoAsyncCallback (callback, srir);
				}
			else
				{
				if (_dataBuffer.Count == 0)
					{
					if (_finishing)
						{
						srir.CompletedSynchronously = true;
						srir.IsCompleted = true;
						((CEvent)srir.AsyncWaitHandle).Set ();
						if (callback != null)
							DoAsyncCallback (callback, srir);
						}
					else
						{
// ReSharper disable PossibleNullReferenceException
						lock (listener.SyncLock)
// ReSharper restore PossibleNullReferenceException
							{
// ReSharper disable PossibleNullReferenceException
							if (_disposed != 0 || listener._disposed != 0 || server.GetServerSocketStatusForSpecificClient (ClientIndex) == SocketStatus.SOCKET_STATUS_SOCKET_NOT_EXIST)
// ReSharper restore PossibleNullReferenceException
								throw new ObjectDisposedException (GetType ().FullName);

							PendingReceive = true;

							var result = server.ReceiveDataAsync (ClientIndex, AsyncReceiveComplete, new AsyncReceiveState (srir, callback, buffer, offset, size));

							if (result != SocketErrorCodes.SOCKET_OK && result != SocketErrorCodes.SOCKET_OPERATION_PENDING)
								{
								PendingReceive = false;

								throw new SocketException (result.ToError ());
								}
							}
						}
					}
				else
					{
					int retLength = _dataBuffer.Count <= size ? _dataBuffer.Count : size;

					Debug.WriteLine (string.Format ("Server ({2}, {3}): SyncReceiveComplete for clientId: {0} [{1}]", ClientIndex, retLength, listener == null ? "<unlnown>" : listener.LocalEndPointDebug.ToString (), server == null ? "<unknown>" : server.EthernetAdapterToBindTo.ToString ()));

					Buffer.BlockCopy (_dataBuffer.ToArray (), 0, buffer, offset, retLength);

					_dataBuffer.RemoveRange (0, retLength);

					srir.DataReceived = retLength;

					srir.CompletedSynchronously = true;
					srir.IsCompleted = true;
					((CEvent)srir.AsyncWaitHandle).Set ();
					if (callback != null)
						DoAsyncCallback (callback, srir);
					}
				}

			return srir;
			}

		private void AsyncReceiveComplete (TCPServer cbServer, uint user, int length, object state)
			{
			Debug.WriteLine (string.Format ("Server ({2}, {3}): AsyncReceiveComplete for clientId: {0} [{1}]", user, length, CrestronListenerSocket.GetLocalEndPointDebug (cbServer), cbServer.EthernetAdapterToBindTo));

			PendingReceive = false;

			CheckForClose ();

			var ars = (AsyncReceiveState)state;
			var iar = ars.AsyncResult;
			var cb = ars.AsyncCallback;
			var buff = ars.Buffer;
			var offset = ars.Offset;
			var reqLength = ars.ReqLength;

			iar.IsCompleted = true;
			((CEvent)iar.AsyncWaitHandle).Set ();
			iar.DataReceived = length;

			if (length > 0)
				{
				iar.ErrorCode = SocketErrorCodes.SOCKET_OK;

				lock (cbServer)
					{
					_dataBuffer.AddRange (cbServer.GetIncomingDataBufferForSpecificClient (user).Take (length));
					}

				int retLength = _dataBuffer.Count <= reqLength ? _dataBuffer.Count : reqLength;

				Buffer.BlockCopy (_dataBuffer.ToArray (), 0, buff, offset, retLength);

				_dataBuffer.RemoveRange (0, retLength);

				iar.DataReceived = retLength;
				}
			else
				{
				iar.ErrorCode = SocketErrorCodes.SOCKET_NOT_CONNECTED;
				iar.Status = SocketStatus.SOCKET_STATUS_BROKEN_REMOTELY;
				_finishing = true;
				}

			if (cb != null)
				DoAsyncCallback (cb, iar);
			}

		public override int EndReceive (IAsyncResult asyncResult)
			{
			var listener = Listener;
			var server = listener == null ? null : listener.Server;

			Debug.Write (string.Format ("Server ({1}, {2}): EndReceive for clientId: {0}", ClientIndex, listener == null ? "<unlnown>" : listener.LocalEndPointDebug.ToString (), server == null ? "<unknown>" : server.EthernetAdapterToBindTo.ToString ()));

			bool inhibitWriteLine = false;

			try
				{
				CheckDisposed ();

				if (asyncResult == null)
					throw new ArgumentNullException ("asyncResult");

				var srir = asyncResult as SocketReceiveAsyncResult;

				if (srir == null)
					throw new ArgumentException ("asyncResult");

				if (srir.EndreceiveCalled)
					throw new InvalidOperationException ("EndReceive already called");
				srir.EndreceiveCalled = true;

				if (!srir.CompletedSynchronously)
					srir.AsyncWaitHandle.Wait ();

				if (srir.DataReceived != 0)
					{
					Debug.Write (String.Format (" returns {0}", srir.DataReceived));

					return srir.DataReceived;
					}

				if (_finishing || srir.ErrorCode == SocketErrorCodes.SOCKET_NOT_CONNECTED)
					{
					Debug.WriteLine (" return 0");

					inhibitWriteLine = true;

					((IDisposable)this).Dispose ();

					return 0;
					}

				if (srir.ErrorCode != SocketErrorCodes.SOCKET_OK)
					throw new SocketException (srir.ErrorCode.ToError ());

				Debug.Write (" return 0");

				return 0;
				}
			finally
				{
				Debug.WriteLineIf (!inhibitWriteLine, "");
				}
			}

		protected override void Dispose (bool disposing)
			{
			var listener = Listener;
			var server = listener == null ? null : listener.Server;

			Debug.WriteLine (String.Format ("Server ({3}, {4}): ServerSocket Dispose for clientId: {0} ({1}) [_disposed = {2}]", ClientIndex, disposing, _disposed, listener == null ? "<unknown>" : listener.LocalEndPointDebug.ToString (), server == null ? "<unknown>" : server.EthernetAdapterToBindTo.ToString ()));

			if (disposing && _disposed == 0)
				{
				if (listener != null)
					{
					listener.CloseServerSocket (this);
					Listener = null;
					}
				}

			if (Interlocked.CompareExchange (ref _disposed, 1, 0) != 0)
				return;

			_finishing = false;

			_active = false;
			}
		}

	public class CrestronListenerSocket : CrestronSocket
		{
		private TCPServer _server;
		private bool _closing;
		private readonly object _syncLock = new object ();
		private readonly CrestronQueue<uint> _queueConnections = new CrestronQueue<uint> (1);
		//private bool _acceptCalled;

		private readonly Dictionary<uint, CrestronServerSocket> _dictServerSockets = new Dictionary<uint, CrestronServerSocket> ();

		public CrestronListenerSocket (TCPServer server)
			{
			_server = server;

			Debug.WriteLine ("Listener socket created for TCPServer listening at {0}, {1}", LocalEndPointDebug, server.EthernetAdapterToBindTo);

			_server.SocketStatusChange += server_SocketStatusChange;
			}


		public CrestronListenerSocket ()
			: this (IPAddress.Any, 0)
			{
			}

		public CrestronListenerSocket (int port)
			: this (IPAddress.Any, port)
			{
			}

		public CrestronListenerSocket (IPEndPoint endpoint)
			: this (endpoint, 16, EthernetAdapterType.EthernetUnknownAdapter)
			{
			}

		public CrestronListenerSocket (IPEndPoint endpoint, int maxConnections)
			: this (endpoint, maxConnections, EthernetAdapterType.EthernetUnknownAdapter)
			{
			}

		public CrestronListenerSocket (IPEndPoint endpoint, int maxConnections, EthernetAdapterType adapter)
			{
			Debug.WriteLine ("Listener socket created listening at {0}, {1}", endpoint, adapter);

			if (endpoint == null)
				throw new ArgumentNullException ("endpoint");

			if (endpoint.Port < IPEndPoint.MinPort || endpoint.Port > IPEndPoint.MaxPort)
				throw new ArgumentOutOfRangeException ("endpoint");

			_server = new TCPServer (endpoint, 8192, adapter, maxConnections)
				{
				Nagle = true
				};

			_server.SocketStatusChange += server_SocketStatusChange;
			}

		public CrestronListenerSocket (IPAddress ipaddress, int port)
			: this (ipaddress, port, 16, EthernetAdapterType.EthernetUnknownAdapter)
			{
			}

		public CrestronListenerSocket (IPAddress ipaddress, int port, int maxConnections)
			: this (ipaddress, port, maxConnections, EthernetAdapterType.EthernetUnknownAdapter)
			{
			}

		public CrestronListenerSocket (IPAddress ipaddress, int port, int maxConnections, EthernetAdapterType adapter)
			{
			Debug.WriteLine ("Listener socket created listening at {0}:{1}, {2}", ipaddress, port, adapter);

			if (ipaddress == null)
				throw new ArgumentNullException ("ipaddress");

			if (port < IPEndPoint.MinPort || port > IPEndPoint.MaxPort)
				throw new ArgumentOutOfRangeException ("port");

			_server = new TCPServer (ipaddress.ToString (), port, 8192, adapter, maxConnections)
				{
				Nagle = true
				};

			_server.SocketStatusChange += server_SocketStatusChange;
			}

		private void server_SocketStatusChange (TCPServer myTCPServer, uint clientIndex, SocketStatus serverSocketStatus)
			{
			Debug.WriteLine (string.Format ("Server ({2}, {3}): Socket status changed for clientIndex: {0} status: {1}", clientIndex, serverSocketStatus, LocalEndPointDebug, myTCPServer.EthernetAdapterToBindTo));
			}

		public TCPServer Server
			{
			get { return _server; }
			}

		internal object SyncLock
			{
			get { return _syncLock; }
			}

		public override bool Nagle
			{
			get { return _server.Nagle; }
			set { _server.Nagle = value; }
			}

		public bool Active
			{
			get { return _active; }
			}

		public override IPEndPoint LocalEndPoint
			{
			get
				{
				CheckDisposed ();

				lock (_syncLock)
					{
					CheckDisposed ();

					return new IPEndPoint (IPAddress.Parse (CrestronEthernetHelper.GetEthernetParameter (CrestronEthernetHelper.ETHERNET_PARAMETER_TO_GET.GET_CURRENT_IP_ADDRESS, 0)), _server.PortNumber);
					}
				}
			}

		internal IPEndPoint LocalEndPointDebug
			{
			get
				{
				return _server != null && _server.AddressToAcceptConnectionFrom != null ? new IPEndPoint (IPAddress.Parse (_server.AddressToAcceptConnectionFrom), _server.PortNumber) : new IPEndPoint (IPAddress.None, 0);
				}
			}

		internal static IPEndPoint GetLocalEndPointDebug (TCPServer s)
			{
			return s != null && s.AddressToAcceptConnectionFrom != null ? new IPEndPoint (IPAddress.Parse (s.AddressToAcceptConnectionFrom), s.PortNumber) : new IPEndPoint (IPAddress.None, 0);
			}

		public override void Close ()
			{
			Debug.WriteLine (String.Format ("Listener socket closed [address = {2}, port = {3}, adapter = {4},  disposed = {0}, closing = {1}]", _disposed, _closing, _server.AddressToAcceptConnectionFrom, _server.PortNumber, _server.EthernetAdapterToBindTo));

			if (_closing || _disposed != 0)
				return;

			lock (_syncLock)
				{
				if (_closing || _disposed != 0)
					return;

				_closing = true;

				Debug.WriteLine (String.Format ("     TCPServer.State = {0}", _server.State));

				_server.Stop ();

				_active = false;

				//_acceptCalled = false;

				uint cix;
				while (_queueConnections.TryToDequeue (out cix) && cix != 0 && cix != UInt32.MaxValue)
					try
						{
						_server.Disconnect (cix);
						}
// ReSharper disable EmptyGeneralCatchClause
					catch
// ReSharper restore EmptyGeneralCatchClause
						{
						}

				_queueConnections.Enqueue (UInt32.MaxValue);

				KeyValuePair<uint, CrestronServerSocket>[] dictServerEntries;
				lock (_dictServerSockets)
					{
					dictServerEntries = _dictServerSockets.ToArray ();
					}

				foreach (var kvp in dictServerEntries)
					{
					Debug.WriteLine (String.Format ("     Server ({6}, {7}): Closing client index: {0} [endpoint = {4}, waitingClose = {1}, pendingSend = {2}, pendingReceive = {3}, disposed = {5}]", kvp.Key, kvp.Value.WaitingClose, kvp.Value.PendingSend, kvp.Value.PendingReceive, kvp.Value.Disposed ? "<unknown>" : kvp.Value.InternalRemoteEndPoint.ToString (), kvp.Value.Disposed, LocalEndPointDebug, _server.EthernetAdapterToBindTo));

					kvp.Value.Close ();
					}
				}

			CheckForInactiveListener ();
			}

		private int _backlog;

		public void Start (int backlog)
			{
			if (_disposed != 0 || _closing)
				throw new ObjectDisposedException (GetType ().FullName);

			if (_active)
				return;

			if (backlog < 0)
				throw new ArgumentOutOfRangeException ("backlog");

			if (backlog > _server.MaxNumberOfClientSupported)
				backlog = _server.MaxNumberOfClientSupported;

			_backlog = backlog;

			_active = true;

			//_acceptCalled = false;

			SocketErrorCodes result;

			Debug.WriteLine (String.Format ("Listener socket started [address = {0}, port = {1}, adapter = {3},  backlog = {2}]", _server.AddressToAcceptConnectionFrom,
													  _server.PortNumber, _backlog, _server.EthernetAdapterToBindTo));

			if (_server.PortNumber == 0)
				{
				for (_server.PortNumber = 1024; _server.PortNumber <= 5000; ++_server.PortNumber)
					{
					result = _server.WaitForConnectionAsync (DoConnection, null);
					if (result == SocketErrorCodes.SOCKET_SPECIFIED_PORT_ALREADY_IN_USE)
						{
						_server.Stop ();
						continue;
						}

					Debug.WriteLine (String.Format ("     TCPServer.WaitForConnectionAsync ({0}:{1}, DoConnection) on {3} returns {2}", _server.AddressToAcceptConnectionFrom, _server.PortNumber, result, _server.EthernetAdapterToBindTo));

					if (result != SocketErrorCodes.SOCKET_OK /*&& result != SocketErrorCodes.SOCKET_NOT_CONNECTED*/ && result != SocketErrorCodes.SOCKET_OPERATION_PENDING)
						throw new SocketException (result.ToError ());

					break;
					}

				if (_server.PortNumber > 5000)
					throw new SocketException (SocketError.TooManyOpenSockets);
				}
			else
				{
				Debug.Write (String.Format ("     TCPServer.WaitForConnectionAsync ({0}:{1}, DoConnection) on {2}", _server.AddressToAcceptConnectionFrom, _server.PortNumber, _server.EthernetAdapterToBindTo));

				result = _server.WaitForConnectionAsync (DoConnection, null);

				Debug.WriteLine (String.Format (" returns {0}", result));

				if (result != SocketErrorCodes.SOCKET_OK /*&& result != SocketErrorCodes.SOCKET_NOT_CONNECTED*/ && result != SocketErrorCodes.SOCKET_OPERATION_PENDING)
					throw new SocketException (result.ToError ());
				}
			}

		public void Start ()
			{
			Start (0);
			}

		public void Listen ()
			{
			Start ();
			}

		public void Listen (int backlog)
			{
			Start (backlog);
			}

		public override void Bind (IPEndPoint localEp)
			{
			_server.PortNumber = localEp.Port;
			}

		private void DoConnection (TCPServer s, uint index, object state)
			{
			if (index == 0)
				return;

			if (_backlog != 0 && _queueConnections.Count >= _backlog)
				{
				Debug.WriteLine ("     Server ({0}, {3}): Backlog exceeded - disconnecting {1}:{2}", GetLocalEndPointDebug (s), index == 0 ? "<unknown>" : s.GetAddressServerAcceptedConnectionFromForSpecificClient (index), index == 0 ? "<unlnown>" : s.GetPortNumberServerAcceptedConnectionFromForSpecificClient (index).ToString (CultureInfo.InvariantCulture), s.EthernetAdapterToBindTo);

				lock (_syncLock)
					{
					s.Disconnect (index);
					}
				}
			else
				{
				Debug.WriteLine ("     Server ({0}, {4}): Queuing clientIndex {3} [{1}:{2}]", GetLocalEndPointDebug (s), index == 0 ? "<unknown>" : s.GetAddressServerAcceptedConnectionFromForSpecificClient (index), index == 0 ? "<unknown>" : s.GetPortNumberServerAcceptedConnectionFromForSpecificClient (index).ToString (CultureInfo.InvariantCulture), index, s.EthernetAdapterToBindTo);

				_queueConnections.Enqueue (index);
				}

			if (!_closing && _disposed == 0)
				{
				Debug.Write (String.Format ("     TCPServer.WaitForConnectionAsync ({0}:{1}, DoConnection) on {2}", _server.AddressToAcceptConnectionFrom, _server.PortNumber, _server.EthernetAdapterToBindTo));

				var result = _server.WaitForConnectionAsync (DoConnection, null);

				Debug.WriteLine (String.Format (" returns {0}", result));

				if (result != SocketErrorCodes.SOCKET_OK /*&& result != SocketErrorCodes.SOCKET_NOT_CONNECTED*/ && result != SocketErrorCodes.SOCKET_OPERATION_PENDING)
					throw new SocketException (result.ToError ());
				}
			}

		public bool Pending ()
			{
			if (!_active)
				throw new InvalidOperationException ("start not called");

			return !_queueConnections.IsEmpty;
			}

		public void Stop ()
			{
			Debug.WriteLine (String.Format ("Listener socket stopped [address = {2}, port = {3}, adapter = {4}, count = {4}, disposed = {0}, closing = {1}]", _disposed, _closing, _server.AddressToAcceptConnectionFrom, _server.PortNumber, _dictServerSockets.Count, _server.EthernetAdapterToBindTo));

			if (_disposed != 0 || _closing)
				throw new ObjectDisposedException (GetType ().FullName);

			_server.Stop ();

			_active = false;

			//_acceptCalled = false;

			uint cix;
			while (_queueConnections.TryToDequeue(out cix) && cix != 0 && cix != UInt32.MaxValue)
				try
					{
					_server.Disconnect (cix);
					}
// ReSharper disable EmptyGeneralCatchClause
				catch
// ReSharper restore EmptyGeneralCatchClause
					{
					}

			_queueConnections.Enqueue (UInt32.MaxValue);
			}

		private void CheckForInactiveListener ()
			{
			Debug.WriteLine (String.Format ("     Server ({4}, {5}): CheckforInactiveListener [count = {0}, queued = {3} disposed = {1}, closing = {2}]", _dictServerSockets.Count, _disposed, _closing, _queueConnections.Count, LocalEndPointDebug, _server.EthernetAdapterToBindTo));

			if (_disposed != 0 || !_closing || _dictServerSockets.Count != 0 || (!_queueConnections.IsEmpty && _queueConnections.Peek () != UInt32.MaxValue))
				return;

			lock (_syncLock)
				{
				if (_disposed != 0 || !_closing || _dictServerSockets.Count != 0 || (!_queueConnections.IsEmpty && _queueConnections.Peek () != UInt32.MaxValue))
					return;

				try
					{
					_disposed = 1;
					_closing = false;

					Debug.WriteLine ("     TCPServer.DisconnectAll ()");

					_server.DisconnectAll ();
					}
// ReSharper disable EmptyGeneralCatchClause
				catch (Exception)
// ReSharper restore EmptyGeneralCatchClause
					{
					}

				_server.SocketStatusChange -= server_SocketStatusChange;
				}
			}

		internal void CloseServerSocket (CrestronServerSocket serverSocket)
			{
			Debug.WriteLine (string.Format ("Server ({7}, {8}): ServerSocket closed for client index: {0} [endpoint = {6}, waitingClose = {1}, pendingSend = {2}, pendingReceive = {3}, disposed = {4}, closing = {5}]", serverSocket.ClientIndex, serverSocket.WaitingClose, serverSocket.PendingSend, serverSocket.PendingReceive, _disposed, _closing, serverSocket.Disposed || _disposed != 0 || _server.GetServerSocketStatusForSpecificClient (serverSocket.ClientIndex) == SocketStatus.SOCKET_STATUS_SOCKET_NOT_EXIST ? "<unknown>" : serverSocket.InternalRemoteEndPoint.ToString (), LocalEndPointDebug, _server.EthernetAdapterToBindTo));

			try
				{
				if (serverSocket.Disposed || _disposed != 0 || _closing)
					return;

				lock (_syncLock)
					{
					if (serverSocket.Disposed || _disposed != 0 || _closing)
						return;

					serverSocket.Disposed = true;

					if (_server.GetServerSocketStatusForSpecificClient (serverSocket.ClientIndex) != SocketStatus.SOCKET_STATUS_CONNECTED)
						{
						Debug.WriteLine (String.Format ("     TCPServer.GetServerSocketStatusForSpecificClient ({0}, {2}) = {1}", serverSocket.ClientIndex, _server.GetServerSocketStatusForSpecificClient (serverSocket.ClientIndex), _server.EthernetAdapterToBindTo));

						//return;
						}

					try
						{
						Debug.WriteLine (string.Format ("     TCPServer.Disconnect ({0})", serverSocket.ClientIndex));

						_server.Disconnect (serverSocket.ClientIndex);
						}
// ReSharper disable EmptyGeneralCatchClause
					catch
// ReSharper restore EmptyGeneralCatchClause
						{
						}
					}
				}
			finally
				{
				if (!serverSocket.WaitingClose)
					RemoveServerSocket (serverSocket);
				}
			}

		internal void RemoveServerSocket (CrestronServerSocket serverSocket)
			{
			Debug.WriteLine (String.Format ("     Server ({1}, {2}): RemoveServerSocket (clientIndex = {0})", serverSocket.ClientIndex, LocalEndPointDebug, _server.EthernetAdapterToBindTo));

			lock (_dictServerSockets)
				{
				_dictServerSockets.Remove (serverSocket.ClientIndex);
				}

			CheckForInactiveListener ();
			}

		public CrestronServerSocket AcceptSocket ()
			{
			return Accept ();
			}

		public CrestronServerSocket AcceptTcpClient ()
			{
			return Accept ();
			}

		public CrestronServerSocket Accept ()
			{
			Debug.WriteLine (String.Format ("Server ({0}, {1}): Accept", LocalEndPointDebug, _server.EthernetAdapterToBindTo));

			CheckDisposed ();

			if (_closing)
				throw new SocketException (SocketError.NotConnected);

			TCPServer server;
			lock (_syncLock)
				{
				CheckDisposed ();

				server = _server;
				}

			if (!_active)
				throw new InvalidOperationException ("start or listen have not been called");

			uint newClientIndex = 0;

			//if (!_acceptCalled || !queueConnections.IsEmpty)
				{
				//_acceptCalled = true;

				Debug.WriteLine ("     Server ({0}:{1}, {2}) : Waiting for queue", server.AddressToAcceptConnectionFrom, server.PortNumber, server.EthernetAdapterToBindTo);
				try
					{
					newClientIndex = _queueConnections.Dequeue ();

					Debug.WriteLine ("     Server ({0}:{1}, {2}) : Got queue [clientIndex = {2}]", server.AddressToAcceptConnectionFrom, server.PortNumber, newClientIndex, server.EthernetAdapterToBindTo);
					}
				catch (Exception ex)
					{
					Debug.WriteLine ("     Server ({0}:{1}, {2}) : Exception while dequeuing: {3}", server.AddressToAcceptConnectionFrom, server.PortNumber, newClientIndex, server.EthernetAdapterToBindTo, ex.Message);
					}

				}
			/*
			else
				{
				Debug.WriteLine ("     TCPServer.WaitForConnection ({0}:{1}, {2})", server.AddressToAcceptConnectionFrom, server.PortNumber, server.EthernetAdapterToBindTo);

				var result = server.WaitForConnection (out newClientIndex);

				Debug.WriteLine ("     TCPServer.WaitForConnection returns {0}", result);

				if (result != SocketErrorCodes.SOCKET_OK)
					{
					throw new SocketException (result.ToError ());
					}
				}
			*/

			CheckDisposed ();

			if (newClientIndex == 0 || newClientIndex == UInt32.MaxValue)
				throw new SocketException (SocketError.SocketError);

			var ss = new CrestronServerSocket (this, newClientIndex);

			lock (_dictServerSockets)
				{
				CrestronServerSocket oldSocket;
				if (_dictServerSockets.TryGetValue (newClientIndex, out oldSocket))
					{
					oldSocket.Listener = null;
					_dictServerSockets.Remove (newClientIndex);
					}
				_dictServerSockets.Add (newClientIndex, ss);
				}

			return ss;
			}

		private class AsyncAcceptState
			{
			public readonly SocketAcceptAsyncResult AsyncResult;
			public readonly AsyncCallback AsyncCallback;

			public AsyncAcceptState (SocketAcceptAsyncResult asyncResult, AsyncCallback asyncCallback)
				{
				AsyncResult = asyncResult;
				AsyncCallback = asyncCallback;
				}
			}

		public IAsyncResult BeginAcceptSocket (AsyncCallback callback, Object state)
			{
			return BeginAccept (callback, state);
			}

		public IAsyncResult BeginAcceptTcpClient (AsyncCallback callback, Object state)
			{
			return BeginAccept (callback, state);
			}

		public IAsyncResult BeginAccept (AsyncCallback callback, Object state)
			{
			Debug.WriteLine (String.Format ("Server ({0}, {1}): BeginAccept", LocalEndPointDebug, _server.EthernetAdapterToBindTo));

			CheckDisposed ();

			if (_closing)
				throw new SocketException (SocketError.NotConnected);

			if (!_active)
				throw new InvalidOperationException ("start or listen not called");

			var sair = new SocketAcceptAsyncResult
				{
					AsyncState = state
				};

			lock (_syncLock)
				{
				CheckDisposed ();

				//if (!_acceptCalled || !queueConnections.IsEmpty)
					{
					//_acceptCalled = true;

					uint newClientId;
					if (_queueConnections.TryToDequeue (out newClientId))
						{
						Debug.WriteLine (String.Format ("     Server ({0}:{1}, {5}): Dequeuing [index = {2}, remoteEndpoint = {3}:{4}]", _server.AddressToAcceptConnectionFrom,
							_server.PortNumber, newClientId, newClientId == 0 ? "<unknown>" : _server.GetAddressServerAcceptedConnectionFromForSpecificClient (newClientId), newClientId == 0 ? "<unknown>" :  newClientId == UInt32.MaxValue ? "<shutting down>" :
															 _server.GetPortNumberServerAcceptedConnectionFromForSpecificClient (newClientId).ToString (CultureInfo.InvariantCulture), _server.EthernetAdapterToBindTo));

						sair.CompletedSynchronously = true;
						AsyncAcceptComplete (_server, newClientId, new AsyncAcceptState (sair, callback));
						}
					else
						{
						Debug.WriteLine (String.Format ("     Server ({0}:{1}, {2}): Waiting for queue", _server.AddressToAcceptConnectionFrom, _server.PortNumber, _server.EthernetAdapterToBindTo));

						ThreadPool.QueueUserWorkItem (o =>
							{
							try
								{
								uint index = _queueConnections.Dequeue ();

								Debug.WriteLine (String.Format ("     Server ({0}:{1}, {5}): Dequeuing [index = {2}, remoteEndpoint = {3}:{4}]", _server.AddressToAcceptConnectionFrom,
									_server.PortNumber, index, index == 0 ? "<unknown>" : _server.GetAddressServerAcceptedConnectionFromForSpecificClient (index), index == 0 ? "<unknown>" : index == UInt32.MaxValue ? "<shutting down>" :
										_server.GetPortNumberServerAcceptedConnectionFromForSpecificClient (index).ToString (CultureInfo.InvariantCulture), _server.EthernetAdapterToBindTo));

								AsyncAcceptComplete (_server, index, new AsyncAcceptState (sair, callback));
								}
							catch (Exception ex)
								{
								Debug.WriteLine("     Server ({0}:{1}, {2}): Exception while waiting for queue: {3}", _server.AddressToAcceptConnectionFrom, _server.PortNumber, _server.EthernetAdapterToBindTo, ex.Message);
								}
							});
						}
					//return sair;
					}

#if false
				Debug.Write (String.Format ("     TCPServer.WaitForConnectionAsync ({0}:{1}, AsyncAcceptComplete, new Tuple<SocketAcceptAsyncResult, AsyncCallback> (sair, callback)) on {2}",
														 _server.AddressToAcceptConnectionFrom, _server.PortNumber, _server.EthernetAdapterToBindTo));

				var result = _server.WaitForConnectionAsync (AsyncAcceptComplete, new AsyncAcceptState (sair, callback));

				Debug.WriteLine (String.Format (" returns {0}", result));

				if (result != SocketErrorCodes.SOCKET_OK /*&& result != SocketErrorCodes.SOCKET_NOT_CONNECTED*/&& result != SocketErrorCodes.SOCKET_OPERATION_PENDING)
					throw new SocketException (result.ToError ());
#endif
				}

			return sair;
			}

		public CrestronServerSocket EndAcceptSocket (IAsyncResult asyncResult)
			{
			return EndAccept (asyncResult);
			}

		public CrestronServerSocket EndAcceptTcpClient (IAsyncResult asyncResult)
			{
			return EndAccept (asyncResult);
			}

		public CrestronServerSocket EndAccept (IAsyncResult asyncResult)
			{
			Debug.WriteLine (String.Format ("Server ({0}, {1}): EndAccept", LocalEndPointDebug, _server.EthernetAdapterToBindTo));

			CheckDisposed ();

			if (asyncResult == null)
				throw new ArgumentNullException ("asyncResult");

			var saar = asyncResult as SocketAcceptAsyncResult;

			if (saar == null)
				throw new ArgumentException ("asyncResult");

			if (saar.EndacceptCalled)
				throw new InvalidOperationException ("EndAccept already called");
			saar.EndacceptCalled = true;

			saar.AsyncWaitHandle.Wait ();

			if (saar.Socket == null)
				throw new SocketException (SocketError.SocketError, "Error during accept");

			return saar.Socket;
			}

		private void AsyncAcceptComplete (TCPServer cbServer, uint newClientId, object state)
			{
			Debug.WriteLine (String.Format (newClientId != 0 ? "Server ({3}, {4}): AsyncAcceptComplete for clientIndex {0} from {1}:{2}" : "AsyncAcceptComplete for clientIndex {0}", newClientId, cbServer.GetAddressServerAcceptedConnectionFromForSpecificClient (newClientId), cbServer.GetPortNumberServerAcceptedConnectionFromForSpecificClient (newClientId), GetLocalEndPointDebug(cbServer), cbServer.EthernetAdapterToBindTo));

			var aas = (AsyncAcceptState)state;
			var iar = aas.AsyncResult;
			var cb = aas.AsyncCallback;

			iar.IsCompleted = true;
			((CEvent)iar.AsyncWaitHandle).Set ();

			if (newClientId != 0 && newClientId != UInt32.MaxValue)
				{
				iar.Socket = new CrestronServerSocket (this, newClientId);

				lock (_dictServerSockets)
					{
					CrestronServerSocket oldSocket;
					if (_dictServerSockets.TryGetValue (newClientId, out oldSocket))
						{
						oldSocket.Listener = null;
						_dictServerSockets.Remove (newClientId);
						}

					_dictServerSockets.Add (newClientId, iar.Socket);
					}
				}

			if (cb != null)
				DoAsyncCallback (cb, iar);
			}

		#region IDisposable Members

		~CrestronListenerSocket ()
			{
			Dispose (false);
			}

		protected override void Dispose (bool disposing)
			{
			Debug.WriteLine (String.Format ("Server ({2}): ServerListenerSocket Dispose ({0}) [disposed = {1}]", disposing, _disposed, LocalEndPointDebug));

			if (Interlocked.CompareExchange (ref _disposed, 1, 0) != 0)
				return;

			if (!disposing)
				return;

			lock (_syncLock)
				{
				Debug.WriteLine (String.Format ("     TCPServer.State = {0}", _server.State));

				_server.Stop ();

				KeyValuePair<uint, CrestronServerSocket>[] dictServerEntries;
				lock (_dictServerSockets)
					{
					dictServerEntries = _dictServerSockets.ToArray ();
					_dictServerSockets.Clear ();
					}

				foreach (var kvp in dictServerEntries)
					{
					Debug.WriteLine (
						String.Format (
							"     Disposing client index: {0} [endpoint = {4}, waitingClose = {1}, pendingSend = {2}, pendingReceive = {3}, disposed = {5}]",
							kvp.Key, kvp.Value.WaitingClose, kvp.Value.PendingSend, kvp.Value.PendingReceive,
							kvp.Value.Disposed ? "<unknown>" : kvp.Value.InternalRemoteEndPoint.ToString (), kvp.Value.Disposed));

					((IDisposable)kvp.Value).Dispose ();
					}
				}

			try
				{
				Debug.WriteLine ("     TCPServer.DisconnectAll ()");

				_server.DisconnectAll ();
				}
// ReSharper disable EmptyGeneralCatchClause
			catch 
// ReSharper restore EmptyGeneralCatchClause
				{
				}

			_server.SocketStatusChange -= server_SocketStatusChange;

			_server = null;
			}

		public void Dispose ()
			{
			Dispose (true);

			CrestronEnvironment.GC.SuppressFinalize (this);
			}

		protected override void CheckDisposed ()
			{
			if (_disposed != 0)
				throw new ObjectDisposedException (GetType ().FullName);
			}

		#endregion
		}

	public class SocketSendAsyncResult : IAsyncResult
		{
		private readonly CEventHandle _waitHandle = new CEvent (false, false);
		internal SocketErrorCodes ErrorCode;
		internal int DataSent;
		internal bool EndsendCalled;

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
			get { return _waitHandle; }
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

	public class SocketReceiveAsyncResult : IAsyncResult
		{
		private readonly CEventHandle _waitHandle = new CEvent (false, false);
		internal SocketErrorCodes ErrorCode;
		internal int DataReceived;
		internal bool EndreceiveCalled;

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
			get { return _waitHandle; }
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

	public class SocketAcceptAsyncResult : IAsyncResult
		{
		private readonly CEventHandle _waitHandle = new CEvent (false, false);
		internal CrestronServerSocket Socket;
		internal bool EndacceptCalled;

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
			get { return _waitHandle; }
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