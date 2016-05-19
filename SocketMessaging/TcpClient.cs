using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace SocketMessaging
{
	public class TcpClient
	{
		System.Net.Sockets.TcpClient _client;

		//public void Connect(IPEndPoint endpoint)
		//{
		//	_client = new System.Net.Sockets.TcpClient();
		//	_client.Connect(endpoint);
		//}
		public void Connect(IPAddress address, int port)
		{
			_client = new System.Net.Sockets.TcpClient();
			_client.Connect(address, port);
		}

		public bool IsConnected { get { return _client != null && _client.Connected; } }

		public void Disconnect()
		{
			_client.Close();
			_client = null;
		}

		internal void Send(byte[] buffer)
		{
			_client.Client.Send(buffer);
		}
	}
}
