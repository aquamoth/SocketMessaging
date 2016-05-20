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
			Task.Run(() => stopPollingThread()); //Without a task, the thread would stop itself instead of being aborted
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
		}

		private void stopPollingThread()
		{
			_pollThread.Abort();
			_pollThread = null;
		}

		private void pollThread_run()
		{
			try
			{

				Helpers.DebugInfo("#{0}: Polling thread started", Id);
				while (true)
				{
					//Helpers.DebugInfo("#{0}: Polling...", Id);
					this.Poll();
					Thread.Sleep(POLLTHREAD_SLEEP);
				}
			}
			catch (ThreadAbortException)
			{
				Helpers.DebugInfo("#{0}: Polling thread stopped.", Id);
			}
			catch (Exception ex)
			{
				System.Diagnostics.Trace.TraceError("Error in polling thread!\n{0}", ex.Message);
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
