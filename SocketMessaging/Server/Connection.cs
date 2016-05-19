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

		public int Available { get { return _client.Available; } }
		public bool IsConnected { get
			{
				if (!_client.Client.Connected)
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
					if (!_client.Client.Poll(0, SelectMode.SelectRead))
						return true;

					byte[] buff = new byte[1];
					var clientSentData = _client.Client.Receive(buff, SocketFlags.Peek) != 0;
					return clientSentData; //False here though Poll() succeeded means we had a disconnect!
				}
				catch (SocketException ex)
				{
					DebugInfo(ex.ToString());
					return false;
				}
			}
		}

		public void Receive(byte[] buffer)
		{
			_client.Client.Receive(buffer);
		}

		public int Receive(byte[] buffer, int offset, int size, SocketFlags socketFlags)
		{
			return _client.Client.Receive(buffer, offset, size, socketFlags);
		}


		internal Connection(int id, System.Net.Sockets.TcpClient client)
		{
			Id = id;
			_client = client;
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

		readonly System.Net.Sockets.TcpClient _client;
	}
}
