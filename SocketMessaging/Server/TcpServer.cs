using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SocketMessaging.Server
{
    public class TcpServer
    {
		public TcpServer()
		{
			_clients = new List<Connection>();
		}

		public void Start(int port)
		{
			if (_listener != null)
				throw new InvalidOperationException("Already started.");

			var address = new IPAddress(0);
			_listener = new TcpListenerEx(address, port);
			_listener.Start();

			startPollingThread();
		}

		public bool IsStarted { get { return _listener != null && _listener.Active; } }

		public void Stop()
		{
			if (_listener == null)
				throw new InvalidOperationException("Not started");

			stopPollingThread();
			_listener.Stop();
			_listener = null;
		}

		public IPEndPoint LocalEndpoint { get { return _listener.LocalEndpoint as IPEndPoint; } }

		public IEnumerable<Connection> Clients { get { return _clients.AsEnumerable(); } }

		#region Public events

		public event EventHandler<ConnectionEventArgs> ClientConnected;
		protected virtual void OnClientConnected(ConnectionEventArgs e)
		{
			ClientConnected?.Invoke(this, e);
		}

		public event EventHandler<ConnectionEventArgs> ClientDisconnected;
		protected virtual void OnClientDisconnected(ConnectionEventArgs e)
		{
			ClientDisconnected?.Invoke(this, e);
		}

		public event EventHandler<ConnectionEventArgs> ClientReceivedRaw;
		protected virtual void OnClientReceivedRaw(ConnectionEventArgs e)
		{
			ClientReceivedRaw?.Invoke(this, e);
		}

		#endregion

		#region Private methods

		private void startPollingThread()
		{
			if (_pollThread != null)
				throw new InvalidOperationException("Polling thread already exists.");

			_pollThread = new Thread(new ThreadStart(pollThread_run))
			{
				Name = "PollThread",
				IsBackground = true
			};

			_pollThread.Start();
		}

		private void stopPollingThread()
		{
			_pollThread.Abort();
			_pollThread = null;
		}

		private void pollThread_run()
		{
			while (true)
			{
				acceptAllPendingClients();

				for (var index = _clients.Count - 1; index >= 0; index--)
				{
					//DebugInfo("Polling client {0}...", index);
					var client = _clients[index];
					if (client.Available > 0)
					{
						DebugInfo("Client {0} sent {1} bytes", index, client.Available);
						var buffer = new byte[client.Available];
						client.Receive(buffer);
					}
					else if (!isConnected(client))
					{
						DebugInfo("Client {0} disconnected", index);
						_clients.Remove(client);
						OnClientDisconnected(new ConnectionEventArgs(client));
					}
				}

				Thread.Sleep(POLLTHREAD_SLEEP);
			}
		}

		private bool isConnected(Connection connection)
		{
			return connection.IsConnected;
		}

		private void acceptAllPendingClients()
		{
			while (_listener.Pending())
			{
				var client = _listener.AcceptTcpClient();
				var connection = new Connection(0, client);
				_clients.Add(connection);
				OnClientConnected(new ConnectionEventArgs(connection));
				DebugInfo("Client {0} connected.", connection.Id);
			}
		}

		#endregion Private methods

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


		TcpListenerEx _listener = null;
		internal Thread _pollThread = null;
		readonly List<Connection> _clients;

		const int POLLTHREAD_SLEEP = 20;
	}
}
