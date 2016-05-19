using System;
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

		public int Available { get { return _socket.Available; } }

		public bool IsConnected { get
			{
				if (!_socket.Connected)
					return false;

				try
				{
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
					DebugInfo(ex.ToString());
					return false;
				}
			}
		}

		#region Public events

		public event EventHandler ReceivedRaw;
		protected virtual void OnReceivedRaw(EventArgs e)
		{
			if (!_triggeredReceiveEventSinceRead)
			{
				_triggeredReceiveEventSinceRead = true;
				DebugInfo("Connection {0} received {1} bytes", this.Id, this.Available);
				ReceivedRaw?.Invoke(this, e);
			}
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
			//DebugInfo("Polling connection {0}...", index);
			if (this.Available > 0)
			{
				OnReceivedRaw(EventArgs.Empty);
			}
			else if (!this.IsConnected)
			{
				DebugInfo("Connection {0} disconnected", this.Id);
				OnDisconnected(EventArgs.Empty);
			}
		}

		internal int Receive(byte[] buffer)
		{
			_triggeredReceiveEventSinceRead = false;
			return _socket.Receive(buffer);
		}

		internal int Receive(byte[] buffer, int offset, int size, SocketFlags socketFlags)
		{
			_triggeredReceiveEventSinceRead = false;
			return _socket.Receive(buffer, offset, size, socketFlags);
		}

		#endregion Internal logic

		internal Connection(int id, Socket socket)
		{
			Id = id;
			_socket = socket;
		}

		#region Debug logging

		[System.Diagnostics.Conditional("DEBUG")]
		void DebugInfo(string format, params object[] args)
		{
			if (_debugInfoTime == null)
			{
				_debugInfoTime = new System.Diagnostics.Stopwatch();
				_debugInfoTime.Start();
			}
			System.Diagnostics.Debug.WriteLine(_debugInfoTime.ElapsedMilliseconds + ": " + format, args);
		}

		System.Diagnostics.Stopwatch _debugInfoTime;

		#endregion Debug logging

		readonly Socket _socket;
		bool _triggeredReceiveEventSinceRead = false;
	}
}
