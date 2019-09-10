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
		[TestCategory("TcpServer")]
		public void Starts_and_stops_multiple_times()
		{
			Assert.IsTrue(server.IsStarted);
			Assert.IsTrue(Helpers.IsTcpPortListening(SERVER_PORT), "Port should be open when server has started.");
			var serverPollThread = server._pollThread;

			server.Stop();
			Assert.IsFalse(server.IsStarted);
			Assert.IsFalse(Helpers.IsTcpPortListening(SERVER_PORT), "Port should be closed when server has stopped.");
			Assert.AreEqual(System.Threading.ThreadState.Stopped, serverPollThread.ThreadState, "Polling thread stops when server stops.");

			server.Start(SERVER_PORT);
			Assert.IsTrue(server.IsStarted);
			Assert.IsTrue(Helpers.IsTcpPortListening(SERVER_PORT), "Port should be open when server has started.");
		}

		[TestMethod]
		[TestCategory("TcpServer")]
		[ExpectedException(typeof(InvalidOperationException))]
		public void Should_not_start_when_started()
		{
			server.Start(SERVER_PORT + 1);
		}

		[TestMethod]
		[TestCategory("TcpServer")]
		public void Exposes_local_endpoint()
		{
			var endpoint = server.LocalEndpoint as IPEndPoint;
			Assert.AreEqual(SERVER_PORT, endpoint.Port);
		}

		[TestMethod]
		[TestCategory("TcpServer")]
		public void Server_triggers_Connected()
		{
			Connection connectedClient = null;

			server.Connected += (s1, e1) => {
				connectedClient = e1.Connection;
			};

			Assert.IsNull(connectedClient, "Server should not publish connected client before connection.");
			var serverAddress = new IPAddress(new byte[] { 127, 0, 0, 1 });
			var client = TcpClient.Connect(serverAddress, SERVER_PORT);
			try
			{
				Helpers.WaitFor(() => connectedClient != null);
				Assert.IsNotNull(connectedClient, "Server should publish connected client after connection.");
			}
			finally
			{
				client.Close();
			}
		}

		[TestMethod]
		[TestCategory("TcpServer")]
		public void Enumerates_connected_clients()
		{
			var serverAddress = new IPAddress(new byte[] { 127, 0, 0, 1 });

			Assert.AreEqual(0, server.Connections.Count(), "No clients before first connection");

			var client1 = TcpClient.Connect(serverAddress, SERVER_PORT);
			Helpers.WaitFor(() => server.Connections.Any());
			Assert.AreEqual(1, server.Connections.Count(), "One client connected");
			Assert.AreEqual(1, server.Connections.First().Id, "Connection Id");

			var client2 = TcpClient.Connect(serverAddress, SERVER_PORT);
			Helpers.WaitFor(() => server.Connections.Count() >= 2);
			Assert.AreEqual(2, server.Connections.Count(), "Two clients connected");
			Assert.AreEqual(1, server.Connections.First().Id, "Connection Id");
			Assert.AreEqual(2, server.Connections.Skip(1).First().Id, "Connection Id");

			client1.Close();
			Helpers.WaitFor(() => server.Connections.Count() != 2);
			Assert.AreEqual(1, server.Connections.Count(), "One client disconnected");
			Assert.AreEqual(2, server.Connections.First().Id, "Connection Id");

			client2.Close();
			Helpers.WaitFor(() => !server.Connections.Any());
			Assert.AreEqual(0, server.Connections.Count(), "All clients disconnected");
		}
	}
}
