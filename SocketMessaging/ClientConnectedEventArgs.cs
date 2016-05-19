using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SocketMessaging
{
	public class ClientConnectedEventArgs : EventArgs
	{
		public System.Net.Sockets.TcpClient Client { get; private set; }

		public ClientConnectedEventArgs(System.Net.Sockets.TcpClient client)
		{
			this.Client = client;
		}
	}
}
