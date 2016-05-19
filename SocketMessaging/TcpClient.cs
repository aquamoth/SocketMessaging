using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace SocketMessaging
{
	public class TcpClient
	{
		Server.Connection _client;

		//public void Connect(IPEndPoint endpoint)
		//{
		//	_client = new System.Net.Sockets.TcpClient();
		//	_client.Connect(endpoint);
		//}
		public void Connect(IPAddress address, int port)
		{
			var socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
			socket.Connect(address, port);
			_client = new Server.Connection(0, socket);
		}

		public bool IsConnected { get { return _client != null && _client.IsConnected; } }

		public bool Available { get { return _client != null && _client.Available > 0; } }

		public void Disconnect()
		{
			_client.Close();
			_client = null;
		}

		public void Send(byte[] buffer)
		{
			_client.Send(buffer);
		}

		public int Receive(byte[] buffer, int actualLength, int v, SocketFlags none)
		{
			throw new NotImplementedException();
		}
	}
}
