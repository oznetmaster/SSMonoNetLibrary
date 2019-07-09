//
// System.Net.Sockets.NetworkStream.cs
//
// Author:
//   Miguel de Icaza (miguel@ximian.com)
//   Sridhar Kulkarni <sridharkulkarni@gmail.com>
//
// (C) 2002 Ximian, Inc. http://www.ximian.com
// Copyright (C) 2002-2006 Novell, Inc.  http://www.novell.com
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
#if SSHARP
using Crestron.SimplSharp.CrestronIO;
using IAsyncResult = Crestron.SimplSharp.CrestronIO.IAsyncResult;
using AsyncCallback = Crestron.SimplSharp.CrestronIO.AsyncCallback;
using Crestron.SimplSharp.CrestronSockets;
using Timer = Crestron.SimplSharp.CTimer;
using Crestron.SimplSharp;
#else
using System.IO;
#endif
using System.Runtime.InteropServices;
using SSCore.Diagnostics;
#if (!NET_2_1 || MOBILE) && !NETCF
using System.Timers;
using System.Threading;
#endif

#if SSHARP
namespace SSMono.Net.Sockets
#else
namespace System.Net.Sockets
#endif
	{
	public class NetworkStream : Stream, IDisposable
		{
		FileAccess access;
#if SSHARP
		CrestronSocket socket;
#else
		Socket socket;
#endif
		bool owns_socket;
		bool readable, writeable;
		bool disposed = false;

#if SSHARP
		public NetworkStream (CrestronSocket socket)
			: this (socket, FileAccess.ReadWrite, false)
			{
			}

		public NetworkStream (CrestronSocket socket, bool ownsSocket)
			: this (socket, FileAccess.ReadWrite, ownsSocket)
			{
			}

		public NetworkStream (CrestronSocket socket, FileAccess access)
			: this (socket, access, false)
			{
			}

		public NetworkStream (CrestronSocket socket, FileAccess access, bool ownsSocket)
			{
			if (socket == null)
				throw new ArgumentNullException ("socket is null");
			if (!socket.Connected)
				throw new IOException ("Not connected");

			this.socket = socket;
			owns_socket = ownsSocket;
			this.access = access;

			readable = access == FileAccess.ReadWrite || access == FileAccess.Read;
			writeable = access == FileAccess.ReadWrite || access == FileAccess.Write;
			}
#else
		public NetworkStream (Socket socket)
			: this (socket, FileAccess.ReadWrite, false)
			{
			}

		public NetworkStream (Socket socket, bool ownsSocket)
			: this (socket, FileAccess.ReadWrite, ownsSocket)
			{
			}

		public NetworkStream (Socket socket, FileAccess access)
			: this (socket, access, false)
			{
			}

		public NetworkStream (Socket socket, FileAccess access, bool ownsSocket)
			{
			if (socket == null)
				throw new ArgumentNullException ("socket is null");
			if (socket.SocketType != SocketType.Stream)
				throw new ArgumentException ("Socket is not of type Stream", "socket");
			if (!socket.Connected)
				throw new IOException ("Not connected");
			if (!socket.Blocking)
				throw new IOException ("Operation not allowed on a non-blocking socket.");

			this.socket = socket;
			this.owns_socket = ownsSocket;
			this.access = access;

			readable = CanRead;
			writeable = CanWrite;
			}
#endif

		public override bool CanRead
			{
			get
				{
				return readable;
				}
			}

		public override bool CanSeek
			{
			get
				{
				// network sockets cant seek.
				return false;
				}
			}

		public override bool CanTimeout
			{
			get
				{
				return (true);
				}
			}

		public override bool CanWrite
			{
			get
				{
				return writeable;
				}
			}

		public virtual bool DataAvailable
			{
			get
				{
				CheckDisposed ();
#if SSHARP
				return socket.DataAvailable;
#else
				return socket.Available > 0;
#endif
				}
			}

		public override long Length
			{
			get
				{
				// Network sockets always throw an exception
				throw new NotSupportedException ();
				}
			}

		public override long Position
			{
			get
				{
				// Network sockets always throw an exception
				throw new NotSupportedException ();
				}

			set
				{
				// Network sockets always throw an exception
				throw new NotSupportedException ();
				}
			}

		protected bool Readable
			{
			get
				{
				return readable;
				}

			set
				{
				readable = value;
				}
			}

#if (!NET_2_1 || MOBILE)
		public override int ReadTimeout
			{
			get
				{
				int r = socket.ReceiveTimeout;
				return (r <= 0) ? Timeout.Infinite : r;
				}
			set
				{
				if (value <= 0 && value != Timeout.Infinite)
					{
					throw new ArgumentOutOfRangeException ("value", "The value specified is less than or equal to zero and is not Infinite.");
					}

				socket.ReceiveTimeout = value;
				}
			}
#endif

#if SSHARP
		 protected CrestronSocket Socket
			{
			get { return socket; }
			}

		internal CrestronSocket InternalSocket
			{
			get { return socket; }
			}
#else
		protected Socket Socket
			{
			get
				{
				return socket;
				}
			}
#endif

		protected bool Writeable
			{
			get
				{
				return writeable;
				}

			set
				{
				writeable = value;
				}
			}

#if (!NET_2_1 || MOBILE)
		public override int WriteTimeout
			{
			get
				{
				int r = socket.SendTimeout;
				return (r <= 0) ? Timeout.Infinite : r;
				}
			set
				{
				if (value <= 0 && value != Timeout.Infinite)
					{
					throw new ArgumentOutOfRangeException ("value", "The value specified is less than or equal to zero and is not Infinite");
					}

				socket.SendTimeout = value;
				}
			}
#endif

		public override IAsyncResult BeginRead (byte[] buffer, int offset, int size,
							AsyncCallback callback, object state)
			{
			var s = socket;

			Debug.WriteLine ("NS ({0}): BeginRead (buffer, {1}, {2})", s == null ? "<unknown>" : s.InternalRemoteEndPoint.ToString (), offset, size);

			CheckDisposed ();
			IAsyncResult retval;

			if (buffer == null)
				throw new ArgumentNullException ("buffer is null");
			int len = buffer.Length;
			if (offset < 0 || offset > len)
				{
				throw new ArgumentOutOfRangeException ("offset exceeds the size of buffer");
				}
			if (size < 0 || offset + size > len)
				{
				throw new ArgumentOutOfRangeException ("offset+size exceeds the size of buffer");
				}

			if (s == null
#if SSHARP
				|| !s.Connected
#endif
				)
				{
				throw new IOException ("Connection closed");
				}

			try
				{
				retval = s.BeginReceive (buffer, offset, size, 0, callback, state);
				}
			catch (Exception e)
				{
				throw new IOException ("BeginReceive failure", e);
				}

			return retval;
			}

		public override IAsyncResult BeginWrite (byte[] buffer, int offset, int size,
							AsyncCallback callback, object state)
			{
			var s = socket;

			Debug.WriteLine ("NS ({0}): BeginWrite (buffer, {1}, {2})", s == null ? "<unknown>" : s.InternalRemoteEndPoint.ToString (), offset, size);

			CheckDisposed ();
			IAsyncResult retval;

			if (buffer == null)
				throw new ArgumentNullException ("buffer is null");

			int len = buffer.Length;
			if (offset < 0 || offset > len)
				{
				throw new ArgumentOutOfRangeException ("offset exceeds the size of buffer");
				}
			if (size < 0 || offset + size > len)
				{
				throw new ArgumentOutOfRangeException ("offset+size exceeds the size of buffer");
				}

			if (s == null
#if SSHARP
				|| !s.Connected
#endif
				)
				{
				throw new IOException ("Connection closed");
				}

			try
				{
				retval = s.BeginSend (buffer, offset, size, 0, callback, state);
				}
			catch (Exception e)
				{
				throw new IOException ("BeginWrite failure", e);
				}

			return retval;
			}

		~NetworkStream ()
			{
			Dispose (false);
			}


#if (!NET_2_1 || MOBILE) && !NETCF
		public void Close (int timeout)
			{
			if (timeout < -1)
				{
				throw new ArgumentOutOfRangeException ("timeout", "timeout is less than -1");
				}

			System.Timers.Timer close_timer = new System.Timers.Timer ();
			close_timer.Elapsed += new ElapsedEventHandler (OnTimeoutClose);
			/* NB timeout is in milliseconds here, cf
			 * seconds in Socket.Close(int)
			 */
			close_timer.Interval = timeout;
			close_timer.AutoReset = false;
			close_timer.Enabled = true;
			}

		private void OnTimeoutClose (object source, ElapsedEventArgs e)
			{
			Close ();
			}
#elif NETCF
		public void Close (int timeout)
			{
			var s = socket;

			Debug.WriteLine ("NS Close ({0}): Close ({1})", s == null ? "<unknown>" : s.InternalRemoteEndPoint.ToString (), timeout);

			if (timeout < -1)
				{
				throw new ArgumentOutOfRangeException ("timeout", "timeout is less than -1");
				}

			var close_timer = new Timer (OnTimeoutClose, timeout);
			}

		private void OnTimeoutClose (object o)
			{
			Close ();
			}
#endif

		protected override void Dispose (bool disposing)
			{
			Debug.WriteLine ("NS Dispose (" + disposing + ")");

			if (disposed)
				return;
			disposed = true;

			readable = false;
			writeable = false;

			if (disposing)
				{
				if (owns_socket || socket is CrestronClientSocket)
					{
					var s = socket;
					if (s != null)
						s.Close ();
					}
				socket = null;
				access = 0;
				}

			base.Dispose(disposing);
			}

		public override int EndRead (IAsyncResult ar)
			{
			var s = socket;

			Debug.WriteLine (String.Format ("NS ({0}): EndRead ({1})", s == null ? "<unknown>" : s.InternalRemoteEndPoint.ToString (),
				ar == null ? "<unknown>" : ar.GetType ().Name));

			int res = 0;

			CheckDisposed ();

			if (ar == null)
				throw new ArgumentNullException ("async result is null");

			if (s == null)
				{
				throw new IOException ("Connection closed");
				}

			try
				{
				res = s.EndReceive (ar);
				}
			catch (Exception e)
				{
				throw new IOException ("EndRead failure", e);
				}

			return res;
			}

		public override void EndWrite (IAsyncResult ar)
			{
			var s = socket;

			Debug.WriteLine ("NS ({0}): EndWrite ({1}) ", s == null ? "<unknown>" : s.InternalRemoteEndPoint.ToString (), ar == null ? "<unknown>" : ar.GetType ().Name);

			CheckDisposed ();
			if (ar == null)
				throw new ArgumentNullException ("async result is null");

			if (s == null)
				{
				throw new IOException ("Connection closed");
				}

			try
				{
				s.EndSend (ar);
				}
			catch (Exception e)
				{
				throw new IOException ("EndWrite failure", e);
				}
			}

		public override void Flush ()
			{
			// network streams are non-buffered, this is a no-op
			}

		public override int Read (byte[] buffer, int offset, int size)
			{
			var s = socket;

			Debug.WriteLine ("NS ({0}): Read (buffer, {1}, {2}) ", s == null ? "<unknown>" : s.InternalRemoteEndPoint.ToString (),
								  offset, size);

			CheckDisposed ();
			int res;

			if (buffer == null)
				throw new ArgumentNullException ("buffer is null");
			if (offset < 0 || offset > buffer.Length)
				{
				throw new ArgumentOutOfRangeException ("offset exceeds the size of buffer");
				}
			if (size < 0 || offset + size > buffer.Length)
				{
				throw new ArgumentOutOfRangeException ("offset+size exceeds the size of buffer");
				}

			if (s == null
#if SSHARP
				|| !s.Connected
#endif
)
				{
				throw new IOException ("Connection closed");
				}

			if (size == 0)
				return 0;

			try
				{
				res = s.Receive (buffer, offset, size, 0);
				}
			catch (Exception e)
				{
				throw new IOException ("Read failure", e);
				}

			return res;
			}

		public override long Seek (long offset, SeekOrigin origin)
			{
			// NetworkStream objects do not support seeking.

			throw new NotSupportedException ();
			}

		public override void SetLength (long value)
			{
			// NetworkStream objects do not support SetLength

			throw new NotSupportedException ();
			}

		public override void Write (byte[] buffer, int offset, int size)
			{
			var s = socket;

			Debug.WriteLine ("NS ({0}): Write (buffer, {1}, {2})", s == null ? "<unknown>" : s.InternalRemoteEndPoint.ToString (), offset, size);

			CheckDisposed ();
			if (buffer == null)
				throw new ArgumentNullException ("buffer");

			if (offset < 0 || offset > buffer.Length)
				throw new ArgumentOutOfRangeException ("offset exceeds the size of buffer");

			if (size < 0 || size > buffer.Length - offset)
				throw new ArgumentOutOfRangeException ("offset+size exceeds the size of buffer");

			if (s == null
#if SSHARP
				|| !s.Connected
#endif
)
				{
				throw new IOException ("Connection closed");
				}

			try
				{
				int count = 0;
				while (size - count > 0)
					{
					count += s.Send (buffer, offset + count, size - count, 0);
					}
				}
			catch (Exception e)
				{
				throw new IOException ("Write failure", e);
				}
			}

		private void CheckDisposed ()
			{
			if (disposed)
				throw new ObjectDisposedException (GetType ().FullName);
			}


		}
	}
