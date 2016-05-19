using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Net;

namespace SocketMessaging.Tests
{
	[TestClass]
	public class TcpServerTests : IDisposable
	{
		const int SERVER_PORT = 7732;
		readonly Server.TcpServer server;

		public TcpServerTests()
		{
			Assert.IsFalse(Helpers.IsTcpPortListening(SERVER_PORT), "Port should be closed at start of test.");
			server = new Server.TcpServer();
			server.Start(SERVER_PORT);
		}

		public void Dispose()
		{
			server.Stop();
		}

		[TestMethod]
		public void Starts_and_stops_multiple_times()
		{
			Assert.IsTrue(server.IsStarted);
			Assert.IsTrue(Helpers.IsTcpPortListening(SERVER_PORT), "Port should be open when server has started.");
			var serverPollThread = server._pollThread;

			server.Stop();
			Assert.IsFalse(server.IsStarted);
			Assert.IsFalse(Helpers.IsTcpPortListening(SERVER_PORT), "Port should be closed when server has stopped.");
			Assert.AreEqual(System.Threading.ThreadState.Aborted, serverPollThread.ThreadState, "Polling thread stops when server stops.");

			server.Start(SERVER_PORT);
			Assert.IsTrue(server.IsStarted);
			Assert.IsTrue(Helpers.IsTcpPortListening(SERVER_PORT), "Port should be open when server has started.");
		}

		[TestMethod]
		[ExpectedException(typeof(InvalidOperationException))]
		public void Should_not_start_when_started()
		{
			server.Start(SERVER_PORT + 1);
		}

		[TestMethod]
		public void Exposes_local_endpoint()
		{
			var endpoint = server.LocalEndpoint as IPEndPoint;
			Assert.AreEqual(SERVER_PORT, endpoint.Port);
		}

		[TestMethod]
		public void Enumerates_connected_clients()
		{
			const int SLEEP_TIME = 25;
			var serverAddress = new IPAddress(new byte[] { 127, 0, 0, 1 });

			Assert.AreEqual(0, server.Connections.Count(), "No clients before first connection");

			var client1 = new TcpClient();
			client1.Connect(serverAddress, SERVER_PORT);
			System.Threading.Thread.Sleep(SLEEP_TIME);
			Assert.AreEqual(1, server.Connections.Count(), "One client connected");

			var client2 = new TcpClient();
			client2.Connect(serverAddress, SERVER_PORT);
			System.Threading.Thread.Sleep(SLEEP_TIME);
			Assert.AreEqual(2, server.Connections.Count(), "Two clients connected");

			client1.Disconnect();
			System.Threading.Thread.Sleep(SLEEP_TIME);
			Assert.AreEqual(1, server.Connections.Count(), "One client disconnected");

			client2.Disconnect();
			System.Threading.Thread.Sleep(SLEEP_TIME);
			Assert.AreEqual(0, server.Connections.Count(), "All clients disconnected");
		}
	}
}
