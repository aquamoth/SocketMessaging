using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Diagnostics;

namespace SocketMessaging.Tests
{
	[TestClass]
	public class TcpClientServerTests : IDisposable
	{
		const int SERVER_PORT = 7783;
		readonly IPAddress serverAddress;
		readonly TcpServer server;
		readonly TcpClient client;

		public TcpClientServerTests()
		{
			server = new TcpServer();
			server.Start(SERVER_PORT);
			serverAddress = new IPAddress(new byte[] { 127, 0, 0, 1 });

			client = new TcpClient();
		}

		public void Dispose()
		{
			if (client.IsConnected)
				client.Disconnect();

			if (server.IsStarted)
				server.Stop();
		}

		[TestMethod]
		public void Can_connect_and_disconnect_to_running_server()
		{
			Assert.IsFalse(client.IsConnected, "IsConnected should be false before connection");
			client.Connect(serverAddress, SERVER_PORT);
			Assert.IsTrue(client.IsConnected, "IsConnected should be true after connection.");
			client.Disconnect();
			Assert.IsFalse(client.IsConnected, "IsConnected should be false after disconnection.");
		}

		[TestMethod]
		[ExpectedException(typeof(SocketException))]
		public void Does_not_connect_to_closed_server()
		{
			server.Stop();
			client.Connect(serverAddress, SERVER_PORT);
		}

		[TestMethod]
		public void Server_announces_connections_and_disconnections()
		{
			System.Net.Sockets.TcpClient connectedClient = null;
			System.Net.Sockets.TcpClient disconnectedClient = null;

			server.ClientConnected += (sender, e) => { connectedClient = e.Client; };
			client.Connect(serverAddress, SERVER_PORT);

			var timeoutCounter = 100;
			while (connectedClient == null && --timeoutCounter > 0)
				System.Threading.Thread.Sleep(10);
			Assert.IsNotNull(connectedClient, "Server should publish connected clients.");

			server.ClientDisconnected += (sender, e) => { disconnectedClient = e.Client; };
			client.Disconnect();

			timeoutCounter = 100;
			while (disconnectedClient == null && --timeoutCounter > 0)
				System.Threading.Thread.Sleep(10);
			Assert.IsNotNull(disconnectedClient, "Server should publish disconnected clients.");

		}

		[TestMethod]
		public void Can_send_packet_to_server()
		{
			client.Connect(serverAddress, SERVER_PORT);

			while (!server.Clients.Any())
				System.Threading.Thread.Sleep(10);
			var serverClient = server.Clients.Single();

			var sendString1 = Guid.NewGuid().ToString();
			var sendString2 = Guid.NewGuid().ToString();
			var expectedString = sendString1 + sendString2;

			client.Send(Encoding.UTF8.GetBytes(sendString1));
			client.Send(Encoding.UTF8.GetBytes(sendString2));

			var timeoutCounter = 100;
			var buffer = new byte[expectedString.Length + 100];
			var actualLength = 0;
			while (actualLength < expectedString.Length)
			{
				if (serverClient.Client.Available > 0)
				{
					actualLength += serverClient.Client.Receive(buffer, actualLength, buffer.Length - actualLength, SocketFlags.None);
				}

				System.Threading.Thread.Sleep(10);
				if (--timeoutCounter == 0)
					throw new ApplicationException("Server did not receive message in time");
			}

			Trace.TraceInformation("Comparing sent with received");
			var receivedString = Encoding.UTF8.GetString(buffer, 0, actualLength);
			Assert.AreEqual(expectedString, receivedString);
		}



	}
}
