using System;
using System.Linq;
using System.Net;
using Xunit;

namespace SocketMessaging.Tests
{
    [Collection("Sequential")]
    public class MessagingTests : IDisposable
	{
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


		[Fact]
		public void Can_switch_messaging_mode()
		{
			Helpers.WaitFor(() => server.Connections.Any());
			Assert.Equal(MessageMode.Raw, client.Mode);//, "Client starts in raw message mode"
            Assert.Equal(MessageMode.Raw, server.Connections.Single().Mode);//, "Server starts in raw message mode"
            client.SetMode(MessageMode.DelimiterBound);
			Assert.Equal(MessageMode.DelimiterBound, client.Mode);//, "Client mode should be DelimiterBound"
            client.SetMode(MessageMode.PrefixedLength);
			Assert.Equal(MessageMode.PrefixedLength, client.Mode);//, "Client mode should be PrefixedLength"
            client.SetMode(MessageMode.FixedLength);
			Assert.Equal(MessageMode.FixedLength, client.Mode);//, "Client mode should be FixedLength"

            client.MaxMessageSize = 10;
			Assert.Equal(10, client.MaxMessageSize);//, "Max message size should be read/write"
        }

		[Fact]
		public void Cant_receive_messages_when_in_raw_packet_mode()
		{
            Assert.Throws<InvalidOperationException>(() =>
            {
			    var buffer = client.ReceiveMessage();
            });
		}

		[Fact]
		public void Connection_returns_null_when_no_message_is_available()
		{
			byte[] buffer;

			client.SetMode(MessageMode.DelimiterBound);
			buffer = client.ReceiveMessage();
			Assert.Null(buffer);//, "Connection should return null when there is no delimited message"

            client.SetMode(MessageMode.FixedLength);
			buffer = client.ReceiveMessage();
			Assert.Null(buffer);//, "Connection should return null when there is no fixed length message"

            client.SetMode(MessageMode.PrefixedLength);
			buffer = client.ReceiveMessage();
			Assert.Null(buffer);//, "Connection should return null when there is no prefixed length message"
        }

		[Fact]
		public void Can_receive_fixed_length_messages()
		{
			var receivedMessageCounter = 0;

			Assert.True(client.MaxMessageSize <= 65536, $"Expected clients initial MaxMessageSize <= 65536 but got {client.MaxMessageSize}.");
			client.MaxMessageSize = 10;
			client.SetMode(MessageMode.FixedLength);
			client.ReceivedMessage += (s, e) => { receivedMessageCounter++; };

			Helpers.WaitFor(() => server.Connections.Any());
			var serverConnection = server.Connections.Single();

			var sentMessage = new byte[30];
			new Random().NextBytes(sentMessage);
			serverConnection.Send(sentMessage);

			Helpers.WaitFor(() => receivedMessageCounter >= 1);
			Assert.True(receivedMessageCounter >= 1, "Client should trigger one message received event");
			var receivedMessage = client.ReceiveMessage();
			Assert.Equal(sentMessage.Take(client.MaxMessageSize).ToArray(), receivedMessage);//, "First message wasn't correctly received"

            Helpers.WaitFor(() => receivedMessageCounter >= 2);
			Assert.True(receivedMessageCounter >= 2, "Client should trigger two message received event");
			receivedMessage = client.ReceiveMessage();
            Assert.Equal(sentMessage.Skip(client.MaxMessageSize).Take(client.MaxMessageSize).ToArray(), receivedMessage);//, "Second message wasn't correctly received"

            Helpers.WaitFor(() => receivedMessageCounter >= 3);
			Assert.True(receivedMessageCounter >= 3, "Client should trigger three message received event");
			receivedMessage = client.ReceiveMessage();
            Assert.Equal(sentMessage.Skip(2 * client.MaxMessageSize).Take(client.MaxMessageSize).ToArray(), receivedMessage);//, "Third message wasn't correctly received"
        }

        [Fact]
        public void Can_receive_large_fixed_length_message()
        {
            var receivedMessageCounter = 0;
            var sentMessage = new byte[2 * 1024 * 1024]; //2MB message
            new Random().NextBytes(sentMessage);

            client.MaxMessageSize = sentMessage.Length;
            client.SetMode(MessageMode.FixedLength);
            client.ReceivedMessage += (s, e) => { receivedMessageCounter++; };

            Helpers.WaitFor(() => server.Connections.Any());
            var serverConnection = server.Connections.Single();

            serverConnection.Send(sentMessage);

            Helpers.WaitFor(() => receivedMessageCounter >= 1);
            Assert.Equal(1, receivedMessageCounter);//, "Client should trigger one message received event"
            var receivedMessage = client.ReceiveMessage();
            Assert.Equal(sentMessage, receivedMessage);//, "Message wasn't correctly received"
        }

        [Fact]
		public void Can_change_delimiter()
		{
            Assert.Equal(new byte[] { 0x0a }, client.Delimiter);
			client.SetDelimiter(new byte[] { 0x00, 0x40 });
            Assert.Equal(new byte[] { 0x00, 0x40 }, client.Delimiter);
			client.SetDelimiter(0x40);
            Assert.Equal(new byte[] { 0x40 }, client.Delimiter);
			client.SetDelimiter('\n');
            Assert.Equal(new byte[] { 0x0a }, client.Delimiter);
			client.MessageEncoding = System.Text.Encoding.GetEncoding("ISO-8859-1");
			client.SetDelimiter("-åäö-");
            Assert.Equal(new byte[] { 45, 229, 228, 246, 45 }, client.Delimiter);
		}

		[Fact]
		public void Changing_delimiter_retriggers_received_messages()
		{
			var receivedMessageCounter = 0;
			client.ReceivedMessage += (s, e) => { receivedMessageCounter++; };
			client.SetMode(MessageMode.DelimiterBound);
            Assert.Equal(new byte[] { 0x0a }, client.Delimiter);

			Helpers.WaitFor(() => server.Connections.Any());
			var serverConnection = server.Connections.Single();
			serverConnection.SetDelimiter(new byte[] { 0x20 });
			serverConnection.SetMode(MessageMode.DelimiterBound);

			var sentMessage = Guid.NewGuid().ToString();
			serverConnection.Send(sentMessage);

			Helpers.WaitFor(() => client.Socket.Available > 0);
			receivedMessageCounter = 0;
			client.SetDelimiter(serverConnection.Delimiter);
            Assert.Equal(new byte[] { 0x20 }, client.Delimiter);

			Helpers.WaitFor(() => receivedMessageCounter >= 1);
			Assert.Equal(1, receivedMessageCounter);//, "Client should trigger one message received event"
            var receivedMessage = client.ReceiveMessageString();
			Assert.Equal(sentMessage, receivedMessage);//, "First message wasn't correctly received"
        }

		[Fact]
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
			Assert.True(receivedMessageCounter >= 1, "Client should trigger one message received event");
			var receivedMessage = client.ReceiveMessage();
            Assert.Equal(sentMessage1, receivedMessage);//, "First message wasn't correctly received"

            Helpers.WaitFor(() => receivedMessageCounter >= 2);
			Assert.True(receivedMessageCounter >= 2, "Client should trigger two message received event");
			receivedMessage = client.ReceiveMessage();
            Assert.Equal(sentMessage2, receivedMessage);//, "Second message wasn't correctly received"

            Helpers.WaitFor(() => receivedMessageCounter >= 3);
			Assert.True(receivedMessageCounter >= 3, "Client should trigger three message received event");
			receivedMessage = client.ReceiveMessage();
            Assert.Equal(sentMessage3, receivedMessage);//, "Third message wasn't correctly received"
        }

		[Fact]
		public void Cant_send_delimited_message_when_delimiter_is_empty()
		{
            Assert.Throws<NotSupportedException>(() =>
            {
			    Helpers.WaitFor(() => server.Connections.Any());
			    var serverConnection = server.Connections.Single();
			    serverConnection.SetDelimiter(new byte[0]);
			    serverConnection.SetMode(MessageMode.DelimiterBound);
			    serverConnection.Send("A");
            });
		}

		[Fact]
		public void Cant_receive_delimited_message_when_delimiter_is_empty()
		{
            Assert.Throws<NotSupportedException>(() =>
            {
                Helpers.WaitFor(() => server.Connections.Any());
			    var serverConnection = server.Connections.Single();
			    serverConnection.SetMode(MessageMode.DelimiterBound);
			    serverConnection.Send("A");

			    client.SetDelimiter(new byte[0]);
			    client.SetMode(MessageMode.DelimiterBound);
			    var message = client.ReceiveMessage();
            });
		}

		[Fact]
		public void Cant_send_message_when_escapecode_is_part_of_delimiter()
		{
            Assert.Throws<NotSupportedException>(() =>
            {
			    Helpers.WaitFor(() => server.Connections.Any());
			    var serverConnection = server.Connections.Single();
			    serverConnection.SetDelimiter(new byte[] { 0x0a, 0x0d, 0x0a });
			    serverConnection.SetEscapecode(0x0d);
			    serverConnection.SetMode(MessageMode.DelimiterBound);
			    serverConnection.Send("A");
            });
		}

		[Fact]
		public void Cant_receive_message_when_escapecode_is_part_of_delimiter()
		{
            Assert.Throws<NotSupportedException>(() =>
            {
                Helpers.WaitFor(() => server.Connections.Any());
			    var serverConnection = server.Connections.Single();
			    serverConnection.SetDelimiter(new byte[] { 0x0a, 0x0d, 0x0a });
			    serverConnection.SetMode(MessageMode.DelimiterBound);
			    serverConnection.Send("A");

			    client.SetDelimiter(serverConnection.Delimiter);
			    client.SetEscapecode(0x0d);
			    client.SetMode(MessageMode.DelimiterBound);
			    var message = client.ReceiveMessage();
            });
        }

        [Fact]
		public void Delimited_messages_cant_overflow_MaxMessageSize()
		{
            Assert.Throws<InvalidOperationException>(() =>
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
            });
        }

        [Fact]
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

			var sentMessage3 = new byte[0];
			var messageSize3 = BitConverter.GetBytes(sentMessage3.Length);
			serverConnection.Send(messageSize3.Concat(sentMessage3).ToArray());

			Helpers.WaitFor(() => receivedMessageCounter >= 1);
			Assert.True(receivedMessageCounter >= 1, "Client should trigger one message received event");
			var receivedMessage = client.ReceiveMessage();
			Assert.Equal(sentMessage1, receivedMessage);//, "First message wasn't correctly received"

            Helpers.WaitFor(() => receivedMessageCounter >= 2);
			Assert.True(receivedMessageCounter >= 2, "Client should trigger two message received event");
			receivedMessage = client.ReceiveMessage();
			Assert.Equal(sentMessage2, receivedMessage);//, "Second message wasn't correctly received"

            Helpers.WaitFor(() => receivedMessageCounter >= 3);
			Assert.True(receivedMessageCounter >= 3, "Client should trigger three message received event");
			receivedMessage = client.ReceiveMessage();
			Assert.Equal(sentMessage3, receivedMessage);//, "Third message wasn't correctly received"
        }

		[Fact]
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
			Assert.Equal(sentMessage, receivedMessage);//, "Message wasn't correctly received"
        }

		[Fact]
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
			Assert.Equal(sentMessage1, receivedMessage);//, "First message wasn't correctly received"

            Helpers.WaitFor(() => receivedMessageCounter >= 2);
			receivedMessage = client.ReceiveMessage();
			Assert.Equal(sentMessage2, receivedMessage);//, "Second message wasn't correctly received"
        }

		[Fact]
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
			Assert.Equal(sentMessage1, receivedMessage);//, "First message wasn't correctly received"

            Helpers.WaitFor(() => receivedMessageCounter >= 2);
			receivedMessage = client.ReceiveMessage();
			Assert.Equal(sentMessage2, receivedMessage);//, "Second message wasn't correctly received"
        }

		[Fact]
		public void Cant_send_messages_larger_than_MaxMessageSize()
		{
            Assert.Throws<ArgumentException>(() =>
            {
			    Helpers.WaitFor(() => server.Connections.Any());
			    var serverConnection = server.Connections.Single();
			    serverConnection.SetMode(MessageMode.PrefixedLength);
			    serverConnection.MaxMessageSize = 4;
			    serverConnection.Send("TestMessage");
            });
		}

		[Fact]
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
			Assert.Equal(sentMessage1, receivedMessage);//, "First message wasn't correctly received"

            Helpers.WaitFor(() => receivedMessageCounter >= 2);
			receivedMessage = client.ReceiveMessage();
			Assert.Equal(sentMessage2, receivedMessage);//, "Second message wasn't correctly received"
        }

		[Fact]
		public void Cant_send_fixed_sized_message_of_wrong_length()
		{
            Assert.Throws<ArgumentException>(() =>
            {
			    Helpers.WaitFor(() => server.Connections.Any());
			    var serverConnection = server.Connections.Single();
			    serverConnection.MaxMessageSize = 30;
			    serverConnection.SetMode(MessageMode.FixedLength);

			    var message = System.Text.Encoding.UTF8.GetBytes(Guid.NewGuid().ToString());
			    serverConnection.Send(message);
            });
		}

		[Fact]
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
			Assert.Equal(sentMessage, receivedMessage);//, "Message wasn't correctly received"
        }

		[Fact]
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
			Assert.Equal(sentMessage, receivedMessage);//, "Message wasn't correctly received"
        }

		[Fact]
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
			serverConnection.SetEscapecode(escapeCode1);
			serverConnection.SetDelimiter(messageDelimiter1);
			client.SetEscapecode(escapeCode1);
			client.SetDelimiter(messageDelimiter1);
			serverConnection.Send(sentMessage1);
			Helpers.WaitFor(() => receivedMessageCounter >= 1);
			var receivedMessage1 = client.ReceiveMessageString();
			Assert.Equal(sentMessage1, receivedMessage1);//, "First message wasn't correctly received"



            receivedMessageCounter = 0;
			var sentMessage2 = @"Message 2! part 1|part 2";
			var messageDelimiter2 = client.MessageEncoding.GetBytes("M");
			var escapeCode2 = client.MessageEncoding.GetBytes("1").Single();
			serverConnection.SetEscapecode(escapeCode2);
			serverConnection.SetDelimiter(messageDelimiter2);
			client.SetEscapecode(escapeCode2);
			client.SetDelimiter(messageDelimiter2);
			serverConnection.Send(sentMessage2);
			Helpers.WaitFor(() => receivedMessageCounter >= 1);
			var receivedMessage2 = client.ReceiveMessageString();
			Assert.Equal(sentMessage2, receivedMessage2);//, "Second message wasn't correctly received"
        }

		[Fact]
		public void Changing_Escapecode_retriggers_received_events()
		{
			byte escapeCode = 0x40;
			var receivedMessageCounter = 0;
			Helpers.WaitFor(() => server.Connections.Any());
			var serverConnection = server.Connections.Single();
			client.ReceivedMessage += (s, e) => { receivedMessageCounter++; };

			serverConnection.SetDelimiter(serverConnection.MessageEncoding.GetBytes(" "));
			serverConnection.SetEscapecode(escapeCode);
			serverConnection.SetMode(MessageMode.DelimiterBound);
			client.SetDelimiter(serverConnection.Delimiter);
			client.SetMode(MessageMode.DelimiterBound);

			var sentMessage = @"Message 1";
			serverConnection.Send(sentMessage);

			Helpers.WaitFor(() => receivedMessageCounter > 0);
			receivedMessageCounter = 0;
			client.SetEscapecode(escapeCode);
			Helpers.WaitFor(() => receivedMessageCounter > 0);
			Assert.True(receivedMessageCounter > 0);

			var receivedMessage = client.ReceiveMessageString();
			Assert.Equal(sentMessage, receivedMessage);//, "First message wasn't correctly received"
        }

		[Fact]
		public void Writing_raw_to_client_that_still_expects_PrefixedLength()
		{
			Helpers.WaitFor(() => server.Connections.Any());
			var serverConnection = server.Connections.Single();
			serverConnection.Send(BitConverter.GetBytes(-100));
			serverConnection.Send(new byte[] { 75 });
			Helpers.WaitFor(() => client.Socket.Available >= 4);

			//This should not throw exception when checking existing buffer for valid messages
			client.SetMode(MessageMode.PrefixedLength);
		}

		[Fact]
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
			Helpers.WaitFor(() => client.Socket.Available >= sentBuffer1.Length);
			var receivedBuffer1 = client.Receive(sentBuffer1.Length);
			Assert.Equal(sentBuffer1, receivedBuffer1);//, "First chunk differs."

            //Receive a fixes length chunk
            receivedMessageCounter = 0;
			client.MaxMessageSize = sentBuffer2.Length;
			client.SetMode(MessageMode.FixedLength);
			Helpers.WaitFor(() => receivedMessageCounter >= 1);
			Assert.True(receivedMessageCounter >= 1, "Changing Mode should retrigger receive-events when receive-queue is not empty");
			var receivedBuffer2 = client.ReceiveMessage();
			client.MaxMessageSize = 65535;
			Assert.Equal(sentBuffer2, receivedBuffer2);//, "Second chunk differs."

            //Receive a delimited message
            receivedMessageCounter = 0;
			client.SetMode(MessageMode.DelimiterBound);
			Helpers.WaitFor(() => receivedMessageCounter >= 1);
			Assert.True(receivedMessageCounter >= 1, "Changing Mode should retrigger receive-events when receive-queue is not empty");
			var receivedMessage3 = client.ReceiveMessageString();
			Assert.Equal(sentMessage3, receivedMessage3);//, "Third message differs."

            //Receive a prefix-length message
            receivedMessageCounter = 0;
			client.SetMode(MessageMode.PrefixedLength);
			Helpers.WaitFor(() => receivedMessageCounter >= 1);
			Assert.True(receivedMessageCounter >= 1, "Changing Mode should retrigger receive-events when receive-queue is not empty");
			var receivedMessage4 = client.ReceiveMessageString();
			Assert.Equal(sentMessage4, receivedMessage4);//, "Fourth message differs."

            //Finish off receiving yet another raw chunk
            client.SetMode(MessageMode.Raw);
			Helpers.WaitFor(() => client.Socket.Available >= sentBuffer5.Length);
			var receivedBuffer5 = client.Receive(sentBuffer5.Length);
			Assert.Equal(sentBuffer5, receivedBuffer5);//, "Last raw chunk differs."

            Assert.Equal(0, client.Socket.Available);//, "The receive queue should be empty after last raw chunk is read."
        }

		[Fact]
		public void Receiving_delimited_message_larger_than_MaxMessageSize_throws_exception()
		{
            Assert.Throws<InvalidOperationException>(() =>
            {
                var message = "This is a long message.";

                Helpers.WaitFor(() => server.Connections.Any());
                var serverConnection = server.Connections.Single();
                serverConnection.SetMode(MessageMode.DelimiterBound);
                serverConnection.Send(message);

                client.MaxMessageSize = 10;
                client.SetMode(MessageMode.DelimiterBound);
                Helpers.WaitFor(() => client.Socket.Available >= message.Length);
                var receivedMessage = client.ReceiveMessageString();
            });
		}

		[Fact]
		public void Receiving_length_prefixed_message_larger_than_MaxMessageSize_throws_exception()
		{
            Assert.Throws<InvalidOperationException>(() =>
            {
                var message = "This is a long message.";

			    Helpers.WaitFor(() => server.Connections.Any());
			    var serverConnection = server.Connections.Single();
			    serverConnection.SetMode(MessageMode.PrefixedLength);
			    serverConnection.Send(message);

			    client.MaxMessageSize = 10;
			    client.SetMode(MessageMode.PrefixedLength);
			    Helpers.WaitFor(() => client.Socket.Available >= message.Length);
			    var receivedMessage = client.ReceiveMessageString();
            });
        }

        [Fact]
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
			Assert.Equal(sentMessage1, receivedMessage1);//, "Message wasn't correctly received"

            Helpers.WaitFor(() => receivedMessageCounter >= 2);
			var receivedMessage2 = client.ReceiveMessageString();
			Assert.Equal(sentMessage2, receivedMessage2);//, "Message wasn't correctly received"
        }
	}
}
