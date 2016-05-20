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
		const int DEFAULT_MAX_MESSAGE_SIZE = 65535;
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

			var buffer = new byte[0].AsQueryable<byte>();
			var actualLength = 0;
			while (actualLength < expectedBuffer.Length)
			{
				waitFor(() => serverConnection.Available > 0);
				Assert.IsTrue(serverConnection.Available > 0, "Server should receive packet.");
				var data = serverConnection.Receive();
				buffer = buffer.Concat(data);
				actualLength += data.Length;
			}

			CollectionAssert.AreEqual(expectedBuffer, buffer.ToArray());
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

			var buffer = new byte[0].AsQueryable<byte>();
			var actualLength = 0;
			while (actualLength < expectedBuffer.Length)
			{
				waitFor(() => client.Available > 0);
				Assert.IsTrue(client.Available > 0, "Client should receive packets.");
				var data = client.Receive();
				buffer = buffer.Concat(data);
				actualLength += data.Length;
			}

			CollectionAssert.AreEqual(expectedBuffer, buffer.ToArray());
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
			Assert.AreEqual(2, receiveEvents, "Connection should trigger received raw after second send.");

			var receiveBuffer = serverConnection.Receive();
			waitFor(() => receiveEvents != 2, 100);
			Assert.AreEqual(2, receiveEvents, "Connection should not trigger received raw events just because buffer was read.");

			client.Send(buffer);
			waitFor(() => receiveEvents != 2);
			Assert.AreEqual(3, receiveEvents, "Connection should trigger received raw event after third send.");
		}

		[TestMethod]
		[TestCategory("Connection")]
		public void Can_read_raw_stream_from_connection()
		{
			Connection serverConnection = null;
			var connectionBuffer = new byte[0].AsQueryable();
			var connectionBufferLength = 0;

			server.Connected += (s, e) => {
				serverConnection = e.Connection;
				e.Connection.ReceivedRaw += (s2, e2) =>
				{
					var receiveBuffer = e.Connection.Receive();
					connectionBuffer = connectionBuffer.Concat(receiveBuffer);
					connectionBufferLength += receiveBuffer.Length;
				};
			};
			connectClient();

			var buffer = new byte[500000];
			new Random().NextBytes(buffer);
			client.Send(buffer);
			waitFor(() => connectionBufferLength >= buffer.Length);

			CollectionAssert.AreEqual(buffer, connectionBuffer.ToArray(), "Connection should receive the same data the client sent.");
		}

		[TestMethod]
		[TestCategory("Connection")]
		public void Can_switch_messaging_mode()
		{
			connectClient();
			waitFor(() => server.Connections.Any());
			Assert.AreEqual(MessageMode.Raw, client.Mode, "Client starts in raw message mode");
			Assert.AreEqual(MessageMode.Raw, server.Connections.Single().Mode, "Server starts in raw message mode");
			client.Mode = MessageMode.DelimiterBound;
			Assert.AreEqual(MessageMode.DelimiterBound, client.Mode, "Client mode should be DelimiterBound");
			client.Mode = MessageMode.PrefixedLength;
			Assert.AreEqual(MessageMode.PrefixedLength, client.Mode, "Client mode should be PrefixedLength");
			client.Mode = MessageMode.FixedLength;
			Assert.AreEqual(MessageMode.FixedLength, client.Mode, "Client mode should be FixedLength");

			client.MaxMessageSize = 10;
			Assert.AreEqual(10, client.MaxMessageSize, "Max message size should be read/write");
		}

		[TestMethod]
		[TestCategory("Connection")]
		[ExpectedException(typeof(InvalidOperationException))]
		public void Cant_receive_messages_when_in_raw_packet_mode()
		{
			connectClient();
			var buffer = client.ReceiveMessage();
		}

		[TestMethod]
		[TestCategory("Connection")]
		public void Connection_returns_null_when_no_message_is_available()
		{
			byte[] buffer;
			connectClient();

			client.Mode = MessageMode.DelimiterBound;
			buffer = client.ReceiveMessage();
			Assert.IsNull(buffer, "Connection should return null when there is no delimited message");

			client.Mode = MessageMode.FixedLength;
			buffer = client.ReceiveMessage();
			Assert.IsNull(buffer, "Connection should return null when there is no fixed length message");

			client.Mode = MessageMode.PrefixedLength;
			buffer = client.ReceiveMessage();
			Assert.IsNull(buffer, "Connection should return null when there is no prefixed length message");
		}

		[TestMethod]
		[TestCategory("Connection")]
		public void Can_receive_fixed_length_messages()
		{
			var receivedMessageCounter = 0;

			connectClient();
			Assert.AreEqual(DEFAULT_MAX_MESSAGE_SIZE, client.MaxMessageSize, "Connection should have a sane default max message size");
			client.MaxMessageSize = 10;
			client.Mode = MessageMode.FixedLength;
			client.ReceivedMessage += (s, e) => { receivedMessageCounter++; };

			waitFor(() => server.Connections.Any());
			var serverConnection = server.Connections.Single();

			var sentMessage = new byte[30];
			new Random().NextBytes(sentMessage);
			serverConnection.Send(sentMessage);

			waitFor(() => receivedMessageCounter >= 1);
			Assert.IsTrue(receivedMessageCounter >= 1, "Client should trigger one message received event");
			var receivedMessage = client.ReceiveMessage();
			CollectionAssert.AreEqual(sentMessage.Take(client.MaxMessageSize).ToArray(), receivedMessage, "First message wasn't correctly received");

			waitFor(() => receivedMessageCounter >= 2);
			Assert.IsTrue(receivedMessageCounter >= 2, "Client should trigger two message received event");
			receivedMessage = client.ReceiveMessage();
			CollectionAssert.AreEqual(sentMessage.Skip(client.MaxMessageSize).Take(client.MaxMessageSize).ToArray(), receivedMessage, "Second message wasn't correctly received");

			waitFor(() => receivedMessageCounter >= 3);
			Assert.IsTrue(receivedMessageCounter >= 3, "Client should trigger three message received event");
			receivedMessage = client.ReceiveMessage();
			CollectionAssert.AreEqual(sentMessage.Skip(2 * client.MaxMessageSize).Take(client.MaxMessageSize).ToArray(), receivedMessage, "Third message wasn't correctly received");
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
