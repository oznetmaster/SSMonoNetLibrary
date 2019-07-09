//
// System.IO.BufferedStream
//
// Author:
//   Matt Kimball (matt@kimball.net)
//   Ville Palo <vi64pa@kolumbus.fi>
//
// Copyright (C) 2004 Novell (http://www.novell.com)
//

//
// Copyright (C) 2004 Novell, Inc (http://www.novell.com)
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
using Crestron.SimplSharp;
using Crestron.SimplSharp.CrestronIO;
using CIO = Crestron.SimplSharp.CrestronIO;
using IAsyncResult = Crestron.SimplSharp.CrestronIO.IAsyncResult;
using AsyncCallback = Crestron.SimplSharp.CrestronIO.AsyncCallback;
using GC = Crestron.SimplSharp.CrestronEnvironment.GC;
using SSCore.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;

namespace SSMono.Net.Sockets
	{
	[ComVisible (true)]
	public sealed class BufferedNetworkStream : Stream
		{
		private NetworkStream m_stream;
		private byte[] m_inputBuffer;
		private byte[] m_outputBuffer;
		private int m_inputBuffer_pos;
		private int m_outputBuffer_pos;
		private int m_inputBuffer_read_ahead;
		private bool disposed = false;
		private readonly int m_bufferSize;
		private CTimer m_nagleTimer;
		private FastLock m_lockFlush = new FastLock ();
		private bool m_isLocal;

		public BufferedNetworkStream (NetworkStream stream)
			: this (stream, 4096)
			{
			}

		public BufferedNetworkStream (NetworkStream stream, int bufferSize)
			{
			if (stream == null)
				throw new ArgumentNullException ("stream");
			// LAMESPEC: documented as < 0
			if (bufferSize <= 0)
				throw new ArgumentOutOfRangeException ("bufferSize", "<= 0");
			if (!stream.CanRead && !stream.CanWrite)
				throw new ObjectDisposedException (Locale.GetText ("Cannot access a closed Stream."));

			m_stream = stream;
			m_bufferSize = bufferSize;
			m_isLocal = m_stream.InternalSocket.IsLocal;

			if (!m_stream.InternalSocket.Nagle)
				{
				m_nagleTimer = new CTimer ((o) =>
					{
						Debug.WriteLine ("BNS ({0}): Nagle", NetworkStream.InternalSocket.RemoteEndPoint);
						if (!disposed && m_stream.CanWrite)
							Flush ();
					}, Timeout.Infinite);
				}
			}

		public override bool CanRead
			{
			get { return m_stream.CanRead; }
			}

		public override bool CanWrite
			{
			get { return m_stream.CanWrite; }
			}

		public override bool CanSeek
			{
			get { return m_stream.CanSeek; }
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

		protected override void Dispose (bool disposing)
			{
			Debug.WriteLine ("BNS ({0}): Dispose ({1}) [disposed = {2}]", disposed ? "<unknown>" : NetworkStream.InternalSocket.InternalRemoteEndPoint.ToString (), disposing, disposed);

			if (disposed)
				return;
			disposed = true;

			if (disposing)
				{
				if (m_nagleTimer != null)
					{
					if (m_stream.CanWrite)
						InternalFlush ();

					m_nagleTimer.Dispose ();
					m_nagleTimer = null;
					}

				m_stream.Close ();
				m_inputBuffer = null;
				m_outputBuffer = null;
				}
			}

		public override void Flush ()
			{
			Debug.WriteLine ("BNS ({0}): Flush () [m_outputBuffer_pos = {1}]", NetworkStream.InternalSocket.InternalRemoteEndPoint, m_outputBuffer_pos);

			CheckObjectDisposedException ();

			if (m_stream.CanWrite && m_nagleTimer != null)
				InternalFlush ();
			}

		private void InternalFlush ()
			{
			Debug.WriteLine ("BNS ({0}): InternalFlush () [m_outputBuffer_pos = {1}]", NetworkStream.InternalSocket.InternalRemoteEndPoint, m_outputBuffer_pos);

			if (m_outputBuffer_pos == 0)
				return;

			if (!m_lockFlush.TryAcquire ())
				{
				Debug.WriteLine ("     Cannot acquire lock");
				return;
				}

			try
				{
				if (m_outputBuffer_pos != 0)
					{
					m_nagleTimer.Stop ();

					m_stream.Write (m_outputBuffer, 0, m_outputBuffer_pos);

					m_outputBuffer_pos = 0;
					}
				}
			finally
				{
				m_lockFlush.Release ();
				}
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

		private void EnsureInputBuffer ()
			{
			if (m_inputBuffer == null)
				m_inputBuffer = new byte[m_bufferSize];
			}

		private void EnsureOutputBuffer ()
			{
			if (m_outputBuffer == null)
				m_outputBuffer = new byte[m_bufferSize];
			}

		public override int ReadByte ()
			{
			Debug.WriteLine ("BNS ({0}): ReadByte () [m_inputBuffer_pos = {1}, m_inputBuffer_read_ahead = {2}]", NetworkStream.InternalSocket.InternalRemoteEndPoint,
								  m_inputBuffer_pos, m_inputBuffer_read_ahead);

			CheckObjectDisposedException ();

			if (!m_stream.CanRead)
				throw new NotSupportedException (Locale.GetText ("Cannot read from stream"));

			EnsureInputBuffer ();

			if (1 <= m_inputBuffer_read_ahead - m_inputBuffer_pos)
				return m_inputBuffer[m_inputBuffer_pos++];

			m_inputBuffer_pos = 0;

			m_inputBuffer_read_ahead = m_stream.Read (m_inputBuffer, 0, m_bufferSize);

			if (1 <= m_inputBuffer_read_ahead)
				return m_inputBuffer[m_inputBuffer_pos++];

			return -1;
			}

		public override void WriteByte (byte value)
			{
			Debug.WriteLine ("BNS ({0}): WriteByte () [m_outputBuffer_pos = {1}]", NetworkStream.InternalSocket.InternalRemoteEndPoint, m_outputBuffer_pos);

			CheckObjectDisposedException ();

			if (!m_stream.CanWrite)
				throw new NotSupportedException (Locale.GetText ("Cannot write to stream"));

			if (m_nagleTimer == null)
				{
				m_stream.Write (new byte[] { value }, 0, 1);
				return;
				}

			EnsureOutputBuffer ();

			m_lockFlush.Acquire ();
			try
				{
				m_outputBuffer[m_outputBuffer_pos++] = value;

				if (m_outputBuffer_pos == m_bufferSize)
					{
					m_nagleTimer.Stop ();
					m_stream.Write (m_outputBuffer, 0, m_bufferSize);
					m_outputBuffer_pos = 0;

					if (m_isLocal)
						CrestronEnvironment.Sleep (200);
					}
				else
					m_nagleTimer.Reset (200);
				}
			finally
				{
				m_lockFlush.Release ();
				}
			}

		public override int Read ([In, Out] byte[] array, int offset, int count)
			{
			Debug.WriteLine ("BNS ({0}): Read (array, {1}, {2}) [m_inputBuffer_pos = {3}, m_inputBuffer_read_ahead = {4}]", NetworkStream.InternalSocket.InternalRemoteEndPoint,
								  offset, count, m_inputBuffer_pos, m_inputBuffer_read_ahead);

			if (array == null)
				throw new ArgumentNullException ("array");
			if (offset < 0)
				throw new ArgumentOutOfRangeException ("offset", "< 0");
			if (count < 0)
				throw new ArgumentOutOfRangeException ("count", "< 0");
			// re-ordered to avoid possible integer overflow
			if (array.Length - offset < count)
				throw new ArgumentException ("array.Length - offset < count");

			CheckObjectDisposedException ();

			if (!m_stream.CanRead)
				throw new NotSupportedException (Locale.GetText ("Cannot read from stream"));

			if (count <= m_inputBuffer_read_ahead - m_inputBuffer_pos)
				{
				Buffer.BlockCopy (m_inputBuffer, m_inputBuffer_pos, array, offset, count);

				m_inputBuffer_pos += count;
				if (m_inputBuffer_pos == m_inputBuffer_read_ahead)
					{
					m_inputBuffer_pos = 0;
					m_inputBuffer_read_ahead = 0;
					}

				return count;
				}

			int ret = m_inputBuffer_read_ahead - m_inputBuffer_pos;
			if (ret != 0)
				{
				Buffer.BlockCopy (m_inputBuffer, m_inputBuffer_pos, array, offset, ret);
				offset += ret;
				count -= ret;
				}

			m_inputBuffer_pos = 0;
			m_inputBuffer_read_ahead = 0;

			if (ret == 0 || m_stream.InternalSocket.DataAvailable)
				{
				if (count > m_bufferSize / 2)
					ret += m_stream.Read (array, offset, count);
				else
					{
					EnsureInputBuffer ();

					m_inputBuffer_read_ahead = m_stream.Read (m_inputBuffer, 0, m_bufferSize);

					if (count < m_inputBuffer_read_ahead)
						{
						Buffer.BlockCopy (m_inputBuffer, 0, array, offset, count);
						m_inputBuffer_pos = count;
						ret += count;
						}
					else
						{
						Buffer.BlockCopy (m_inputBuffer, 0, array, offset, m_inputBuffer_read_ahead);
						ret += m_inputBuffer_read_ahead;
						m_inputBuffer_read_ahead = 0;
						}
					}
				}

			return ret;
			}

		public override void Write (byte[] array, int offset, int count)
			{
			Debug.WriteLine ("BNS ({0}): Write (array, {1}, {2}) [m_outputBuffer_pos = {3}]", NetworkStream.InternalSocket.InternalRemoteEndPoint, offset, count,
								  m_outputBuffer_pos);

			if (array == null)
				throw new ArgumentNullException ("array");
			if (offset < 0)
				throw new ArgumentOutOfRangeException ("offset", "< 0");
			if (count < 0)
				throw new ArgumentOutOfRangeException ("count", "< 0");
			// avoid possible integer overflow
			if (array.Length - offset < count)
				throw new ArgumentException ("array.Length - offset < count");

			CheckObjectDisposedException ();

			if (!m_stream.CanWrite)
				throw new NotSupportedException (Locale.GetText ("Cannot write to stream"));

			if (count == 0)
				return;

			if (m_nagleTimer == null)
				{
				m_stream.Write (array, offset, count);
				return;
				}

			int avail = m_bufferSize - m_outputBuffer_pos;
			if (count <= avail)
				{
				EnsureOutputBuffer ();

				m_lockFlush.Acquire ();
				try
					{
					Buffer.BlockCopy (array, offset, m_outputBuffer, m_outputBuffer_pos, count);
					m_outputBuffer_pos += count;

					if (m_outputBuffer_pos == m_bufferSize)
						{
						m_nagleTimer.Stop ();
						m_stream.Write (m_outputBuffer, 0, m_bufferSize);
						m_outputBuffer_pos = 0;

						if (m_isLocal)
							CrestronEnvironment.Sleep (200);
						}
					else
						m_nagleTimer.Reset (200);
					}
				finally
					{
					m_lockFlush.Release ();
					}

				return;
				}

			if (m_outputBuffer_pos != 0 && count <= m_bufferSize)
				{
				m_nagleTimer.Stop ();

				m_lockFlush.Acquire ();
				try
					{
					if (m_outputBuffer_pos != 0)
						{
						var tbuffer = new byte[m_outputBuffer_pos + count];
						Buffer.BlockCopy (m_outputBuffer, 0, tbuffer, 0, m_outputBuffer_pos);
						Buffer.BlockCopy (array, offset, tbuffer, m_outputBuffer_pos, count);

						m_outputBuffer_pos = 0;

						m_stream.Write (tbuffer, 0, tbuffer.Length);

						if (m_isLocal)
							CrestronEnvironment.Sleep (200);

						return;
						}
					}
				finally
					{
					m_lockFlush.Release ();
					}
				}

			InternalFlush ();
			m_stream.Write (array, offset, count);
			}

		private void CheckObjectDisposedException ()
			{
			if (disposed)
				throw new ObjectDisposedException ("BufferedNetworkStream", Locale.GetText ("Stream is closed"));
			}

		public override int ReadTimeout
			{
			get { return m_stream.ReadTimeout; }
			set { m_stream.ReadTimeout = value; }
			}

		public override int WriteTimeout
			{
			get { return m_stream.WriteTimeout; }
			set { m_stream.WriteTimeout = value; }
			}

		public NetworkStream NetworkStream
			{
			get { return m_stream; }
			}

		public override bool CanTimeout
			{
			get { return m_stream.CanTimeout; }
			}

		public override void Close ()
			{
			Dispose (true);

			GC.SuppressFinalize (this);
			}

		private class ReadState
			{
			public AsyncCallback callback;
			public ReadAsyncResult readAsyncResult;
			public byte[] buffer;
			public int offset;
			public int count;
			}

		public override IAsyncResult BeginRead (byte[] buffer, int offset, int count, AsyncCallback callback, object state)
			{
			Debug.WriteLine ("BNS ({0}): BeginRead (buffer, {1}, {2}) [m_inputBuffer_pos = {3}, m_inputBuffer_read_ahead = {4}]",
								  NetworkStream.InternalSocket.InternalRemoteEndPoint, offset, count, m_inputBuffer_pos, m_inputBuffer_read_ahead);

			ReadAsyncResult rar;

			if (buffer == null)
				throw new ArgumentNullException ("buffer is null");
			int len = buffer.Length;
			if (offset < 0 || offset > len)
				throw new ArgumentOutOfRangeException ("offset exceeds the size of buffer");
			if (count < 0 || offset + count > len)
				throw new ArgumentOutOfRangeException ("offset+size exceeds the size of buffer");

			CheckObjectDisposedException ();

			if (!m_stream.CanRead)
				throw new NotSupportedException (Locale.GetText ("Cannot read from stream"));

			if (m_inputBuffer_pos < m_inputBuffer_read_ahead)
				{
				int curLength = m_inputBuffer_read_ahead - m_inputBuffer_pos;
				if (count <= curLength)
					{
					Buffer.BlockCopy (m_inputBuffer, m_inputBuffer_pos, buffer, offset, count);
					m_inputBuffer_pos += count;

					var rsr = new ReadSyncResult
						{
							AsyncState = state,
							DataReceived = count
						};

					if (callback != null)
						callback.BeginInvokeEx (rsr, null, null);

					return rsr;
					}

				if (curLength != 0)
					{
					Buffer.BlockCopy (m_inputBuffer, m_inputBuffer_pos, buffer, offset, curLength);
					count -= curLength;
					offset += curLength;

					if (!m_stream.InternalSocket.DataAvailable)
						{
						m_inputBuffer_pos = 0;
						m_inputBuffer_read_ahead = 0;

						var rsr = new ReadSyncResult
							{
								AsyncState = state,
								DataReceived = curLength
							};

						if (callback != null)
							callback.BeginInvokeEx (rsr, null, null);

						return rsr;
						}
					}

				rar = new ReadAsyncResult
					{
						DataReceived = curLength,
						AsyncState = state
					};
				}
			else
				{
				rar = new ReadAsyncResult
					{
						DataReceived = 0,
						AsyncState = state
					};
				}

			m_inputBuffer_pos = 0;
			m_inputBuffer_read_ahead = 0;

			if (count > m_bufferSize / 2)
				{
				if (rar.DataReceived == 0)
					return m_stream.BeginRead (buffer, offset, count, callback, state);

				m_stream.BeginRead (buffer, offset, count, iar =>
					{
						var rs = (ReadState)iar.AsyncState;

						rs.readAsyncResult.DataReceived += m_stream.EndRead (iar);

						rs.readAsyncResult.IsCompleted = true;
						((CEvent)rs.readAsyncResult.AsyncWaitHandle).Set ();

						if (rs.callback != null)
							{
							try
								{
								rs.callback (rs.readAsyncResult);
								}
							catch
								{
								}
							}
					}, new ReadState
						{
							callback = callback,
							readAsyncResult = rar
						});

				return rar;
				}

			EnsureInputBuffer ();

			m_stream.BeginRead (m_inputBuffer, 0, m_bufferSize, iar =>
				{
					var rs = (ReadState)iar.AsyncState;

					m_inputBuffer_read_ahead = m_stream.EndRead (iar);

					if (rs.count > m_inputBuffer_read_ahead)
						rs.count = m_inputBuffer_read_ahead;

					rs.readAsyncResult.DataReceived += rs.count;

					if (rs.count != 0)
						{
						Buffer.BlockCopy (m_inputBuffer, 0, rs.buffer, rs.offset, rs.count);
						m_inputBuffer_pos = rs.count;
						}

					rs.readAsyncResult.IsCompleted = true;
					((CEvent)rs.readAsyncResult.AsyncWaitHandle).Set ();

					if (rs.callback != null)
						{
						try
							{
							rs.callback (rs.readAsyncResult);
							}
						catch
							{
							}
						}
				}, new ReadState
					{
						callback = callback,
						readAsyncResult = rar,
						buffer = buffer,
						offset = offset,
						count = count
					});

			return rar;
			}

		private class WriteState
			{
			public AsyncCallback callback;
			public WriteAsyncResult writeAsyncResult;
			public byte[] buffer;
			public int offset;
			public int count;
			}

		public override IAsyncResult BeginWrite (byte[] buffer, int offset, int count, AsyncCallback callback, object state)
			{
			Debug.WriteLine ("BNS ({0}): BeginWrite (buffer, {1}, {2}) [m_outputBuffer_pos = {3}]", NetworkStream.InternalSocket.InternalRemoteEndPoint, offset, count,
								  m_outputBuffer_pos);

			if (buffer == null)
				throw new ArgumentNullException ("buffer is null");

			int len = buffer.Length;
			if (offset < 0 || offset > len)
				throw new ArgumentOutOfRangeException ("offset exceeds the size of buffer");
			if (count < 0 || offset + count > len)
				throw new ArgumentOutOfRangeException ("offset+size exceeds the size of buffer");

			CheckObjectDisposedException ();

			if (!m_stream.CanWrite)
				throw new NotSupportedException (Locale.GetText ("Cannot write to stream"));

			if (m_nagleTimer == null)
				return m_stream.BeginWrite (buffer, offset, count, callback, state);

			var avail = m_bufferSize - m_outputBuffer_pos;
			if (count <= avail)
				{
				EnsureOutputBuffer ();

				m_lockFlush.Acquire ();
				try
					{
					Buffer.BlockCopy (buffer, offset, m_outputBuffer, m_outputBuffer_pos, count);
					m_outputBuffer_pos += count;

					if (m_outputBuffer_pos == m_bufferSize)
						{
						m_nagleTimer.Stop ();

						m_outputBuffer_pos = 0;

						return m_stream.BeginWrite (m_outputBuffer, 0, m_bufferSize, callback, state);
						}

					m_nagleTimer.Reset (200);

					var wsr = new WriteSyncResult
						{
							AsyncState = state
						};

					if (callback != null)
						callback.BeginInvokeEx (wsr, null, null);

					return wsr;
					}
				finally
					{
					m_lockFlush.Release ();
					}
				}

			if (m_outputBuffer_pos == 0 && count > m_bufferSize)
				return m_stream.BeginWrite (buffer, offset, count, callback, state);

			if (m_inputBuffer_pos != 0)
				{
				m_lockFlush.Acquire ();
				try
					{
					if (m_outputBuffer_pos != 0)
						{
						m_nagleTimer.Stop ();

						if (count <= m_bufferSize)
							{
							var tbuffer = new byte[m_outputBuffer_pos + count];
							Buffer.BlockCopy (m_outputBuffer, 0, tbuffer, 0, m_outputBuffer_pos);
							Buffer.BlockCopy (buffer, offset, tbuffer, m_outputBuffer_pos, count);

							m_outputBuffer_pos = 0;

							return m_stream.BeginWrite (tbuffer, 0, tbuffer.Length, callback, state);
							}

						var war = new WriteAsyncResult
							{
								AsyncState = state
							};

						m_stream.BeginWrite (m_outputBuffer, 0, m_outputBuffer_pos, iar =>
							{
								var ws = (WriteState)iar.AsyncState;

								m_stream.EndWrite (iar);

								m_stream.BeginWrite (ws.buffer, ws.offset, ws.count, iar2 =>
									{
										var ws2 = (WriteState)iar.AsyncState;

										m_stream.EndWrite (iar2);

										ws2.writeAsyncResult.IsCompleted = true;
										((CEvent)ws2.writeAsyncResult.AsyncWaitHandle).Set ();

										if (ws2.callback != null)
											{
											try
												{
												ws2.callback (ws2.writeAsyncResult);
												}
											catch
												{
												}
											}
									}, ws);
							}, new WriteState
								{
									writeAsyncResult = war,
									callback = callback,
									buffer = buffer,
									offset = offset,
									count = count,
								});

						m_outputBuffer_pos = 0;

						return war;
						}
					}
				finally
					{
					m_lockFlush.Release ();
					}
				}

			return m_stream.BeginWrite (buffer, offset, count, callback, state);
			}

		public override int EndRead (IAsyncResult asyncResult)
			{
			Debug.Write (String.Format ("BNS ({0}): EndRead ({1}) [m_inputBuffer_pos = {2}, m_inputBuffer_read_ahead = {3}]", NetworkStream.InternalSocket.InternalRemoteEndPoint,
				asyncResult == null ? "<unknown>" : asyncResult.GetType ().Name, m_inputBuffer_pos, m_inputBuffer_read_ahead));

			try
				{
				if (asyncResult == null)
					throw new ArgumentNullException ("asyncResult");

				var rar = asyncResult as ReadAsyncResult;
				if (rar != null)
					{
					if (rar.EndReadCalled)
						throw new InvalidOperationException ("EndRead was already called");

					rar.EndReadCalled = true;

					if (!rar.IsCompleted)
						rar.AsyncWaitHandle.Wait ();

					Debug.Write (String.Format (" returns {0}", rar.DataReceived));

					return rar.DataReceived;
					}

				var rsr = asyncResult as ReadSyncResult;
				if (rsr != null)
					{
					if (rsr.EndReadCalled)
						throw new InvalidOperationException ("EndRead was already called");
					rsr.EndReadCalled = true;

					Debug.Write (String.Format (" returns {0}", rsr.DataReceived));

					return rsr.DataReceived;
					}

				}
			finally
				{
				Debug.WriteLine (String.Empty);
				}

			return m_stream.EndRead (asyncResult);
			}

		public override void EndWrite (IAsyncResult asyncResult)
			{
			Debug.WriteLine ("BNS ({0}): EndWrite ({1}) [m_outputBuffer_pos = {2}]", NetworkStream.InternalSocket.InternalRemoteEndPoint, asyncResult == null ? "<unknown>" : asyncResult.GetType ().Name,
								  m_outputBuffer_pos);

			if (asyncResult == null)
				throw new ArgumentNullException ("asyncResult");

			var wsr = asyncResult as WriteSyncResult;
			if (wsr != null)
				{
				if (wsr.EndWriteCalled)
					throw new InvalidOperationException ("EndWrite was already called");
				wsr.EndWriteCalled = true;

				return;
				}
			var war = asyncResult as WriteAsyncResult;
			if (war != null)
				{
				if (war.EndWriteCalled)
					throw new InvalidOperationException ("EndWrite was already called");
				war.EndWriteCalled = true;

				if (!war.IsCompleted)
					war.AsyncWaitHandle.Wait ();
				return;
				}

			m_stream.EndWrite (asyncResult);
			}

		private class ReadAsyncResult : IAsyncResult
			{
			private readonly CEvent cevent = new CEvent (false, false);

			public int DataReceived { get; set; }

			public bool EndReadCalled { get; set; }

			#region IAsyncResult Members

			public object AsyncState { get; set; }

			public CEventHandle AsyncWaitHandle
				{
				get { return cevent; }
				}

			public bool CompletedSynchronously { get; set; }

			public object InnerObject
				{
				get { throw new NotImplementedException (); }
				}

			public bool IsCompleted { get; set; }

			#endregion
			}

		private class ReadSyncResult : IAsyncResult
			{
			private CEvent cevent;

			public int DataReceived { get; set; }

			public bool EndReadCalled { get; set; }

			#region IAsyncResult Members

			public object AsyncState { get; set; }

			public CEventHandle AsyncWaitHandle
				{
				get { return cevent ?? (cevent = new CEvent (false, true)); }
				}

			public bool CompletedSynchronously
				{
				get { return true; }
				}

			public object InnerObject
				{
				get { throw new NotImplementedException (); }
				}

			public bool IsCompleted
				{
				get { return true; }
				}

			#endregion
			}

		private class WriteAsyncResult : IAsyncResult
			{
			private readonly CEvent cevent = new CEvent (false, false);

			public bool EndWriteCalled { get; set; }

			#region IAsyncResult Members

			public object AsyncState { get; set; }

			public CEventHandle AsyncWaitHandle
				{
				get { return cevent; }
				}

			public bool CompletedSynchronously { get; set; }

			public object InnerObject
				{
				get { throw new NotImplementedException (); }
				}

			public bool IsCompleted { get; set; }

			#endregion
			}

		private class WriteSyncResult : IAsyncResult
			{
			private CEvent cevent;

			public bool EndWriteCalled { get; set; }

			#region IAsyncResult Members

			public object AsyncState { get; set; }

			public CEventHandle AsyncWaitHandle
				{
				get { return cevent ?? (cevent = new CEvent (false, true)); }
				}

			public bool CompletedSynchronously
				{
				get { return true; }
				}

			public object InnerObject
				{
				get { throw new NotImplementedException (); }
				}

			public bool IsCompleted
				{
				get { return true; }
				}

			#endregion
			}
		}
	}