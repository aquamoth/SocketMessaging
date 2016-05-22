using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace SocketMessaging.Server
{
	public class Connection
	{
		public int Id { get; private set; }

		public int Available { get { return _rawQueue.Count; } }

		public bool IsConnected
		{
			get
			{
				try
				{
					if (_socket == null || !_socket.Connected)
						return false;

					/* pear to the documentation on Poll:
					 * When passing SelectMode.SelectRead as a parameter to the Poll method it will return 
					 * -either- true if Socket.Listen(Int32) has been called and a connection is pending;
					 * -or- true if data is available for reading; 
					 * -or- true if the connection has been closed, reset, or terminated; 
					 * otherwise, returns false
					 */
					if (!_socket.Poll(0, SelectMode.SelectRead))
						return true;

					byte[] buff = new byte[1];
					var clientSentData = _socket.Receive(buff, SocketFlags.Peek) != 0;
					return clientSentData; //False here though Poll() succeeded means we had a disconnect!
				}
				catch (SocketException ex)
				{
					Helpers.DebugInfo("#{0}: {1}", Id, ex);
					return false;
				}
			}
		}

		public Encoding MessageEncoding { get; set; }

		#region Raw

		public void Send(string message)
		{
			var buffer = MessageEncoding.GetBytes(message);
			Send(buffer);
		}

		public void Send(byte[] buffer)
		{
			Helpers.DebugInfo("#{0}: Sending {1} bytes.", Id, buffer.Length);
			switch (Mode)
			{
				case MessageMode.Raw:
					_socket.Send(buffer);
					break;
				case MessageMode.DelimiterBound:
					_socket.Send(buffer);
					_socket.Send(new[] { Delimiter });
					break;
				case MessageMode.PrefixedLength:
					var sizeOfMessage = BitConverter.GetBytes(buffer.Length);
					_socket.Send(sizeOfMessage);
					_socket.Send(buffer);
					break;
				case MessageMode.FixedLength:
					if (buffer.Length != MaxMessageSize)
						throw new ArgumentException(string.Format("Message is {0} bytes but expected {1} bytes.", buffer.Length, MaxMessageSize));
					_socket.Send(buffer);
					break;
				default:
					throw new NotSupportedException();
			}
		}

		public byte[] Receive(int maxLength = 0)
		{
			var buffer = _rawQueue.Read(maxLength);
			Helpers.DebugInfo("#{0}: Received {1} bytes from raw queue. Queue is now {2} bytes.", Id, buffer.Length, _rawQueue.Count);
			return buffer;
		}

		//public int Receive(byte[] buffer, int offset, int size, SocketFlags socketFlags)
		//{
		//	_triggeredReceiveEventSinceRead = false;
		//	return _socket.Receive(buffer, offset, size, socketFlags);
		//}

		#endregion Raw

		#region Messages

		public int MaxMessageSize { get; set; }

		public byte Delimiter { get; set; }

		public MessageMode Mode { get; set; }

		public byte[] ReceiveMessage()
		{
			switch (Mode)
			{
				case MessageMode.DelimiterBound:
					{
						var buffer = _rawQueue.ReadUntil(Delimiter, MaxMessageSize);
						if (buffer == null)
							return null;
						return buffer.Take(buffer.Length - 1).ToArray();
					}

				case MessageMode.FixedLength:
					{
						if (_rawQueue.Count < this.MaxMessageSize)
							return null;
						var buffer = Receive(this.MaxMessageSize);
						if (buffer.Length != this.MaxMessageSize)
							throw new ApplicationException(string.Format("Expected message of size {0} but got {1} bytes.", MaxMessageSize, buffer.Length));
						return buffer;
					}

				case MessageMode.PrefixedLength:
					{
						if (_rawQueue.Count < 4)
							return null;
						
						var messageSize = BitConverter.ToInt32(_rawQueue.Peek(0, 4), 0);
						if (messageSize > _rawQueue.Count + 4)
							return null;

						_rawQueue.Read(4);
						var buffer = _rawQueue.Read(messageSize);
						return buffer;
					}

				case MessageMode.Raw:
					throw new InvalidOperationException("You must first select a message mode.");

				default:
					throw new NotSupportedException();
			}
		}

		#endregion Messages

		public void Close()
		{
			_socket.Close();
			_socket = null;
		}

		#region Public events

		public event EventHandler ReceivedRaw;
		protected virtual void OnReceivedRaw(EventArgs e)
		{
			Helpers.DebugInfo("#{0}: Connection received {1} bytes", this.Id, this.Available);
			ReceivedRaw?.Invoke(this, e);
		}

		public event EventHandler ReceivedMessage;
		protected virtual void OnReceivedMessage(EventArgs e)
		{
			Helpers.DebugInfo("#{0}: Connection received a new message", this.Id, this.Available);
			ReceivedMessage?.Invoke(this, e);
		}
		

		public event EventHandler Disconnected;
		protected virtual void OnDisconnected(EventArgs e)
		{
			Disconnected?.Invoke(this, e);
		}

		#endregion
		
		#region Internal logic

		internal void Poll()
		{
			//Helpers.DebugInfo("#{0} Polling connection...", Id);
			if (!this.IsConnected)
			{
				Helpers.DebugInfo("#{0} Connection disconnected", this.Id);
				OnDisconnected(EventArgs.Empty);
			}
			else
			{
				readIntoRawQueue();
			}
		}

		private void readIntoRawQueue()
		{
			if (_socket.Available == 0)
				return;

			if (_rawQueue.UnusedQueueLength <= 0)
			{
				Helpers.DebugInfo("#{0}: Not receiving {1} because queue is full.", Id, _socket.Available);
				return;
			}

			var bufferSize = Math.Min(_rawQueue.UnusedQueueLength, _socket.Available);
			var buffer = new byte[bufferSize];
			Helpers.DebugInfo("#{0}: Reading {1} bytes into raw queue.", Id, bufferSize);
			_socket.Receive(buffer);
			_rawQueue.Write(buffer);

			OnReceivedRaw(EventArgs.Empty);

			var numberOfNewMessages = numberOfNewMessagesInRawQueue(buffer);
			for (var i = 0; i < numberOfNewMessages; i++)
				OnReceivedMessage(EventArgs.Empty);
		}

		private int numberOfNewMessagesInRawQueue(byte[] buffer)
		{
			switch (Mode)
			{
				case MessageMode.Raw:
					Helpers.DebugInfo("#{0}: In raw mode no new messages are found", Id);
					return 0;

				case MessageMode.DelimiterBound:
					return buffer.Where(b => b == Delimiter).Count();

				case MessageMode.PrefixedLength:
					var queueLengthBeforeBuffer = _rawQueue.Count - buffer.Length;
					var peekPosition = 0;
					var counter = 0;
					while (peekPosition < _rawQueue.Count)
					{
						var messageSize = BitConverter.ToInt32(_rawQueue.Peek(peekPosition, 4), 0);
						peekPosition += 4 + messageSize;
						if (peekPosition >= queueLengthBeforeBuffer && peekPosition <= _rawQueue.Count)
							counter++;
					}
					return counter;

				case MessageMode.FixedLength:
					var pendingBytes = _rawQueue.Count % MaxMessageSize;
					var count = (pendingBytes + buffer.Length) / MaxMessageSize;
					Helpers.DebugInfo("#{0}: With {1} pending bytes and {2} new bytes, with {3} bytes message size, {4} new messages are identified.", Id, pendingBytes, buffer.Length, MaxMessageSize, count);
					return count;

				default:
					throw new NotSupportedException();
			}
		}

		#endregion Internal logic

		internal Connection(int id, Socket socket)
		{
			Id = id;
			_socket = socket;
			MaxMessageSize = 65535; //Same size as default socket window
			Delimiter = 0x0a; //\n (<CR>) as default delimiter
			MessageEncoding = Encoding.UTF8;
			_rawQueue = new FixedSizedQueue(MaxMessageSize);
		}

		protected Socket _socket;
		readonly FixedSizedQueue _rawQueue;
	}
}
