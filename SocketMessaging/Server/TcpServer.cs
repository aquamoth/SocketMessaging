﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;

namespace SocketMessaging.Server
{
    public class TcpServer
    {
		public TcpServer()
		{
			_connections = new HashSet<Connection>();
		}

		public void Start(int port)
		{
			if (_listener != null)
				throw new InvalidOperationException("Already started.");

			_connectionsSinceStart = 0;
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

		public IEnumerable<Connection> Connections { get { return _connections.AsEnumerable(); } }

		#region Public events

		public event EventHandler<ConnectionEventArgs> Connected;
		protected virtual void OnConnected(ConnectionEventArgs e)
		{
			Helpers.DebugInfo("Connection {0} connected.", e.Connection.Id);
			Connected?.Invoke(this, e);
		}

		#endregion

		#region Private methods

		private void startPollingThread()
		{
			if (_pollThread != null)
				throw new InvalidOperationException("Polling thread already exists.");

			_pollThread = new Thread(new ParameterizedThreadStart(pollThread_run))
			{
				Name = "PollThread",
				IsBackground = true
			};

            _pollThreadCancellationTokenSource = new CancellationTokenSource();
			_pollThread.Start(_pollThreadCancellationTokenSource.Token);
		}

		private void stopPollingThread()
		{
            _pollThreadCancellationTokenSource.Cancel();
            _pollThread.Join();
			_pollThread = null;
		}

		private void pollThread_run(object parameter)
		{
            var cancellationToken = (CancellationToken)parameter;

            while (!cancellationToken.IsCancellationRequested)
			{
				acceptPendingConnections();

				foreach (var connection in _connections)
					connection.Poll();

				_connections.RemoveWhere(c => !c.IsConnected);

				Thread.Sleep(POLLTHREAD_SLEEP);
			}
		}

		private bool isConnected(Connection connection)
		{
			return connection.IsConnected;
		}

		private void acceptPendingConnections()
		{
			while (_listener.Pending())
			{
				var socket = _listener.AcceptSocket();
				var connection = new Connection(++_connectionsSinceStart, socket);
				_connections.Add(connection);
				OnConnected(new ConnectionEventArgs(connection));
			}
		}

		#endregion Private methods


		TcpListenerEx _listener = null;
		internal Thread _pollThread = null;
        CancellationTokenSource _pollThreadCancellationTokenSource;
        readonly HashSet<Connection> _connections;
		int _connectionsSinceStart;
		const int POLLTHREAD_SLEEP = 20;
	}
}
