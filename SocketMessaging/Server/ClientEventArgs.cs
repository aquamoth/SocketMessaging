using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SocketMessaging.Server
{
	public class ClientEventArgs : EventArgs
	{
		public System.Net.Sockets.TcpClient Client { get; private set; }

		public ClientEventArgs(System.Net.Sockets.TcpClient client)
		{
			this.Client = client;
		}
	}
}
