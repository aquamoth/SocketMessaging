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
		readonly Server.TcpServer server;
		TcpClient client = null;

		public TcpClientServerTests()
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
			waitFor(() => clientPollThread.ThreadState == System.Threading.ThreadState.Aborted);
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

		[TestMethod]
		[TestCategory("TcpServer")]
		public void Server_triggers_Connected()
		{
			Connection connectedClient = null;

			server.Connected += (s1, e1) => {
				connectedClient = e1.Connection;
			};

			Assert.IsNull(connectedClient, "Server should not publish connected client before connection.");
			connectClient();
			waitFor(() => connectedClient != null);
			Assert.IsNotNull(connectedClient, "Server should publish connected client after connection.");
		}

		[TestMethod]
		[TestCategory("Connection")]
		public void Connection_triggers_Disconnected()
		{
			var serverDisconnectedTriggered = false;
			var clientDisconnectedTriggered = false;

			server.Connected += (s1, e1) => {
				e1.Connection.Disconnected += (s2, e2) => serverDisconnectedTriggered = true;
			};

			connectClient();
			client.Disconnected += (s2, e2) => clientDisconnectedTriggered = true;

			Assert.IsFalse(serverDisconnectedTriggered, "Connection should not trigger disconnected event before client disconnects.");
			client.Close();
			waitFor(() => serverDisconnectedTriggered && clientDisconnectedTriggered);
			Assert.IsTrue(serverDisconnectedTriggered, "Server Connection should trigger disconnected event when client disconnects.");
			Assert.IsTrue(clientDisconnectedTriggered, "Client should trigger disconnected event when client disconnects.");
		}

		[TestMethod]
		[TestCategory("Connection")]
		public void Client_can_send_packet_to_server()
		{
			connectClient();
			waitFor(() => server.Connections.Any());
			var serverConnection = server.Connections.Single();

			var r = new Random();
			var buffer1 = new byte[100];
			r.NextBytes(buffer1);
			var buffer2 = new byte[100];
			r.NextBytes(buffer2);
			var expectedBuffer = buffer1.Concat(buffer2).ToArray();

			client.Send(buffer1);
			client.Send(buffer2);

			var buffer = new byte[expectedBuffer.Length + 1];
			var actualLength = 0;
			while (actualLength < expectedBuffer.Length)
			{
				waitFor(() => serverConnection.Available > 0);
				Assert.IsTrue(serverConnection.Available > 0, "Server should receive packet.");
				actualLength += serverConnection.Receive(buffer, actualLength, buffer.Length - actualLength, SocketFlags.None);
			}

			CollectionAssert.AreEqual(expectedBuffer, buffer.Take(actualLength).ToArray());
		}

		[TestMethod]
		[TestCategory("Connection")]
		public void Server_can_send_packet_to_client()
		{
			connectClient();
			waitFor(() => server.Connections.Any());
			var serverConnection = server.Connections.Single();

			var r = new Random();
			var buffer1 = new byte[100];
			r.NextBytes(buffer1);
			var buffer2 = new byte[100];
			r.NextBytes(buffer2);
			var expectedBuffer = buffer1.Concat(buffer2).ToArray();

			serverConnection.Send(buffer1);
			serverConnection.Send(buffer2);

			var buffer = new byte[expectedBuffer.Length + 1];
			var actualLength = 0;
			while (actualLength < expectedBuffer.Length)
			{
				waitFor(() => client.Available > 0);
				Assert.IsTrue(client.Available > 0, "Client should receive packet:");
				actualLength += client.Receive(buffer, actualLength, buffer.Length - actualLength, SocketFlags.None);
			}

			CollectionAssert.AreEqual(expectedBuffer, buffer.Take(actualLength).ToArray());
		}

		[TestMethod]
		[TestCategory("Connection")]
		public void Connection_triggers_receivedRaw_event()
		{
			Connection serverConnection = null;
			int receiveEvents = 0;
			var buffer = new byte[1];

			server.Connected += (s, e) => {
				serverConnection = e.Connection;
				e.Connection.ReceivedRaw += (s2, e2) => receiveEvents++;
			};
			connectClient();

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
		[TestCategory("Connection")]
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
			connectClient();

			var buffer = new byte[500000];
			new Random().NextBytes(buffer);
			client.Send(buffer);
			waitFor(() => connectionBuffer.Count >= buffer.Length);

			CollectionAssert.AreEqual(buffer, connectionBuffer, "Connection should receive the same data the client sent.");
		}




		private void connectClient()
		{
			var serverAddress = new IPAddress(new byte[] { 127, 0, 0, 1 });
			client = TcpClient.Connect(serverAddress, SERVER_PORT);
		}

		private static void waitFor(Func<bool> func, int timeout = 1000)
		{
			int timeoutCounter = timeout / 10;
			while (!func() && --timeoutCounter > 0)
				System.Threading.Thread.Sleep(10);
		}
	}
}
