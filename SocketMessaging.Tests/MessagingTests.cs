using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Net;

namespace SocketMessaging.Tests
{
	[TestClass]
	public class MessagingTests : IDisposable
	{
		const int DEFAULT_MAX_MESSAGE_SIZE = 65535;
		const int SERVER_PORT = 7783;
		readonly Server.TcpServer server;
		readonly TcpClient client = null;

		public MessagingTests()
		{
			server = new Server.TcpServer();
			server.Start(SERVER_PORT);

			var serverAddress = new IPAddress(new byte[] { 127, 0, 0, 1 });
			client = TcpClient.Connect(serverAddress, SERVER_PORT);
		}

		public void Dispose()
		{
			client.Close();
			server.Stop();
		}


		[TestMethod]
		[TestCategory("Connection: Messages")]
		public void Can_switch_messaging_mode()
		{
			Helpers.WaitFor(() => server.Connections.Any());
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
		[TestCategory("Connection: Messages")]
		[ExpectedException(typeof(InvalidOperationException))]
		public void Cant_receive_messages_when_in_raw_packet_mode()
		{
			var buffer = client.ReceiveMessage();
		}

		[TestMethod]
		[TestCategory("Connection: Messages")]
		public void Connection_returns_null_when_no_message_is_available()
		{
			byte[] buffer;

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
		[TestCategory("Connection: Messages")]
		public void Can_receive_fixed_length_messages()
		{
			var receivedMessageCounter = 0;

			Assert.AreEqual(DEFAULT_MAX_MESSAGE_SIZE, client.MaxMessageSize, "Connection should have a sane default max message size");
			client.MaxMessageSize = 10;
			client.Mode = MessageMode.FixedLength;
			client.ReceivedMessage += (s, e) => { receivedMessageCounter++; };

			Helpers.WaitFor(() => server.Connections.Any());
			var serverConnection = server.Connections.Single();

			var sentMessage = new byte[30];
			new Random().NextBytes(sentMessage);
			serverConnection.Send(sentMessage);

			Helpers.WaitFor(() => receivedMessageCounter >= 1);
			Assert.IsTrue(receivedMessageCounter >= 1, "Client should trigger one message received event");
			var receivedMessage = client.ReceiveMessage();
			CollectionAssert.AreEqual(sentMessage.Take(client.MaxMessageSize).ToArray(), receivedMessage, "First message wasn't correctly received");

			Helpers.WaitFor(() => receivedMessageCounter >= 2);
			Assert.IsTrue(receivedMessageCounter >= 2, "Client should trigger two message received event");
			receivedMessage = client.ReceiveMessage();
			CollectionAssert.AreEqual(sentMessage.Skip(client.MaxMessageSize).Take(client.MaxMessageSize).ToArray(), receivedMessage, "Second message wasn't correctly received");

			Helpers.WaitFor(() => receivedMessageCounter >= 3);
			Assert.IsTrue(receivedMessageCounter >= 3, "Client should trigger three message received event");
			receivedMessage = client.ReceiveMessage();
			CollectionAssert.AreEqual(sentMessage.Skip(2 * client.MaxMessageSize).Take(client.MaxMessageSize).ToArray(), receivedMessage, "Third message wasn't correctly received");
		}

		[TestMethod]
		[TestCategory("Connection: Messages")]
		public void Can_switch_delimiter()
		{
			Assert.AreEqual(0x0a, client.Delimiter);
			client.Delimiter = 0x20;
			Assert.AreEqual(0x20, client.Delimiter);
		}

		[TestMethod]
		[TestCategory("Connection: Messages")]
		public void Can_receive_delimited_messages()
		{
			var receivedMessageCounter = 0;
			Helpers.WaitFor(() => server.Connections.Any());
			var serverConnection = server.Connections.Single();

			client.Mode = MessageMode.DelimiterBound;
			client.ReceivedMessage += (s, e) => { receivedMessageCounter++; };

			var delimiter = new byte[] { 0x0a };
			var sentMessage1 = System.Text.Encoding.UTF8.GetBytes(Guid.NewGuid().ToString());
			serverConnection.Send(sentMessage1.Concat(delimiter).ToArray());

			var sentMessage2 = System.Text.Encoding.UTF8.GetBytes(Guid.NewGuid().ToString());
			serverConnection.Send(sentMessage2.Concat(delimiter).ToArray());

			var sentMessage3 = System.Text.Encoding.UTF8.GetBytes(Guid.NewGuid().ToString());
			serverConnection.Send(sentMessage3.Concat(delimiter).ToArray());

			Helpers.WaitFor(() => receivedMessageCounter >= 1);
			Assert.IsTrue(receivedMessageCounter >= 1, "Client should trigger one message received event");
			var receivedMessage = client.ReceiveMessage();
			CollectionAssert.AreEqual(sentMessage1, receivedMessage, "First message wasn't correctly received");

			Helpers.WaitFor(() => receivedMessageCounter >= 2);
			Assert.IsTrue(receivedMessageCounter >= 2, "Client should trigger two message received event");
			receivedMessage = client.ReceiveMessage();
			CollectionAssert.AreEqual(sentMessage2, receivedMessage, "Second message wasn't correctly received");

			Helpers.WaitFor(() => receivedMessageCounter >= 3);
			Assert.IsTrue(receivedMessageCounter >= 3, "Client should trigger three message received event");
			receivedMessage = client.ReceiveMessage();
			CollectionAssert.AreEqual(sentMessage3, receivedMessage, "Third message wasn't correctly received");
		}

		[TestMethod]
		[TestCategory("Connection: Messages")]
		public void Delimited_messages_cant_overflow_MaxMessageSize()
		{
			var receivedMessageCounter = 0;
			Helpers.WaitFor(() => server.Connections.Any());
			var serverConnection = server.Connections.Single();

			client.Mode = MessageMode.DelimiterBound;
			client.MaxMessageSize = 10;
			client.ReceivedMessage += (s, e) => { receivedMessageCounter++; };

			var delimiter = new byte[] { 0x0a };
			var sentMessage = System.Text.Encoding.UTF8.GetBytes(Guid.NewGuid().ToString());
			serverConnection.Send(sentMessage.Concat(delimiter).ToArray());

			Helpers.WaitFor(() => receivedMessageCounter >= 1, 100);
			//Assert.IsTrue(receivedMessageCounter == 0, "Client should not trigger a message received event");
			var receivedMessage = client.ReceiveMessage();
			Assert.IsNull(receivedMessage, "Client should not return message larger than MaxMessageSize");
		}

	}
}
