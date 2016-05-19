using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SocketMessaging
{
	public class TcpClient : Server.Connection//, IDisposable
	{
		private TcpClient(Socket socket) 
			: base(0, socket)
		{
			startPollingThread();
		}

		//public void Dispose()
		//{
		//	if (this.IsConnected)
		//		this.Close();
		//}

		protected override void OnDisconnected(EventArgs e)
		{
			base.OnDisconnected(e);
			stopPollingThread();
		}

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
			System.Diagnostics.Trace.TraceInformation("#{0}: Polling thread started", Id);
		}

		private void stopPollingThread()
		{
			_pollThread.Abort();
			_pollThread = null;
			System.Diagnostics.Trace.TraceInformation("#{0}: Polling thread stopped", Id);
		}

		private void pollThread_run()
		{
			while (true)
			{
				//System.Diagnostics.Trace.TraceInformation("#{0}: Polling...", Id);
				this.Poll();
				Thread.Sleep(POLLTHREAD_SLEEP);
			}
		}

		#endregion Private methods

		internal Thread _pollThread = null;
		const int POLLTHREAD_SLEEP = 20;


		public static TcpClient Connect(IPAddress address, int port)
		{
			var socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
			socket.Connect(address, port);
			var client = new TcpClient(socket);
			return client;
		}
	}
}
