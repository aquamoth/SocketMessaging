using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace SocketMessaging
{
    public class TcpServer
    {
		public void Start(int port)
		{
			if (_listener != null)
				throw new InvalidOperationException("Already started.");

			var address = new IPAddress(0);
			_listener = new TcpListenerEx(address, port);
			_listener.Start();
		}

		public bool IsStarted { get { return _listener != null && _listener.Active; } }

		public void Stop()
		{
			if (_listener == null)
				throw new InvalidOperationException("Not started");

			_listener.Stop();
			_listener = null;
		}

		public IPEndPoint LocalEndpoint { get { return _listener.LocalEndpoint as IPEndPoint; } }


		TcpListenerEx _listener;
	}
}
