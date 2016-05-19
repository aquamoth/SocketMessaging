using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Diagnostics;
using SocketMessaging.Server;

namespace SocketMessaging.Tests
{
	[TestClass]
	public class TcpClientServerTests : IDisposable
	{
		const int SERVER_PORT = 7783;
		readonly IPAddress serverAddress;
		readonly Server.TcpServer server;
		readonly TcpClient client;

		public TcpClientServerTests()
		{
			server = new Server.TcpServer();
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
			Connection connectedClient = null;
			server.ClientConnected += (sender, e) => { connectedClient = e.Client; };

			Connection disconnectedClient = null;
			server.ClientDisconnected += (sender, e) => { disconnectedClient = e.Client; };

			Assert.IsNull(connectedClient, "Server should not publish connected client before connection.");
			client.Connect(serverAddress, SERVER_PORT);
			waitFor(() => connectedClient != null);
			Assert.IsNotNull(connectedClient, "Server should publish connected client after connection.");

			Assert.IsNull(disconnectedClient, "Server should not publish disconnected client before disconnection.");
			client.Disconnect();
			waitFor(() => disconnectedClient != null);
			Assert.IsNotNull(disconnectedClient, "Server should publish disconnected client after disconnection.");
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
				if (serverClient.Available > 0)
				{
					actualLength += serverClient.Receive(buffer, actualLength, buffer.Length - actualLength, SocketFlags.None);
				}

				System.Threading.Thread.Sleep(10);
				if (--timeoutCounter == 0)
					throw new ApplicationException("Server did not receive message in time");
			}

			Trace.TraceInformation("Comparing sent with received");
			var receivedString = Encoding.UTF8.GetString(buffer, 0, actualLength);
			Assert.AreEqual(expectedString, receivedString);
		}
		
		//[TestMethod]
		//public void Server_publishes_raw_packages()
		//{
		//	System.Net.Sockets.TcpClient receivingClient = null;
		//	server.ClientReceivedRaw += (sender, e) => { receivingClient = e.Client; };

		//	client.Connect(serverAddress, SERVER_PORT);

		//	var buffer = new byte[1024];
		//	new Random().NextBytes(buffer);
		//	client.Send(buffer);
		//	waitFor(() => receivingClient != null);
		//	Assert.IsNotNull(receivingClient, "Server should publish client received raw data since last read");

		//	var firstReceiveClient = receivingClient;
		//	receivingClient = null;
		//	var buffer2 = new byte[1024];
		//	new Random().NextBytes(buffer2);
		//	client.Send(buffer2);
		//	waitFor(() => receivingClient != null, 100);
		//	Assert.IsNull(receivingClient, "Server should not publish client received raw data again until previous data was read");

		//	var receiveBuffer = new byte[65536];
		//	firstReceiveClient.Client.Receive(receiveBuffer);

		//	var buffer3 = new byte[1024];
		//	new Random().NextBytes(buffer3);
		//	client.Send(buffer3);
		//	waitFor(() => receivingClient != null);
		//	Assert.IsNotNull(receivingClient, "Server should publish client received raw data since last read");
		//}

		private static void waitFor(Func<bool> func, int timeout = 1000)
		{
			int timeoutCounter = timeout / 10;
			while (!func() && --timeoutCounter > 0)
				System.Threading.Thread.Sleep(10);
		}
	}
}
