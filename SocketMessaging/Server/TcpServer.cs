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
			_clients = new List<System.Net.Sockets.TcpClient>();
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

		public IEnumerable<System.Net.Sockets.TcpClient> Clients { get { return _clients.AsEnumerable(); } }

		#region Public events

		public event EventHandler<ClientEventArgs> ClientConnected;
		protected virtual void OnClientConnected(ClientEventArgs e)
		{
			ClientConnected?.Invoke(this, e);
		}

		public event EventHandler<ClientEventArgs> ClientDisconnected;
		protected virtual void OnClientDisconnected(ClientEventArgs e)
		{
			ClientDisconnected?.Invoke(this, e);
		}

		public event EventHandler<ClientEventArgs> ClientReceivedRaw;
		protected virtual void OnClientReceivedRaw(ClientEventArgs e)
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
						client.Client.Receive(buffer);
					}
					else if (!isConnected(client))
					{
						DebugInfo("Client {0} disconnected", index);
						_clients.Remove(client);
						OnClientDisconnected(new ClientEventArgs(client));
					}
				}

				Thread.Sleep(POLLTHREAD_SLEEP);
			}
		}

		private bool isConnected(System.Net.Sockets.TcpClient client)
		{
			if (!client.Client.Connected)
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
				if (!client.Client.Poll(0, SelectMode.SelectRead))
					return true;

				byte[] buff = new byte[1];
				var clientSentData = client.Client.Receive(buff, SocketFlags.Peek) != 0;
				return clientSentData; //False here though Poll() succeeded means we had a disconnect!
			}
			catch (SocketException ex)
			{
				DebugInfo(ex.ToString());
				return false;
			}
		}

		private void acceptAllPendingClients()
		{
			while (_listener.Pending())
			{
				var client = _listener.AcceptTcpClient();
				_clients.Add(client);
				OnClientConnected(new ClientEventArgs(client));
				DebugInfo("Client {0} connected.", _clients.Count);
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
		readonly List<System.Net.Sockets.TcpClient> _clients;

		const int POLLTHREAD_SLEEP = 20;
	}
}
