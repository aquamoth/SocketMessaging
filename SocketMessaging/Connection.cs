using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace SocketMessaging
{
    /// <summary>
    /// The Connection wraps a Socket. It is responsible for maintaining the connection and handle the polling logic for the receive buffer. Especially important is to trigger events as messages are received or the connection is closed.
    /// The Connection is not meant to be instanced manually but is base class to TcpClient and is contained in the TcpServer's Connections enumeration.
    /// </summary>
    /// <remarks>
    /// While it contains the polling logic in its protected Poll() method Connection is not driving the polling with its own thread. That functionality is delegated to the classes that uses it.
    /// </remarks>
	public class Connection
	{
		public int Id { get; private set; }

		public Socket Socket { get { return _socket; } }

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

        #region Send and Receive

        public int MaxMessageSize {
            get { return _maxMessageSize; }
            set {
                _maxMessageSize = value;
                if (_maxMessageSize > Socket.ReceiveBufferSize)
                    Socket.ReceiveBufferSize = _maxMessageSize;
            }
        }
        int _maxMessageSize = 0;

        public MessageMode Mode { get; protected set; }
		public void SetMode(MessageMode newMode)
		{
			Mode = newMode;
			retriggerMessageReceivedEvents();
		}

		public byte[] Delimiter { get; private set; }
		public void SetDelimiter(byte[] delimiter)
		{
			Delimiter = delimiter;
			retriggerMessageReceivedEvents();
		}
		public void SetDelimiter(byte delimiter)
		{
			SetDelimiter(new byte[] { delimiter });
		}
		public void SetDelimiter(string delimiter)
		{
			SetDelimiter(MessageEncoding.GetBytes(delimiter));
		}
		public void SetDelimiter(char delimiter)
		{
			SetDelimiter(delimiter.ToString());
		}

		public byte Escapecode { get; private set; }
		public void SetEscapecode(byte escapecode)
		{
			Escapecode = escapecode;
			retriggerMessageReceivedEvents();
		}

		public Encoding MessageEncoding { get; set; }

		public void Send(byte[] buffer)
		{
			Helpers.DebugInfo("#{0}: Sending {1} bytes.", Id, buffer.Length);
			if (Mode != MessageMode.Raw && buffer.Length > MaxMessageSize)
				throw new ArgumentException("Message is larger than MaxMessageSize.");

			switch (Mode)
			{
				case MessageMode.Raw:
					_socket.Send(buffer);
					break;

				case MessageMode.DelimiterBound:
					if (Delimiter == null || Delimiter.Length == 0)
						throw new NotSupportedException("Delimiter must be at least one byte for delimited messages.");
					if (Delimiter.Contains(Escapecode))
						throw new NotSupportedException("The escape code can not be part of the message delimiter.");

					var encodedBuffer = appendEscapeCodes(buffer);
					_socket.Send(encodedBuffer);
					_socket.Send(Delimiter);
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

		public void Send(string message)
		{
			var buffer = MessageEncoding.GetBytes(message);
			Send(buffer);
		}

        public byte[] Receive(int maxLength = int.MaxValue, SocketFlags socketFlags = SocketFlags.None)
        {
            var bufferLength = Math.Min(Socket.Available, maxLength);

            var buffer = new byte[bufferLength];
            if (bufferLength > 0)
                Socket.Receive(buffer, socketFlags);

            if (!socketFlags.HasFlag(SocketFlags.Peek))
            {
                _expectedBytesInReceiveBuffer -= bufferLength;

                _indexOfNextMessageToReceive -= bufferLength;
                if (_indexOfNextMessageToReceive < 0)
                    _indexOfNextMessageToReceive = 0;
            }

            Helpers.DebugInfo("#{0}: Received {1} bytes from raw queue. Queue is now {2} bytes.", Id, buffer.Length, Socket.Available);
            return buffer;
        }

        public byte[] ReceiveMessage()
		{
			switch (Mode)
			{
				case MessageMode.DelimiterBound:
					{
						if (Delimiter == null || Delimiter.Length == 0)
							throw new NotSupportedException("Delimiter must be at least one byte for delimited messages.");
						if (Delimiter.Contains(Escapecode))
							throw new NotSupportedException("The escape code can not be part of the message delimiter.");

						var buffer = new byte[0].AsEnumerable();
						var inEscapeMode = false;
						do
						{
							var readBuffer = readSocketUntil(Delimiter, MaxMessageSize);
							if (readBuffer == null)
							{
								if (Socket.Available >= MaxMessageSize)
									throw new InvalidOperationException("Message is larger than max allowed message size.");
								return null;
							}

							var escapedBuffer = removeEscapeCodes(readBuffer, ref inEscapeMode);
							buffer = buffer.Concat(escapedBuffer);
						} while (buffer.Skip(buffer.Count() - Delimiter.Length).SequenceEqual(Delimiter));
						return buffer.ToArray();
					}

				case MessageMode.FixedLength:
					{
						if (Socket.Available < this.MaxMessageSize)
							return null;
						var buffer = Receive(this.MaxMessageSize);
						if (buffer.Length != this.MaxMessageSize)
							throw new ApplicationException(string.Format("Expected message of size {0} but got {1} bytes.", MaxMessageSize, buffer.Length));
						return buffer;
					}

				case MessageMode.PrefixedLength:
					{
                        var messageSizeBuffer = Receive(4, SocketFlags.Peek);
                        if (messageSizeBuffer.Length < 4)
                            return null;

                        var messageSize = BitConverter.ToInt32(messageSizeBuffer, 0);

                        if (messageSize > MaxMessageSize)
							throw new InvalidOperationException("Message is larger than max allowed message size.");

                        if (messageSize > Socket.Available - 4)
                            return null;

                        Receive(4); //Read out the length prefix
                        var buffer = Receive(messageSize);
						return buffer;
					}

				case MessageMode.Raw:
					throw new InvalidOperationException("You must first select a message mode.");

				default:
					throw new NotSupportedException();
			}
		}

        internal byte[] readSocketUntil(byte[] delimiter, int maxMessageSize)
        {
            var peekBuffer = Receive(Socket.Available, SocketFlags.Peek);

            var delimiterIndex = 0;
            var counter = 0;
            var walker = 0;
            while (walker < peekBuffer.Length && counter < maxMessageSize)
            {
                counter++;

                if (peekBuffer[walker] == delimiter[delimiterIndex])
                {
                    delimiterIndex++;
                    if (delimiterIndex == delimiter.Length)
                        return Receive(counter);
                }
                else if (delimiterIndex != 0)
                {
                    counter -= delimiterIndex;
                    walker -= delimiterIndex;
                    delimiterIndex = 0;
                }

                walker++;
            }
            return null;
        }

        public string ReceiveMessageString()
		{
			return MessageEncoding.GetString(ReceiveMessage());
		}

		#endregion Send and Receive

		public void Close()
		{
			_socket.Shutdown(SocketShutdown.Both);
			_socket.Close();
			_socket = null;
		}

		#region Public events

		public event EventHandler ReceivedRaw;
		protected virtual void OnReceivedRaw(EventArgs e)
		{
			Helpers.DebugInfo("#{0}: Connection received {1} bytes", this.Id, Socket.Available);
			ReceivedRaw?.Invoke(this, e);
		}

		public event EventHandler ReceivedMessage;
		protected virtual void OnReceivedMessage(EventArgs e)
		{
			Helpers.DebugInfo("#{0}: Connection received a new message", this.Id, Socket.Available);
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
            else if (_expectedBytesInReceiveBuffer > Socket.Available)
            {
                //If user tries to read directly from socket we don't really know what bytes have been seen!
                throw new InvalidOperationException($"Expected at least {_expectedBytesInReceiveBuffer} bytes in receive buffer but found {Socket.Available}!");
            }
            else if (_expectedBytesInReceiveBuffer != Socket.Available)
            {
                _expectedBytesInReceiveBuffer = Socket.Available;
                OnReceivedRaw(EventArgs.Empty);

                triggerNewReceivedMessageEvents();
            }
        }

        private void triggerNewReceivedMessageEvents()
        {
            int unprocessedIndexOfBuffer;
            var bufferWithPossibleNewMessages = Receive(Socket.Available, SocketFlags.Peek).Skip(_indexOfNextMessageToReceive).ToArray();
            var numberOfNewMessages = numberOfNewMessagesInRawQueue(bufferWithPossibleNewMessages, out unprocessedIndexOfBuffer);
            _indexOfNextMessageToReceive += unprocessedIndexOfBuffer;

            for (var i = 0; i < numberOfNewMessages; i++)
                OnReceivedMessage(EventArgs.Empty);
        }

        private int numberOfNewMessagesInRawQueue(byte[] buffer, out int nextIndexToSearchFrom)
		{
            nextIndexToSearchFrom = 0;
            switch (Mode)
			{
				case MessageMode.Raw:
					Helpers.DebugInfo("#{0}: In raw mode no new messages are found", Id);
                    return 0;

				case MessageMode.DelimiterBound:
					{
                        if (Delimiter.Length == 0)
                            throw new NotSupportedException("In Delimiter mode the Delimiter property must be non-empty!");

						var index = 0;
						var counter = 0;
						while (index < buffer.Length)
						{
							if (buffer.Skip(index).Take(Delimiter.Length).SequenceEqual(Delimiter))
							{
								//TODO: Should make sure there is no escapecode just before the delimiter
								counter++;
								index += Delimiter.Length;
                                nextIndexToSearchFrom = index;
                            }
							else
							{
								index++;
							}
						}
						return counter;
					}

				case MessageMode.PrefixedLength:
					{
						var peekPosition = 0;
						var counter = 0;
                        while (peekPosition <= buffer.Length - 4)
                        {
                            var messageSize = BitConverter.ToInt32(buffer, peekPosition);
                            if (messageSize < 0) break; //This message is likely not meant to be read in current mode, so just abort counter
                            peekPosition += 4 + messageSize;
                            if (peekPosition > buffer.Length)
                                break;

                            nextIndexToSearchFrom = peekPosition;
                            counter++;
                        }
						return counter;
					}

				case MessageMode.FixedLength:
                    {
                        var lengthOfAllNewMessages = buffer.Length - (buffer.Length % MaxMessageSize);

                        var count = lengthOfAllNewMessages / MaxMessageSize;
                        nextIndexToSearchFrom += lengthOfAllNewMessages;

                        return count;
                    }

                default:
					throw new NotSupportedException();
			}
		}

		private void retriggerMessageReceivedEvents()
		{
            _indexOfNextMessageToReceive = 0;
            triggerNewReceivedMessageEvents();
		}

		internal byte[] appendEscapeCodes(byte[] buffer)
		{
			var encodedBuffer = new byte[0].AsEnumerable();
			var startIndex = 0;
			for(var endIndex=0;endIndex<buffer.Length;endIndex++)
			{
				if (buffer[endIndex] == Escapecode)
				{
					encodedBuffer = encodedBuffer.Concat(buffer.Skip(startIndex).Take(endIndex - startIndex));
					startIndex = endIndex; //Add the escapecode again, so it occurs twice
				}
				else if (buffer.Skip(endIndex).Take(Delimiter.Length).SequenceEqual(Delimiter))
				{
					encodedBuffer = encodedBuffer.Concat(buffer.Skip(startIndex).Take(endIndex - startIndex));
					encodedBuffer = encodedBuffer.Concat(new[] { Escapecode });
					startIndex = endIndex; //Add delimiter after escapecode
				}
				else
				{
					//continue matching
				}
			}
			encodedBuffer = encodedBuffer.Concat(buffer.Skip(startIndex));

			return encodedBuffer.ToArray();
		}

		internal byte[] removeEscapeCodes(byte[] message, ref bool inEscapeMode)
		{
			IEnumerable<byte> escapedMessage = new byte[0];

			var startIndex = 0;
			for (var endIndex = 0; endIndex < message.Length; endIndex++)
			{
				var token = message[endIndex];
				if (inEscapeMode)
				{
					if (token == Escapecode)
					{
						escapedMessage = escapedMessage.Concat(message.Skip(startIndex).Take(endIndex - startIndex));
						startIndex = endIndex + 1;
					}
					else if (message.Skip(endIndex).Take(Delimiter.Length).SequenceEqual(Delimiter))
					{
						escapedMessage = escapedMessage.Concat(message.Skip(startIndex).Take(endIndex - startIndex - 1));
						startIndex = endIndex;
					}
					inEscapeMode = false;
				}
				else
				{
					if (message.Skip(endIndex).Take(Delimiter.Length).SequenceEqual(Delimiter))
					{
						if (endIndex != message.Length - Delimiter.Length)
							throw new ApplicationException("Found delimiter inside message.");
						escapedMessage = escapedMessage.Concat(message.Skip(startIndex).Take(endIndex - startIndex));
						startIndex = endIndex + Delimiter.Length;//Skip past the delimiter (to end-of-buffer)
					}
					else if (token == Escapecode)
					{
						inEscapeMode = true;
					}
					else
					{
						//Continue processing
					}
				}
			}
			escapedMessage = escapedMessage.Concat(message.Skip(startIndex));
			return escapedMessage.ToArray();
		}

		#endregion Internal logic

		internal Connection(int id, Socket socket)
		{
			Id = id;
			_socket = socket;
            MaxMessageSize = socket.ReceiveBufferSize;
            Delimiter = new byte[] { 0x0a }; //\n (<CR>) as default delimiter
			Escapecode = Encoding.UTF8.GetBytes(@"\").Single();
			MessageEncoding = Encoding.UTF8;
		}

		protected Socket _socket;
        int _expectedBytesInReceiveBuffer = 0;
        int _indexOfNextMessageToReceive = 0;
    }
}
