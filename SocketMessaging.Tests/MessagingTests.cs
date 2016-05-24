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
			client.SetMode(MessageMode.DelimiterBound);
			Assert.AreEqual(MessageMode.DelimiterBound, client.Mode, "Client mode should be DelimiterBound");
			client.SetMode(MessageMode.PrefixedLength);
			Assert.AreEqual(MessageMode.PrefixedLength, client.Mode, "Client mode should be PrefixedLength");
			client.SetMode(MessageMode.FixedLength);
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

			client.SetMode(MessageMode.DelimiterBound);
			buffer = client.ReceiveMessage();
			Assert.IsNull(buffer, "Connection should return null when there is no delimited message");

			client.SetMode(MessageMode.FixedLength);
			buffer = client.ReceiveMessage();
			Assert.IsNull(buffer, "Connection should return null when there is no fixed length message");

			client.SetMode(MessageMode.PrefixedLength);
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
			client.SetMode(MessageMode.FixedLength);
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
		public void Changing_delimiter_retriggers_received_messages()
		{
			var receivedMessageCounter = 0;
			client.ReceivedMessage += (s, e) => { receivedMessageCounter++; };
			client.SetMode(MessageMode.DelimiterBound);
			CollectionAssert.AreEqual(new byte[] { 0x0a }, client.Delimiter);

			Helpers.WaitFor(() => server.Connections.Any());
			var serverConnection = server.Connections.Single();
			serverConnection.SetDelimiter(new byte[] { 0x20 });
			serverConnection.SetMode(MessageMode.DelimiterBound);

			var sentMessage = Guid.NewGuid().ToString();
			serverConnection.Send(sentMessage);

			Helpers.WaitFor(() => client.Available > 0);
			receivedMessageCounter = 0;
			client.SetDelimiter(serverConnection.Delimiter);
			CollectionAssert.AreEqual(new byte[] { 0x20 }, client.Delimiter);

			Helpers.WaitFor(() => receivedMessageCounter >= 1);
			Assert.AreEqual(1, receivedMessageCounter, "Client should trigger one message received event");
			var receivedMessage = client.ReceiveMessageString();
			Assert.AreEqual(sentMessage, receivedMessage, "First message wasn't correctly received");
		}

		[TestMethod]
		[TestCategory("Connection: Messages")]
		public void Can_receive_delimited_messages()
		{
			var receivedMessageCounter = 0;
			Helpers.WaitFor(() => server.Connections.Any());
			var serverConnection = server.Connections.Single();

			client.SetMode(MessageMode.DelimiterBound);
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
		[ExpectedException(typeof(NotSupportedException))]
		public void Cant_send_delimited_message_when_delimiter_is_empty()
		{
			Helpers.WaitFor(() => server.Connections.Any());
			var serverConnection = server.Connections.Single();
			serverConnection.SetDelimiter(new byte[0]);
			serverConnection.SetMode(MessageMode.DelimiterBound);
			serverConnection.Send("A");
		}

		[TestMethod]
		[TestCategory("Connection: Messages")]
		[ExpectedException(typeof(NotSupportedException))]
		public void Cant_receive_delimited_message_when_delimiter_is_empty()
		{
			Helpers.WaitFor(() => server.Connections.Any());
			var serverConnection = server.Connections.Single();
			serverConnection.SetMode(MessageMode.DelimiterBound);
			serverConnection.Send("A");

			client.SetDelimiter(new byte[0]);
			client.SetMode(MessageMode.DelimiterBound);
			var message = client.ReceiveMessage();
		}

		[TestMethod]
		[TestCategory("Connection: Messages")]
		[ExpectedException(typeof(NotSupportedException))]
		public void Cant_send_message_when_escapecode_is_part_of_delimiter()
		{
			Helpers.WaitFor(() => server.Connections.Any());
			var serverConnection = server.Connections.Single();
			serverConnection.SetDelimiter(new byte[] { 0x0a, 0x0d, 0x0a });
			serverConnection.Escapecode = 0x0d;
			serverConnection.SetMode(MessageMode.DelimiterBound);
			serverConnection.Send("A");
		}

		[TestMethod]
		[TestCategory("Connection: Messages")]
		[ExpectedException(typeof(NotSupportedException))]
		public void Cant_receive_message_when_escapecode_is_part_of_delimiter()
		{
			Helpers.WaitFor(() => server.Connections.Any());
			var serverConnection = server.Connections.Single();
			serverConnection.SetDelimiter(new byte[] { 0x0a, 0x0d, 0x0a });
			serverConnection.SetMode(MessageMode.DelimiterBound);
			serverConnection.Send("A");

			client.SetDelimiter(serverConnection.Delimiter);
			client.Escapecode = 0x0d;
			client.SetMode(MessageMode.DelimiterBound);
			var message = client.ReceiveMessage();
		}

		[TestMethod]
		[TestCategory("Connection: Messages")]
		[ExpectedException(typeof(InvalidOperationException))]
		public void Delimited_messages_cant_overflow_MaxMessageSize()
		{
			var receivedMessageCounter = 0;
			Helpers.WaitFor(() => server.Connections.Any());
			var serverConnection = server.Connections.Single();

			client.SetMode(MessageMode.DelimiterBound);
			client.MaxMessageSize = 10;
			client.ReceivedMessage += (s, e) => { receivedMessageCounter++; };

			var sentBuffer = new byte[client.MaxMessageSize + 1];
			serverConnection.Send(sentBuffer.Concat(client.Delimiter).ToArray());

			Helpers.WaitFor(() => receivedMessageCounter >= 1, 100);
			//Assert.IsTrue(receivedMessageCounter == 0, "Client should not trigger a message received event");
			var receivedMessage = client.ReceiveMessage();
		}

		[TestMethod]
		[TestCategory("Connection: Messages")]
		public void Can_receive_length_prefixed_messages()
		{
			var receivedMessageCounter = 0;
			Helpers.WaitFor(() => server.Connections.Any());
			var serverConnection = server.Connections.Single();

			client.SetMode(MessageMode.PrefixedLength);
			client.ReceivedMessage += (s, e) => { receivedMessageCounter++; };

			var sentMessage1 = System.Text.Encoding.UTF8.GetBytes(Guid.NewGuid().ToString());
			var messageSize1 = BitConverter.GetBytes(sentMessage1.Length);
			serverConnection.Send(messageSize1.Concat(sentMessage1).ToArray());

			var sentMessage2 = System.Text.Encoding.UTF8.GetBytes(Guid.NewGuid().ToString() + Guid.NewGuid().ToString());
			var messageSize2 = BitConverter.GetBytes(sentMessage2.Length);
			serverConnection.Send(messageSize2.Concat(sentMessage2).ToArray());

			Helpers.WaitFor(() => receivedMessageCounter >= 1);
			Assert.IsTrue(receivedMessageCounter >= 1, "Client should trigger one message received event");
			var receivedMessage = client.ReceiveMessage();
			CollectionAssert.AreEqual(sentMessage1, receivedMessage, "First message wasn't correctly received");

			Helpers.WaitFor(() => receivedMessageCounter >= 2);
			Assert.IsTrue(receivedMessageCounter >= 2, "Client should trigger two message received event");
			receivedMessage = client.ReceiveMessage();
			CollectionAssert.AreEqual(sentMessage2, receivedMessage, "Second message wasn't correctly received");
		}

		[TestMethod]
		[TestCategory("Connection: Messages")]
		public void Can_receive_message_string_with_escaped_delimiters()
		{
			var customEncoding = System.Text.Encoding.GetEncoding("ISO-8859-1");
			var sentMessage = @"Mitt namn är Mattias Åslund. För övrigt är jag programmerare.";
			var sentBuffer = customEncoding.GetBytes(sentMessage);

			var receivedMessageCounter = 0;
			Helpers.WaitFor(() => server.Connections.Any());
			var serverConnection = server.Connections.Single();

			client.MaxMessageSize = sentBuffer.Length;
			client.MessageEncoding = customEncoding;
			client.SetMode(MessageMode.FixedLength);
			client.ReceivedMessage += (s, e) => { receivedMessageCounter++; };

			serverConnection.Send(sentBuffer);

			Helpers.WaitFor(() => receivedMessageCounter >= 1);
			var receivedMessage = client.ReceiveMessageString();
			Assert.AreEqual(sentMessage, receivedMessage, "Message wasn't correctly received");
		}

		[TestMethod]
		[TestCategory("Connection: Messages")]
		public void Can_send_delimited_messages()
		{
			var receivedMessageCounter = 0;
			Helpers.WaitFor(() => server.Connections.Any());
			var serverConnection = server.Connections.Single();
			serverConnection.SetMode(MessageMode.DelimiterBound);
			client.SetMode(MessageMode.DelimiterBound);
			client.ReceivedMessage += (s, e) => { receivedMessageCounter++; };

			var sentMessage1 = System.Text.Encoding.UTF8.GetBytes(Guid.NewGuid().ToString());
			serverConnection.Send(sentMessage1);

			var sentMessage2 = System.Text.Encoding.UTF8.GetBytes(Guid.NewGuid().ToString() + Guid.NewGuid().ToString());
			serverConnection.Send(sentMessage2);

			Helpers.WaitFor(() => receivedMessageCounter >= 1);
			var receivedMessage = client.ReceiveMessage();
			CollectionAssert.AreEqual(sentMessage1, receivedMessage, "First message wasn't correctly received");

			Helpers.WaitFor(() => receivedMessageCounter >= 2);
			receivedMessage = client.ReceiveMessage();
			CollectionAssert.AreEqual(sentMessage2, receivedMessage, "Second message wasn't correctly received");
		}

		[TestMethod]
		[TestCategory("Connection: Messages")]
		public void Can_send_messages_prefixed_with_length()
		{
			var receivedMessageCounter = 0;
			Helpers.WaitFor(() => server.Connections.Any());
			var serverConnection = server.Connections.Single();
			serverConnection.SetMode(MessageMode.PrefixedLength);
			client.SetMode(MessageMode.PrefixedLength);
			client.ReceivedMessage += (s, e) => { receivedMessageCounter++; };

			var sentMessage1 = System.Text.Encoding.UTF8.GetBytes(Guid.NewGuid().ToString());
			serverConnection.Send(sentMessage1);

			var sentMessage2 = System.Text.Encoding.UTF8.GetBytes(Guid.NewGuid().ToString() + Guid.NewGuid().ToString());
			serverConnection.Send(sentMessage2);

			Helpers.WaitFor(() => receivedMessageCounter >= 1);
			var receivedMessage = client.ReceiveMessage();
			CollectionAssert.AreEqual(sentMessage1, receivedMessage, "First message wasn't correctly received");

			Helpers.WaitFor(() => receivedMessageCounter >= 2);
			receivedMessage = client.ReceiveMessage();
			CollectionAssert.AreEqual(sentMessage2, receivedMessage, "Second message wasn't correctly received");
		}

		[TestMethod]
		[TestCategory("Connection: Messages")]
		public void Can_send_fixed_sized_messages()
		{
			var receivedMessageCounter = 0;
			Helpers.WaitFor(() => server.Connections.Any());
			var serverConnection = server.Connections.Single();
			serverConnection.MaxMessageSize = 36;
			serverConnection.SetMode(MessageMode.FixedLength);
			client.MaxMessageSize = 36;
			client.SetMode(MessageMode.FixedLength);
			client.ReceivedMessage += (s, e) => { receivedMessageCounter++; };

			var sentMessage1 = System.Text.Encoding.UTF8.GetBytes(Guid.NewGuid().ToString());
			serverConnection.Send(sentMessage1);

			var sentMessage2 = System.Text.Encoding.UTF8.GetBytes(Guid.NewGuid().ToString());
			serverConnection.Send(sentMessage2);

			Helpers.WaitFor(() => receivedMessageCounter >= 1);
			var receivedMessage = client.ReceiveMessage();
			CollectionAssert.AreEqual(sentMessage1, receivedMessage, "First message wasn't correctly received");

			Helpers.WaitFor(() => receivedMessageCounter >= 2);
			receivedMessage = client.ReceiveMessage();
			CollectionAssert.AreEqual(sentMessage2, receivedMessage, "Second message wasn't correctly received");
		}

		[TestMethod]
		[ExpectedException(typeof(ArgumentException))]
		[TestCategory("Connection: Messages")]
		public void Cant_send_fixed_sized_message_of_wrong_length()
		{
			Helpers.WaitFor(() => server.Connections.Any());
			var serverConnection = server.Connections.Single();
			serverConnection.MaxMessageSize = 30;
			serverConnection.SetMode(MessageMode.FixedLength);

			var message = System.Text.Encoding.UTF8.GetBytes(Guid.NewGuid().ToString());
			serverConnection.Send(message);
		}

		[TestMethod]
		public void Can_send_message_string()
		{
			var receivedMessageCounter = 0;
			Helpers.WaitFor(() => server.Connections.Any());
			var serverConnection = server.Connections.Single();
			serverConnection.SetDelimiter(new byte[] { 0x00 });
			serverConnection.SetMode(MessageMode.DelimiterBound);
			client.SetDelimiter(new byte[] { 0x00 });
			client.SetMode(MessageMode.DelimiterBound);
			client.ReceivedMessage += (s, e) => { receivedMessageCounter++; };

			var sentMessage = @"¥£€$¢₡₢₣₤₥₦₧₨₩₪₫₭₮₯₹
ᚠᛇᚻ᛫ᛒᛦᚦ᛫ᚠᚱᚩᚠᚢᚱ᛫ᚠᛁᚱᚪ᛫ᚷᛖᚻᚹᛦᛚᚳᚢᛗ
ᛋᚳᛖᚪᛚ᛫ᚦᛖᚪᚻ᛫ᛗᚪᚾᚾᚪ᛫ᚷᛖᚻᚹᛦᛚᚳ᛫ᛗᛁᚳᛚᚢᚾ᛫ᚻᛦᛏ᛫ᛞᚫᛚᚪᚾ
ᚷᛁᚠ᛫ᚻᛖ᛫ᚹᛁᛚᛖ᛫ᚠᚩᚱ᛫ᛞᚱᛁᚻᛏᚾᛖ᛫ᛞᚩᛗᛖᛋ᛫ᚻᛚᛇᛏᚪᚾ᛬

An preost wes on leoden, Laȝamon was ihoten
He wes Leovenaðes sone -- liðe him be Drihten.
He wonede at Ernleȝe at æðelen are chirechen,
Uppen Sevarne staþe, sel þar him þuhte,
Onfest Radestone, þer he bock radde.
";
			serverConnection.Send(sentMessage);

			Helpers.WaitFor(() => receivedMessageCounter >= 1);
			var receivedMessage = System.Text.Encoding.UTF8.GetString(client.ReceiveMessage());
			Assert.AreEqual(sentMessage, receivedMessage, "Message wasn't correctly received");
		}

		[TestMethod]
		public void Can_send_message_string_in_custom_encoding()
		{
			var receivedMessageCounter = 0;
			Helpers.WaitFor(() => server.Connections.Any());
			var serverConnection = server.Connections.Single();
			serverConnection.SetDelimiter(new byte[] { 0x00 });
			serverConnection.SetMode(MessageMode.DelimiterBound);
			serverConnection.MessageEncoding = System.Text.Encoding.GetEncoding("ISO-8859-1");
			client.SetDelimiter(new byte[] { 0x00 });
			client.SetMode(MessageMode.DelimiterBound);
			client.ReceivedMessage += (s, e) => { receivedMessageCounter++; };

			var sentMessage = @"Mitt namn är Mattias Åslund. För övrigt är jag programmerare.";
			serverConnection.Send(sentMessage);

			Helpers.WaitFor(() => receivedMessageCounter >= 1);
			var receivedMessage = System.Text.Encoding.GetEncoding("ISO-8859-1").GetString(client.ReceiveMessage());
			Assert.AreEqual(sentMessage, receivedMessage, "Message wasn't correctly received");
		}

		[TestMethod]
		public void Sends_messages_with_escaped_delimiters()
		{
			var receivedMessageCounter = 0;
			Helpers.WaitFor(() => server.Connections.Any());
			var serverConnection = server.Connections.Single();
			client.ReceivedMessage += (s, e) => { receivedMessageCounter++; };
			serverConnection.SetMode(MessageMode.DelimiterBound);
			client.SetMode(MessageMode.DelimiterBound);

			var sentMessage1 = @"Message 1! part 1|part 2";
			var messageDelimiter1 = client.MessageEncoding.GetBytes("|");
			var escapeCode1 = client.MessageEncoding.GetBytes("!").Single();
			serverConnection.Escapecode = escapeCode1;
			serverConnection.SetDelimiter(messageDelimiter1);
			client.Escapecode = escapeCode1;
			client.SetDelimiter(messageDelimiter1);
			serverConnection.Send(sentMessage1);
			Helpers.WaitFor(() => receivedMessageCounter >= 1);
			var receivedMessage1 = client.ReceiveMessageString();
			Assert.AreEqual(sentMessage1, receivedMessage1, "First message wasn't correctly received");



			receivedMessageCounter = 0;
			var sentMessage2 = @"Message 2! part 1|part 2";
			var messageDelimiter2 = client.MessageEncoding.GetBytes("M");
			var escapeCode2 = client.MessageEncoding.GetBytes("1").Single();
			serverConnection.Escapecode = escapeCode2;
			serverConnection.SetDelimiter(messageDelimiter2);
			client.Escapecode = escapeCode2;
			client.SetDelimiter(messageDelimiter2);
			serverConnection.Send(sentMessage2);
			Helpers.WaitFor(() => receivedMessageCounter >= 1);
			var receivedMessage2 = client.ReceiveMessageString();
			Assert.AreEqual(sentMessage2, receivedMessage2, "Second message wasn't correctly received");
		}

		[TestMethod]
		public void Writing_raw_to_client_that_still_expects_PrefixedLength()
		{
			Helpers.WaitFor(() => server.Connections.Any());
			var serverConnection = server.Connections.Single();
			serverConnection.Send(BitConverter.GetBytes(-100));
			serverConnection.Send(new byte[] { 75 });
			Helpers.WaitFor(() => client.Available >= 4);

			//This should not throw exception when checking existing buffer for valid messages
			client.SetMode(MessageMode.PrefixedLength);
		}

		[TestMethod]
		public void Can_send_and_receive_a_mix_of_message_types()
		{
			var rnd = new Random();

			var receivedMessageCounter = 0;
			client.ReceivedMessage += (s, e) => { receivedMessageCounter++; };

			Helpers.WaitFor(() => server.Connections.Any());
			var serverConnection = server.Connections.Single();

			//Send a raw chunk
			var sentBuffer1 = new byte[20];
			rnd.NextBytes(sentBuffer1);
			serverConnection.Send(sentBuffer1);

			//Send a fixes length chunk
			var sentBuffer2 = new byte[50];
			rnd.NextBytes(sentBuffer2);
			serverConnection.MaxMessageSize = sentBuffer2.Length;
			serverConnection.SetMode(MessageMode.FixedLength);
			serverConnection.Send(sentBuffer2);

			//Send a delimited message
			var sentMessage3 = "Jag är programmerare.\nSå det så!\nOch rad tre";
			serverConnection.SetMode(MessageMode.DelimiterBound);
			serverConnection.Send(sentMessage3);

			//Send a prefix-length message
			var sentMessage4 = "A prefix-length delimited message\nWith two rows.";
			serverConnection.SetMode(MessageMode.PrefixedLength);
			serverConnection.Send(sentMessage4);

			//Finish off with yet another raw chunk
			var sentBuffer5 = new byte[10];
			rnd.NextBytes(sentBuffer5);
			serverConnection.SetMode(MessageMode.Raw);
			serverConnection.Send(sentBuffer5);


			//Receive a raw chunk
			Helpers.WaitFor(() => client.Available >= sentBuffer1.Length);
			var receivedBuffer1 = client.Receive(sentBuffer1.Length);
			CollectionAssert.AreEqual(sentBuffer1, receivedBuffer1, "First chunk differs.");

			//Receive a fixes length chunk
			receivedMessageCounter = 0;
			client.MaxMessageSize = sentBuffer2.Length;
			client.SetMode(MessageMode.FixedLength);
			Helpers.WaitFor(() => receivedMessageCounter >= 1);
			Assert.IsTrue(receivedMessageCounter >= 1, "Changing Mode should retrigger receive-events when receive-queue is not empty");
			var receivedBuffer2 = client.ReceiveMessage();
			client.MaxMessageSize = 65535;
			CollectionAssert.AreEqual(sentBuffer2, receivedBuffer2, "Second chunk differs.");

			//Receive a delimited message
			receivedMessageCounter = 0;
			client.SetMode(MessageMode.DelimiterBound);
			Helpers.WaitFor(() => receivedMessageCounter >= 1);
			Assert.IsTrue(receivedMessageCounter >= 1, "Changing Mode should retrigger receive-events when receive-queue is not empty");
			var receivedMessage3 = client.ReceiveMessageString();
			Assert.AreEqual(sentMessage3, receivedMessage3, "Third message differs.");

			//Receive a prefix-length message
			receivedMessageCounter = 0;
			client.SetMode(MessageMode.PrefixedLength);
			Helpers.WaitFor(() => receivedMessageCounter >= 1);
			Assert.IsTrue(receivedMessageCounter >= 1, "Changing Mode should retrigger receive-events when receive-queue is not empty");
			var receivedMessage4 = client.ReceiveMessageString();
			Assert.AreEqual(sentMessage4, receivedMessage4, "Fourth message differs.");

			//Finish off receiving yet another raw chunk
			client.SetMode(MessageMode.Raw);
			Helpers.WaitFor(() => client.Available >= sentBuffer5.Length);
			var receivedBuffer5 = client.Receive(sentBuffer5.Length);
			CollectionAssert.AreEqual(sentBuffer5, receivedBuffer5, "Last raw chunk differs.");

			Assert.AreEqual(0, client.Available, "The receive queue should be empty after last raw chunk is read.");
		}

		[TestMethod]
		[ExpectedException(typeof(InvalidOperationException))]
		[TestCategory("Connection: Messages")]
		public void Receiving_delimited_message_larger_than_MaxMessageSize_throws_exception()
		{
			var message = "This is a long message.";

			Helpers.WaitFor(() => server.Connections.Any());
			var serverConnection = server.Connections.Single();
			serverConnection.SetMode(MessageMode.DelimiterBound);
			serverConnection.Send(message);

			client.MaxMessageSize = 10;
			client.SetMode(MessageMode.DelimiterBound);
			Helpers.WaitFor(() => client.Available >= message.Length);
			var receivedMessage = client.ReceiveMessageString();
		}

		[TestMethod]
		[ExpectedException(typeof(InvalidOperationException))]
		[TestCategory("Connection: Messages")]
		public void Receiving_length_prefixed_message_larger_than_MaxMessageSize_throws_exception()
		{
			var message = "This is a long message.";

			Helpers.WaitFor(() => server.Connections.Any());
			var serverConnection = server.Connections.Single();
			serverConnection.SetMode(MessageMode.PrefixedLength);
			serverConnection.Send(message);

			client.MaxMessageSize = 10;
			client.SetMode(MessageMode.PrefixedLength);
			Helpers.WaitFor(() => client.Available >= message.Length);
			var receivedMessage = client.ReceiveMessageString();
		}

		[TestMethod]
		public void Can_receive_multi_byte_delimited_messages()
		{
			var receivedMessageCounter = 0;
			Helpers.WaitFor(() => server.Connections.Any());
			var serverConnection = server.Connections.Single();
			serverConnection.SetDelimiter(serverConnection.MessageEncoding.GetBytes("ᚠ"));
			serverConnection.SetMode(MessageMode.DelimiterBound);
			client.SetDelimiter(serverConnection.Delimiter);
			client.SetMode(MessageMode.DelimiterBound);
			client.ReceivedMessage += (s, e) => { receivedMessageCounter++; };

			var sentMessage1 = @"Text row 1";
			serverConnection.Send(sentMessage1);

			var sentMessage2 = "Text row 2";
			serverConnection.Send(sentMessage2);

			Helpers.WaitFor(() => receivedMessageCounter >= 1);
			var receivedMessage1 = client.ReceiveMessageString();
			Assert.AreEqual(sentMessage1, receivedMessage1, "Message wasn't correctly received");

			Helpers.WaitFor(() => receivedMessageCounter >= 2);
			var receivedMessage2 = client.ReceiveMessageString();
			Assert.AreEqual(sentMessage2, receivedMessage2, "Message wasn't correctly received");
		}
	}
}
