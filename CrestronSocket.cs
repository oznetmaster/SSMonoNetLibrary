//
// CrestronSocket.cs
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
using System.Linq;
using System.Text;
using Crestron.SimplSharp;
using Crestron.SimplSharp.CrestronIO;
using Crestron.SimplSharp.Cryptography;
using Crestron.SimplSharp.Net;
using SSMono.Net;
using SSMono.Net.Sockets;
using SSMono.Threading;
using Socket = Crestron.SimplSharp.CrestronSockets.CrestronClientSocket;
using IAsyncResult = Crestron.SimplSharp.CrestronIO.IAsyncResult;
using AsyncCallback = Crestron.SimplSharp.CrestronIO.AsyncCallback;

namespace Crestron.SimplSharp.CrestronSockets
	{
	using SocketException = SSMono.Net.Sockets.SocketException;

	public class CrestronSocket : IDisposable
		{
		protected SocketShutdown? _shutdown;
		private NetworkStream _networkStream;
		protected internal bool _active;
		protected int _bytesSent;
		private bool? _isLocal;

		public virtual bool Connected
			{
			get { return false; }
			}

		public virtual bool DataAvailable
			{
			get
				{
				CheckDisposed ();

				return false;
				}
			}

		public virtual IPEndPoint RemoteEndPoint
			{
			get
				{
				CheckDisposed ();

				return new IPEndPoint (IPAddress.None, 0);
				}
			}

		internal virtual IPEndPoint InternalRemoteEndPoint
			{
			get { return new IPEndPoint (IPAddress.None, 0); }
			}

		public virtual IPEndPoint LocalEndPoint
			{
			get
				{
				CheckDisposed ();
				return new IPEndPoint (IPAddress.Loopback, 0);
				}
			}

		public bool IsLocal
			{
			get
				{
				if (_isLocal.HasValue)
					return _isLocal.Value;

				return (_isLocal = InternalRemoteEndPoint.Address.IsLocal ()).Value;
				}
			}

		public bool IsBound
			{
			get { return LocalEndPoint.Port != 0; }
			}

		public virtual int Available
			{
			get { return 0; }
			}

		public virtual bool Nagle { get; set; }

		public bool NoDelay
			{
			get { return !Nagle; }
			set { Nagle = !value; }
			}

		private int _sendTimeout;
		private int _receiveTimeout;

		public virtual int SendTimeout
			{
			get
				{
				CheckDisposed ();
				return _sendTimeout;
				}
			set
				{
				CheckDisposed ();

				if (value < -1)
					throw new ArgumentOutOfRangeException ("value");

				_sendTimeout = value == -1 ? 0 : value;
				}
			}

		public virtual int ReceiveTimeout
			{
			get
				{
				CheckDisposed ();
				return _receiveTimeout;
				}
			set
				{
				CheckDisposed ();

				if (value < -1)
					throw new ArgumentOutOfRangeException ("value");

				_receiveTimeout = value == -1 ? 0 : value;
				}
			}

		public virtual SocketType SocketType
			{
			get { return SocketType.Unknown; }
			}

		public virtual ProtocolType ProtocolType
			{
			get { return ProtocolType.Unknown; }
			}

		public virtual AddressFamily AddressFamily
			{
			get { return AddressFamily.Unknown; }
			}

		public virtual void Connect (IPEndPoint endPoint)
			{
			throw new NotSupportedException ();
			}

		public virtual void Bind (IPEndPoint localEp)
			{
			throw new NotSupportedException ();
			}


		public NetworkStream GetStream ()
			{
			CheckDisposed ();

			if (!Connected)
				throw new InvalidOperationException ("Socket not connected");

			return _networkStream ?? (_networkStream = new NetworkStream (this, false));
			}

		public virtual IAsyncResult BeginReceive (byte[] buffer, int offset, int size, SocketFlags socketFlags, AsyncCallback callback, Object state)
			{
			throw new NotSupportedException ();
			}

		public virtual int EndReceive (IAsyncResult asyncResult)
			{
			throw new NotSupportedException ();
			}

		public virtual IAsyncResult BeginSend (byte[] buffer, int offset, int size, SocketFlags socketFlags, AsyncCallback callback, Object state)
			{
			throw new NotSupportedException ();
			}

		public virtual int EndSend (IAsyncResult asyncResult)
			{
			throw new NotSupportedException ();
			}

		public virtual void Close ()
			{
			((IDisposable)this).Dispose ();
			}

		public void Disconnect (bool reuseSocket)
			{
			Close ();
			}

		public virtual void Shutdown (SocketShutdown how)
			{
			CheckDisposed ();

			_shutdown = how;
			}

		public int Send (byte[] buffer)
			{
			if (buffer == null)
				throw new ArgumentNullException ("buffer");

			return Send (buffer, 0, buffer.Length, SocketFlags.None);
			}

		public int Send (byte[] buffer, int size, SocketFlags socketFlags)
			{
			return Send (buffer, 0, size, socketFlags);
			}

		public int Send (IList<ArraySegment<byte>> buffers)
			{
			return Send (buffers, SocketFlags.None);
			}

		public int Send (IList<ArraySegment<byte>> buffers, SocketFlags flags)
			{
			CheckDisposed ();

			if (buffers == null)
				throw new ArgumentNullException ("buffers");

			var buff = buffers.SelectMany (seg => seg.Array.Skip (seg.Offset).Take (seg.Count)).ToArray ();

			return Send (buff, 0, buff.Length, flags);
			}

		public virtual int Send (byte[] buffer, int offset, int size, SocketFlags socketFlags)
			{
			CheckDisposed ();

			if (_shutdown.HasValue && (_shutdown == SocketShutdown.Send || _shutdown == SocketShutdown.Both))
				throw new SocketException (SocketError.Shutdown);

			return 0;
			}

		public int SendTo (byte[] buffer, IPEndPoint remoteEP)
			{
			return SendTo (buffer, 0, buffer.Length, 0, remoteEP);
			}

		public int SendTo (byte[] buffer, SocketFlags socketFlags, IPEndPoint remoteEP)
			{
			return SendTo (buffer, 0, buffer.Length, socketFlags, remoteEP);
			}

		public int SendTo (byte[] buffer, int size, SocketFlags socketFlags, IPEndPoint remoteEP)
			{
			return SendTo (buffer, 0, size, socketFlags, remoteEP);
			}

		public virtual int SendTo (byte[] buffer, int offset, int size, SocketFlags socketFlags, IPEndPoint remoteEp)
			{
			CheckDisposed ();

			if (_shutdown.HasValue && (_shutdown == SocketShutdown.Send || _shutdown == SocketShutdown.Both))
				throw new SocketException (SocketError.Shutdown);

			return 0;
			}

		public int Receive (byte[] buffer)
			{
			if (buffer == null)
				throw new ArgumentNullException ("buffer");

			return Receive (buffer, 0, buffer.Length, SocketFlags.None);
			}

		public int Receive (byte[] buffer, SocketFlags socketFlags)
			{
			if (buffer == null)
				throw new ArgumentNullException ("buffer");

			return Receive (buffer, 0, buffer.Length, socketFlags);
			}

		public int Receive (byte[] buffer, int size, SocketFlags socketFlags)
			{
			return Receive (buffer, 0, size, socketFlags);
			}

		public int Receive (IList<ArraySegment<byte>> buffers)
			{
			return Receive (buffers, SocketFlags.None);
			}

		public int Receive (IList<ArraySegment<byte>> buffers, SocketFlags flags)
			{
			CheckDisposed ();

			if (buffers == null)
				throw new ArgumentNullException ("buffers");

			int size = buffers.Aggregate (0, (sz, seg) => sz + seg.Count);
			var buf = new byte[size];

			var cnt = Receive (buf, 0, size, flags);
			var tcnt = cnt;
			var ix = 0;

			foreach (var seg in buffers)
				{
				Buffer.BlockCopy (buf, ix, seg.Array, seg.Offset, tcnt >= seg.Count ? seg.Count : tcnt);
				ix += seg.Count;
				tcnt -= seg.Count;
				if (tcnt <= 0)
					break;
				}

			return cnt;
			}

		public virtual int Receive (byte[] buffer, int offset, int size, SocketFlags socketFlags)
			{
			CheckDisposed ();

			if (_shutdown.HasValue && (_shutdown == SocketShutdown.Receive || _shutdown == SocketShutdown.Both))
				throw new SocketException (SocketError.Shutdown);

			return 0;
			}

		public void SendFile (string fileName)
			{
			SendFile (fileName, null, null, TransmitFileOptions.UseDefaultWorkerThread);
			}

		public void SendFile (string fileName, byte[] preBuffer, byte[] postBuffer, TransmitFileOptions flags)
			{
			if (fileName == null)
				throw new ArgumentNullException ("fileName");

			if (!File.Exists (fileName))
				throw new FileNotFoundException ("file not found");

			CheckDisposed ();

			if (!Connected)
				throw new NotSupportedException ("not connected to remote host");

			using (var fs = new FileStream (fileName, FileMode.Open, FileAccess.Read, FileShare.Read))
			using (var ns = GetStream ())
				{
				if (preBuffer != null)
					ns.Write (preBuffer, 0, preBuffer.Length);
				fs.CopyTo (ns);
				if (postBuffer != null)
					ns.Write (postBuffer, 0, postBuffer.Length);
				}

			if (flags.HasFlag (TransmitFileOptions.Disconnect))
				Close ();
			}

		public IAsyncResult BeginSendFile (string fileName, AsyncCallback callback, object state)
			{
			return BeginSendFile (fileName, null, null, TransmitFileOptions.UseDefaultWorkerThread, callback, state);
			}

		private class SendFileInfo
			{
			public readonly string FileName;
			public readonly CrestronSocket Socket;
			public readonly TransmitFileOptions Flags;
			public readonly SendFileAsyncResult Iar;
			public readonly AsyncCallback Callback;

			public SendFileInfo (string fileName, CrestronSocket socket, TransmitFileOptions flags, SendFileAsyncResult iar, AsyncCallback callback)
				{
				FileName = fileName;
				Socket = socket;
				Flags = flags;
				Iar = iar;
				Callback = callback;
				}
			}

		private class SendFileAsyncResult : IAsyncResult
			{
			#region IAsyncResult Members

			private readonly ManualResetEvent _completed = new ManualResetEvent (false);

			public object AsyncState { get; internal set; }

			public CEventHandle AsyncWaitHandle
				{
				get { return _completed; }
				}

			public bool CompletedSynchronously { get; internal set; }

			public object InnerObject
				{
				get { throw new NotImplementedException (); }
				}

			public bool IsCompleted { get; internal set; }

			internal bool EndCalled { get; set; }

			#endregion
			}

		public IAsyncResult BeginSendFile (string fileName, byte[] preBuffer, byte[] postBuffer, TransmitFileOptions flags, AsyncCallback callback, object state)
			{
			if (fileName == null)
				throw new ArgumentNullException ("fileName");

			if (!File.Exists (fileName))
				throw new FileNotFoundException ("file not found");

			CheckDisposed ();

			if (!Connected)
				throw new NotSupportedException ("not connected to remote host");

			var iar = new SendFileAsyncResult {AsyncState = state};

			ThreadPool.QueueUserWorkItem (o =>
				{
				var sfi = (SendFileInfo)o;
				using (var fs = new FileStream (sfi.FileName, FileMode.Open, FileAccess.Read, FileShare.Read))
				using (var ns = sfi.Socket.GetStream ())
					{
					if (preBuffer != null)
						ns.Write (preBuffer, 0, preBuffer.Length);
					fs.CopyTo (ns);
					if (postBuffer != null)
						ns.Write (postBuffer, 0, postBuffer.Length);
					}

				if (sfi.Flags.HasFlag (TransmitFileOptions.Disconnect))
					sfi.Socket.Close ();

				sfi.Iar.IsCompleted = true;
				((CEvent)sfi.Iar.AsyncWaitHandle).Set ();
				if (sfi.Callback != null)
					DoAsyncCallback (sfi.Callback, sfi.Iar);
				}, new SendFileInfo (fileName, this, flags, iar, callback));

			return iar;
			}

		public void EndSendFile (IAsyncResult asyncResult)
			{
			if (asyncResult == null)
				throw new ArgumentNullException ("asyncResult");

			var sfi = asyncResult as SendFileAsyncResult;

			if (sfi == null)
				throw new ArgumentException ("invalid asyncResult");

			if (sfi.EndCalled)
				throw new InvalidOperationException ("EndSendFile already called");

			sfi.EndCalled = true;
			}

		protected static void DoAsyncCallback (AsyncCallback callback, IAsyncResult result)
			{
			try
				{
				callback (result);
				}
			catch (Exception)
				{
				}
			}

		protected virtual void CheckDisposed ()
			{
			if (_disposed != 0)
				throw new ObjectDisposedException (GetType ().FullName);
			}

		#region IDisposable Members

		void IDisposable.Dispose ()
			{
			Dispose (true);

			CrestronEnvironment.GC.SuppressFinalize (this);
			}

		#endregion

		protected internal int _disposed;

		~CrestronSocket ()
			{
			Dispose (false);
			}

		protected virtual void Dispose (bool disposing)
			{
			if (Interlocked.CompareExchange (ref _disposed, 1, 0) != 0)
				return;

			_active = false;
			}

		private static int InterlockedAdd (ref Int32 target, Int32 value)
			{
			Int32 i, j = target, n;
			do
				{
				i = j;
				n = unchecked (i + value);
				j = Interlocked.CompareExchange (ref target, n, i);
				} while (i != j);
			return n;
			}

		protected void UpdateSentData (int size)
			{
			if (!IsLocal)
				return;

			if (InterlockedAdd (ref _bytesSent, size) > 32767)
				{
				_bytesSent = 0;

				CrestronEnvironment.Sleep (100);
				}
			}
		}

	public class CrestronConnectableSocket : CrestronSocket
		{
		public void Connect (IPAddress addr, int port)
			{
			CheckDisposed ();

			if (addr == null)
				throw new ArgumentNullException ("addr");

			if (port < 0 || port > 65535)
				throw new ArgumentOutOfRangeException ("port");

			Connect (new IPEndPoint (addr, port));
			}

		public void Connect (string hostname, int port)
			{
			CheckDisposed ();

			if (hostname == null)
				throw new ArgumentNullException ("hostname");

			if (port < 0 || port > 65535)
				throw new ArgumentOutOfRangeException ("port");

			var addresses = DnsEx.GetHostAddresses (hostname);

			if (addresses.Length == 0)
				throw new SocketException (SocketError.HostNotFound);

			Connect (addresses, port);
			}

		public virtual void Connect (IPAddress[] addresses, int port)
			{
			CheckDisposed ();

			if (addresses == null)
				throw new ArgumentNullException ("addresses");

			if (port < 0 || port > 65535)
				throw new ArgumentOutOfRangeException ("port");

			foreach (var address in addresses)
				{
				try
					{
					Connect (address, port);

					if (!_active)
						throw new SocketException (SocketError.NotConnected);

					return;
					}
				catch (SocketException)
					{
					}
				}

			throw new SocketException (SocketError.HostUnreachable);
			}
		}
	}