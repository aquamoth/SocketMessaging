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
		public void Server_triggers_event_when_client_connects()
		{
			Connection connectedClient = null;

			server.Connected += (s1, e1) => {
				connectedClient = e1.Connection;
			};

			Assert.IsNull(connectedClient, "Server should not publish connected client before connection.");
			client.Connect(serverAddress, SERVER_PORT);
			waitFor(() => connectedClient != null);
			Assert.IsNotNull(connectedClient, "Server should publish connected client after connection.");
		}

		[TestMethod]
		public void Connection_triggers_event_on_disconnection()
		{
			bool disconnectionEventTriggered = false;

			server.Connected += (s1, e1) => {
				e1.Connection.Disconnected += (s2, e2) => disconnectionEventTriggered = true;
			};

			client.Connect(serverAddress, SERVER_PORT);

			Assert.IsFalse(disconnectionEventTriggered, "Connection should not trigger disconnected event before client disconnects.");
			client.Disconnect();
			waitFor(() => disconnectionEventTriggered);
			Assert.IsTrue(disconnectionEventTriggered, "Connection should trigger disconnected event when client disconnects.");
		}

		[TestMethod]
		public void Can_send_packet_to_server()
		{
			client.Connect(serverAddress, SERVER_PORT);

			while (!server.Connections.Any())
				System.Threading.Thread.Sleep(10);
			var serverConnection = server.Connections.Single();

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
				if (serverConnection.Available > 0)
				{
					actualLength += serverConnection.Receive(buffer, actualLength, buffer.Length - actualLength, SocketFlags.None);
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
		//public void Server_can_send_packet_to_client()
		//{
		//	client.Connect(serverAddress, SERVER_PORT);
		//	waitFor(() => server.Connections.Any());

		//	var serverConnection = server.Connections.Single();

		//	var r = new Random();
		//	var buffer1 = new byte[100];
		//	var buffer2 = new byte[100];
		//	var expectedBuffer = buffer1.Concat(buffer2).ToArray();

		//	serverConnection.Send(buffer1);
		//	serverConnection.Send(buffer2);

			
		//	var buffer = new byte[expectedBuffer.Length + 1];
		//	var actualLength = 0;
		//	while (actualLength < expectedBuffer.Length)
		//	{
		//		waitFor(() => client.Available);
		//		Assert.IsTrue(client.Available, "Client should receive packet:");
		//		actualLength += client.Receive(buffer, actualLength, buffer.Length - actualLength, SocketFlags.None);
		//	}

		//	CollectionAssert.AreEqual(expectedBuffer, buffer.Take(actualLength).ToArray());
		//}

		[TestMethod]
		public void Connection_triggers_receivedRaw_events()
		{
			Connection serverConnection = null;
			int receiveEvents = 0;
			var buffer = new byte[1];

			server.Connected += (s, e) => {
				serverConnection = e.Connection;
				e.Connection.ReceivedRaw += (s2, e2) => receiveEvents++;
			};
			client.Connect(serverAddress, SERVER_PORT);

			Assert.AreEqual(0, receiveEvents, "Connection should not trigger receive raw event before client sends something.");

			client.Send(buffer);
			waitFor(() => receiveEvents != 0);
			Assert.AreEqual(1, receiveEvents, "Connection should trigger received raw event after first send.");

			client.Send(buffer);
			waitFor(() => receiveEvents != 1, 100);
			Assert.AreEqual(1, receiveEvents, "Connection should not trigger multiple received raw events between reads.");

			var receiveBuffer = new byte[100];
			serverConnection.Receive(receiveBuffer);
			waitFor(() => receiveEvents != 1, 100);
			Assert.AreEqual(1, receiveEvents, "Connection should not trigger received raw events just because buffer was read.");

			client.Send(buffer);
			waitFor(() => receiveEvents != 1);
			Assert.AreEqual(2, receiveEvents, "Connection should trigger new received raw event.");
		}

		[TestMethod]
		public void Can_read_raw_stream_from_connection()
		{
			Connection serverConnection = null;
			var connectionBuffer = new List<byte>();

			server.Connected += (s, e) => {
				serverConnection = e.Connection;
				e.Connection.ReceivedRaw += (s2, e2) =>
				{
					var receiveBuffer = new byte[e.Connection.Available];
					e.Connection.Receive(receiveBuffer);
					connectionBuffer.AddRange(receiveBuffer);
				};
			};
			client.Connect(serverAddress, SERVER_PORT);

			var buffer = new byte[500000];
			new Random().NextBytes(buffer);
			client.Send(buffer);
			waitFor(() => connectionBuffer.Count >= buffer.Length);

			CollectionAssert.AreEqual(buffer, connectionBuffer, "Connection should receive the same data the client sent.");
		}



		private static void waitFor(Func<bool> func, int timeout = 1000)
		{
			int timeoutCounter = timeout / 10;
			while (!func() && --timeoutCounter > 0)
				System.Threading.Thread.Sleep(10);
		}
	}
}
