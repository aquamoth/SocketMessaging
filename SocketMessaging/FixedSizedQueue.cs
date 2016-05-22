using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SocketMessaging
{
	public class FixedSizedQueue
	{
		public FixedSizedQueue(int queueSize)
		{
			_queue = new byte[queueSize + 1]; //Add a token-byte to the queue to discern start-of-read with start-of-write
		}

		public int Count { get { return (_queue.Length + _writeIndex - _readIndex) % _queue.Length; } }

		public int UnusedQueueLength { get { return (_queue.Length + _readIndex - _writeIndex - 1) % _queue.Length; } }

		public void Write(byte[] buffer)
		{
			var bufferIndex = 0;
			if (_writeIndex >= _readIndex)
			{
				bufferIndex = Math.Min(buffer.Length, _queue.Length - _writeIndex - (_readIndex == 0 ? 1 : 0));
				Array.Copy(buffer, 0, _queue, _writeIndex, bufferIndex);
				_writeIndex = (_writeIndex + bufferIndex) % _queue.Length;
			}

			var numberOfBytesLeftToWrite = buffer.Length - bufferIndex;
			if (numberOfBytesLeftToWrite > 0)
			{
				//Here is always _writeIndex < _readIndex
				//Except when _readIndex == 0, in which case _writeIndex == _queue.Length, but that's ok because freeQueueLength will just be negative instead of 0.
				if (numberOfBytesLeftToWrite > UnusedQueueLength)
					throw new OverflowException("Entire buffer does not fit into the queue");

				Array.Copy(buffer, bufferIndex, _queue, _writeIndex, numberOfBytesLeftToWrite);
				_writeIndex += numberOfBytesLeftToWrite;
			}
		}

		internal byte[] Peek(int peekPosition, int numberOfBytes)
		{
			if (peekPosition + numberOfBytes >= Count)
				throw new OverflowException("Cant peek outside end of queue");

			var buffer = new byte[numberOfBytes];

			var bufferIndex = 0;
			var startIndex = (_readIndex + peekPosition) % _queue.Length;
			if (numberOfBytes > _queue.Length - startIndex)
			{
				bufferIndex = _queue.Length - startIndex;
				Array.Copy(_queue, startIndex, buffer, 0, bufferIndex);
				startIndex = 0;
			}

			Array.Copy(_queue, startIndex, buffer, bufferIndex, buffer.Length - bufferIndex);

			return buffer;
		}

		internal byte[] Read(int maxReadSize = 0)
		{
			var bufferLength = maxReadSize == 0 
				? this.Count 
				: Math.Min(this.Count, maxReadSize);

			var buffer = new byte[bufferLength];

			var bufferIndex = 0;
			if (bufferLength > _queue.Length - _readIndex)
			{
				bufferIndex = _queue.Length - _readIndex;
				Array.Copy(_queue, _readIndex, buffer, 0, bufferIndex);
				_readIndex = 0;
			}

			Array.Copy(_queue, _readIndex, buffer, bufferIndex, buffer.Length - bufferIndex);
			_readIndex += buffer.Length - bufferIndex;

			return buffer;
		}

		internal byte[] ReadUntil(byte delimiter, int maxReadSize)
		{
			var counter = 0;
			var walker = _readIndex;
			while (walker != _writeIndex && counter < maxReadSize)
			{
				if (_queue[walker] == delimiter)
					return Read(counter + 1);
				walker++;
				if (walker == _queue.Length)
					walker = 0;
				counter++;
			}
			return null;
		}



		readonly byte[] _queue;
		int _writeIndex = 0;
		int _readIndex = 0;
	}
}
