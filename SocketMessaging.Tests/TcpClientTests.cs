using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Net.Sockets;
using System.Net;

namespace SocketMessaging.Tests
{
	[TestClass]
	public class TcpClientTests : IDisposable
	{
		const int SERVER_PORT = 7783;
		readonly TcpServer server;

		public TcpClientTests()
		{
			server = new TcpServer();
			server.Start(SERVER_PORT);
		}

		public void Dispose()
		{
			if (server.IsStarted)
				server.Stop();
		}

		[TestMethod]
		public void Can_connect_and_disconnect_to_running_server()
		{
			var client = new TcpClient();
			var address = new IPAddress(new byte[] { 127, 0, 0, 1 });
			Assert.IsFalse(client.IsConnected, "IsConnected should be false before connection");
			client.Connect(address, SERVER_PORT);
			Assert.IsTrue(client.IsConnected, "IsConnected should be true after connection.");
			client.Disconnect();
			Assert.IsFalse(client.IsConnected, "IsConnected should be false after disconnection.");
		}

		[TestMethod]
		[ExpectedException(typeof(SocketException))]
		public void Does_not_connect_to_closed_server()
		{
			server.Stop();
			var client = new TcpClient();
			var address = new IPAddress(new byte[] { 127, 0, 0, 1 });
			client.Connect(address, SERVER_PORT);
			client.Disconnect(); //Guard clause
		}
	}
}
