using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SocketMessaging.Server
{
	public class ConnectionEventArgs : EventArgs
	{
		public Connection Client { get; private set; }

		public ConnectionEventArgs(Connection connection)
		{
			this.Client = connection;
		}
	}
}
