using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SocketMessaging
{
	public class ConnectionEventArgs : EventArgs
	{
		public Connection Connection { get; private set; }

		public ConnectionEventArgs(Connection connection)
		{
			this.Connection = connection;
		}
	}
}
