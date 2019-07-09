//
// System.Net.Sockets.SocketError.cs
//
// Author:
//	Robert Jordan  <robertj@gmx.net>
//
// Copyright (C) 2005 Novell, Inc. (http://www.novell.com)
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

using System.Collections.Generic;
using SEC = Crestron.SimplSharp.CrestronSockets.SocketErrorCodes;
using SS= Crestron.SimplSharp.CrestronSockets.SocketStatus;

namespace SSMono.Net.Sockets
	{
	public enum SocketError
		{
		AccessDenied = 10013,
		AddressAlreadyInUse = 10048,
		AddressFamilyNotSupported = 10047,
		AddressNotAvailable = 10049,
		AlreadyInProgress = 10037,
		ConnectionAborted = 10053,
		ConnectionRefused = 10061,
		ConnectionReset = 10054,
		DestinationAddressRequired = 10039,
		Disconnecting = 10101,
		Fault = 10014,
		HostDown = 10064,
		HostNotFound = 11001,
		HostUnreachable = 10065,
		InProgress = 10036,
		Interrupted = 10004,
		InvalidArgument = 10022,
		IOPending = 997,
		IsConnected = 10056,
		MessageSize = 10040,
		NetworkDown = 10050,
		NetworkReset = 10052,
		NetworkUnreachable = 10051,
		NoBufferSpaceAvailable = 10055,
		NoData = 11004,
		NoRecovery = 11003,
		NotConnected = 10057,
		NotInitialized = 10093,
		NotSocket = 10038,
		OperationAborted = 995,
		OperationNotSupported = 10045,
		ProcessLimit = 10067,
		ProtocolFamilyNotSupported = 10046,
		ProtocolNotSupported = 10043,
		ProtocolOption = 10042,
		ProtocolType = 10041,
		Shutdown = 10058,
		SocketError = -1,
		SocketNotSupported = 10044,
		Success = 0,
		SystemNotReady = 10091,
		TimedOut = 10060,
		TooManyOpenSockets = 10024,
		TryAgain = 11002,
		TypeNotFound = 10109,
		VersionNotSupported = 10092,
		WouldBlock = 10035
		}

	public static class SocketResultExtensions
		{
		private static readonly Dictionary<SEC, SocketError> dictErrorToError = new Dictionary<SEC, SocketError>
			{
				{SEC.SOCKET_ADDRESS_NOT_SPECIFIED, SocketError.DestinationAddressRequired},
				{SEC.SOCKET_BUFFER_NOT_ALLOCATED, SocketError.NoBufferSpaceAvailable},
				{SEC.SOCKET_CONNECTION_IN_PROGRESS, SocketError.AlreadyInProgress},
				{SEC.SOCKET_INVALID_ADDRESS_ADAPTER_BINDING, SocketError.NetworkUnreachable},
				{SEC.SOCKET_INVALID_CLIENT_INDEX, SocketError.InvalidArgument},
				{SEC.SOCKET_INVALID_PORT_NUMBER, SocketError.AddressNotAvailable},
				{SEC.SOCKET_INVALID_STATE, SocketError.OperationNotSupported},
				{SEC.SOCKET_MAX_CONNECTIONS_REACHED, SocketError.TooManyOpenSockets},
				{SEC.SOCKET_NO_HOSTNAME_RESOLVE, SocketError.HostNotFound},
				{SEC.SOCKET_NOT_ALLOWED_IN_SECURE_MODE, SocketError.ProtocolNotSupported},
				{SEC.SOCKET_NOT_CONNECTED, SocketError.NotConnected},
				{SEC.SOCKET_OK, SocketError.Success},
				{SEC.SOCKET_OPERATION_PENDING, SocketError.InProgress},
				{SEC.SOCKET_OUT_OF_MEMORY, SocketError.NoBufferSpaceAvailable},
				{SEC.SOCKET_SPECIFIED_PORT_ALREADY_IN_USE, SocketError.AddressAlreadyInUse},
				{SEC.SOCKET_UDP_SERVER_RECEIVE_ONLY, SocketError.OperationNotSupported}
			};

		public static SocketError ToError (this SEC sec)
			{
			SocketError err;
			if (dictErrorToError.TryGetValue (sec, out err))
				return err;

			return SocketError.SocketError;
			}

		private static readonly Dictionary<SS, SocketError> dictStatusToError = new Dictionary<SS, SocketError>
			{
				{SS.SOCKET_STATUS_BROKEN_LOCALLY, SocketError.ConnectionReset},
				{SS.SOCKET_STATUS_BROKEN_REMOTELY, SocketError.ConnectionReset},
				{SS.SOCKET_STATUS_CONNECT_FAILED, SocketError.HostUnreachable},
				{SS.SOCKET_STATUS_CONNECTED, SocketError.Success},
				{SS.SOCKET_STATUS_DNS_FAILED, SocketError.HostNotFound},
				{SS.SOCKET_STATUS_DNS_LOOKUP, SocketError.InProgress},
				{SS.SOCKET_STATUS_DNS_RESOLVED, SocketError.InProgress},
				{SS.SOCKET_STATUS_LINK_LOST, SocketError.ConnectionAborted},
				{SS.SOCKET_STATUS_NO_CONNECT, SocketError.NotConnected},
				{SS.SOCKET_STATUS_SOCKET_NOT_EXIST, SocketError.NotSocket},
				{SS.SOCKET_STATUS_WAITING, SocketError.InProgress}
			};

		public static SocketError ToError (this SS ss)
			{
			SocketError err;
			if (dictStatusToError.TryGetValue (ss, out err))
				return err;

			return SocketError.SocketError;
			}

		}
	}