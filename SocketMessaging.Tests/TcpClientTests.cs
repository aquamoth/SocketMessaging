using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Diagnostics;
using SocketMessaging.Server;
using System.Collections.Generic;

namespace SocketMessaging.Tests
{
	[TestClass]
	public class TcpClientTests : IDisposable
	{
		const int SERVER_PORT = 7783;
		readonly Server.TcpServer server;
		TcpClient client = null;

		public TcpClientTests()
		{
			server = new Server.TcpServer();
			server.Start(SERVER_PORT);
		}

		public void Dispose()
		{
			if (client != null && client.IsConnected)
				client.Close();

			if (server.IsStarted)
				server.Stop();
		}

		[TestMethod]
		[TestCategory("TcpClient")]
		public void Can_connect_and_disconnect_to_running_server()
		{
			connectClient();
			Assert.IsTrue(client.IsConnected, "IsConnected should be true after connection.");
			var clientPollThread = client._pollThread;
			client.Close();
			Assert.IsFalse(client.IsConnected, "IsConnected should be false after disconnection.");
			Helpers.WaitFor(() => clientPollThread.ThreadState == System.Threading.ThreadState.Aborted);
			Assert.AreEqual(System.Threading.ThreadState.Aborted, clientPollThread.ThreadState, "Polling thread stops when client disconnects");
		}

		[TestMethod]
		[TestCategory("TcpClient")]
		[ExpectedException(typeof(SocketException))]
		public void Does_not_connect_to_closed_server()
		{
			server.Stop();
			connectClient();
		}



		private void connectClient()
		{
			var serverAddress = new IPAddress(new byte[] { 127, 0, 0, 1 });
			client = TcpClient.Connect(serverAddress, SERVER_PORT);
		}
	}
}
