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
    /// <summary>
    /// The TcpClient is used establish a connection with a tcp server. 
    /// It is not confined to talking to TcpServer's but can connect 
    /// and exchange messages with any tcp server.
    /// After the connection is established TcpClient's main responsibility 
    /// is to driver the Connection's protected Poll() method that receives 
    /// messages and identifies closed connections.
    /// </summary>
    /// <example>@code
    /// var serverAddress = Server.Net.Dns.GetHostAddresses("chatbot.example.com").First();
    /// var serverPort = 80;
    /// var client = TcpClient.Connect(serverAddress, serverPort);
    /// var data = client.Receive();
    /// client.Send(data);
    /// client.Close();
    /// @endcode</example>
	public class TcpClient : Connection//, IDisposable
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

            try
            {
				Helpers.DebugInfo("#{0}: Polling thread started", Id);
                while (!cancellationToken.IsCancellationRequested)
                {
                    //Helpers.DebugInfo("#{0}: Polling...", Id);
                    this.Poll();
					Thread.Sleep(POLLTHREAD_SLEEP);
				}
				Helpers.DebugInfo("#{0}: Polling thread stopped.", Id);
			}
			catch (Exception ex)
			{
				System.Diagnostics.Trace.TraceError("Error in polling thread!\n{0}", ex.Message);
			}
		}

		#endregion Private methods

		internal Thread _pollThread = null;
        CancellationTokenSource _pollThreadCancellationTokenSource;
        const int POLLTHREAD_SLEEP = 20;

        /// <summary>
        /// Connects to a server and returns a TcpClient that handles the lifetime 
        /// of the connection.
        /// </summary>
        /// <param name="address">The ip address to connect to.</param>
        /// <param name="port">The tcp port to connect to.</param>
        /// <returns>An established connection to the server.</returns>
		public static TcpClient Connect(IPAddress address, int port)
		{
			var socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
			socket.Connect(address, port);
			var client = new TcpClient(socket);
			return client;
		}
	}
}
