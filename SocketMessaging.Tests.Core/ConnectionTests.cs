using System;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Diagnostics;
using SocketMessaging.Server;
using System.Collections.Generic;
using System.Threading;
using Xunit;

namespace SocketMessaging.Tests.Core
{
    [Collection("Sequential")]
    public class ConnectionTests : IDisposable
	{
		const int SERVER_PORT = 7783;
		readonly Server.TcpServer server;
		TcpClient client = null;
		readonly Random rnd = new Random();

		public ConnectionTests()
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




		[Fact]
		public void Connection_triggers_Disconnected()
		{
			var serverDisconnectedTriggered = false;
			var clientDisconnectedTriggered = false;

			server.Connected += (s1, e1) => {
				e1.Connection.Disconnected += (s2, e2) => serverDisconnectedTriggered = true;
			};

			connectClient();
			client.Disconnected += (s2, e2) => clientDisconnectedTriggered = true;
			Helpers.WaitFor(() => client.IsConnected);

			Assert.False(serverDisconnectedTriggered, "Connection should not trigger disconnected event before client disconnects.");
			client.Close();
			Helpers.WaitFor(() => serverDisconnectedTriggered && clientDisconnectedTriggered);
			Assert.True(serverDisconnectedTriggered, "Server Connection should trigger disconnected event when client disconnects.");
			Assert.True(clientDisconnectedTriggered, "Client should trigger disconnected event when client disconnects.");
		}

		[Fact]
		public void Client_can_send_packet_to_server()
		{
			connectClient();
			Helpers.WaitFor(() => server.Connections.Any());
			var serverConnection = server.Connections.Single();

			var buffer1 = new byte[100];
			rnd.NextBytes(buffer1);
			var buffer2 = new byte[100];
			rnd.NextBytes(buffer2);
			var expectedBuffer = buffer1.Concat(buffer2).ToArray();

			client.Send(buffer1);
			client.Send(buffer2);

			var buffer = new byte[0].AsQueryable<byte>();
			var actualLength = 0;
			while (actualLength < expectedBuffer.Length)
			{
				Helpers.WaitFor(() => serverConnection.Socket.Available > 0);
				Assert.True(serverConnection.Socket.Available > 0, "Server should receive packet.");
				var data = serverConnection.Receive();
				buffer = buffer.Concat(data);
				actualLength += data.Length;
			}

			Assert.Equal(expectedBuffer, buffer.ToArray());
		}

		[Fact]
		public void Server_can_send_packet_to_client()
		{
			connectClient();
			Helpers.WaitFor(() => server.Connections.Any());
			var serverConnection = server.Connections.Single();

			var buffer1 = new byte[100];
			rnd.NextBytes(buffer1);
			var buffer2 = new byte[100];
			rnd.NextBytes(buffer2);
			var expectedBuffer = buffer1.Concat(buffer2).ToArray();

			serverConnection.Send(buffer1);
			serverConnection.Send(buffer2);

			var buffer = new byte[0].AsQueryable<byte>();
			var actualLength = 0;
			while (actualLength < expectedBuffer.Length)
			{
				Helpers.WaitFor(() => client.Socket.Available > 0);
				Assert.True(client.Socket.Available > 0, "Client should receive packets.");
				var data = client.Receive();
				buffer = buffer.Concat(data);
				actualLength += data.Length;
			}

			Assert.Equal(expectedBuffer, buffer.ToArray());
		}

		[Fact]
		public void Polling_threads_are_threadsafe()
		{
			connectClient();
			Helpers.WaitFor(() => server.Connections.Any());
			var serverConnection = server.Connections.Single();

			var targetBuffer = 140000;

			Trace.TraceInformation("Starting threads");
			var serverSendState = new ThreadSafeState(serverConnection, targetBuffer);
			var serverSendThread = new Thread(new ParameterizedThreadStart(Polling_threads_are_threadsafe__Send));
			serverSendThread.Start(serverSendState);

			var clientReceiveState = new ThreadSafeState(client, targetBuffer);
			var clientReceiveThread = new Thread(new ParameterizedThreadStart(Polling_threads_are_threadsafe__Receive));
			clientReceiveThread.Start(clientReceiveState);

			var clientSendState = new ThreadSafeState(client, targetBuffer);
			var clientSendThread = new Thread(new ParameterizedThreadStart(Polling_threads_are_threadsafe__Send));
			clientSendThread.Start(clientSendState);

			var serverReceiveState = new ThreadSafeState(serverConnection, targetBuffer);
			var serverReceiveThread = new Thread(Polling_threads_are_threadsafe__Receive);
			serverReceiveThread.Start(serverReceiveState);

			Trace.TraceInformation("Waiting for senders to complete");
			serverSendThread.Join();
			clientSendThread.Join();

			Trace.TraceInformation("Waiting grace time before disconnecting");
			Thread.Sleep(200);
			serverConnection.Close();

			Trace.TraceInformation("Waiting for receivers to complete");
			clientReceiveThread.Join();
			serverReceiveThread.Join();

			Trace.TraceInformation("Asserting state");
			Assert.True(serverSendState.Buffer.Count() >= serverSendState.TargetBuffer, "Server should send a big buffer");
			Assert.Equal(serverSendState.Buffer.ToArray(), clientReceiveState.Buffer.ToArray());//, "Client should have received what server sent"

            Assert.True(clientSendState.Buffer.Count() >= clientSendState.TargetBuffer, "Client should send a big buffer");
			Assert.Equal(clientSendState.Buffer.ToArray(), serverReceiveState.Buffer.ToArray());//, "Server should have received what client sent"
        }
		private void Polling_threads_are_threadsafe__Send(object o)
		{
			var state = o as ThreadSafeState;
			var count = 0;
			while (count < state.TargetBuffer)
			{
				var buffer = new byte[rnd.Next(1000, 2000)];
				rnd.NextBytes(buffer);
				state.Connection.Send(buffer);
				state.Buffer = state.Buffer.Concat(buffer);
				count += buffer.Length;
			}
		}
		private void Polling_threads_are_threadsafe__Receive(object o)
		{
			var state = o as ThreadSafeState;
			while (state.Connection.IsConnected || state.Connection.Socket.Available > 0)
			{
				var buffer = state.Connection.Receive();
				state.Buffer = state.Buffer.Concat(buffer).ToArray().AsQueryable();
			}
		}

		[Fact]
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

			Assert.Equal(0, receiveEvents);//, "Connection should not trigger receive raw event before client sends something."

            client.Send(buffer);
			Helpers.WaitFor(() => receiveEvents != 0);
			Assert.Equal(1, receiveEvents);//, "Connection should trigger received raw event after first send."

            client.Send(buffer);
			Helpers.WaitFor(() => receiveEvents != 1, 100);
			Assert.Equal(2, receiveEvents);//, "Connection should trigger received raw after second send."

            var receiveBuffer = serverConnection.Receive();
			Helpers.WaitFor(() => receiveEvents != 2, 100);
			Assert.Equal(2, receiveEvents);//, "Connection should not trigger received raw events just because buffer was read."

            client.Send(buffer);
			Helpers.WaitFor(() => receiveEvents != 2);
			Assert.Equal(3, receiveEvents);//, "Connection should trigger received raw event after third send."
        }

		[Fact]
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
			rnd.NextBytes(buffer);
			client.Send(buffer);
			Helpers.WaitFor(() => connectionBufferLength >= buffer.Length);

			Assert.Equal(buffer, connectionBuffer.ToArray());//, "Connection should receive the same data the client sent."
        }

		[Fact]
		public void Can_read_stream_after_connection_closed()
		{
			connectClient();
			Helpers.WaitFor(() => server.Connections.Any());
			var serverConnection = server.Connections.First();

			var sentBuffer = new byte[10];
			rnd.NextBytes(sentBuffer);
			serverConnection.Send(sentBuffer);
			serverConnection.Close();

			Helpers.WaitFor(() => !client.IsConnected);
			var receiveBuffer = client.Receive();

			Assert.Equal(sentBuffer, receiveBuffer);//, "Client should receive what server sent event after a disconnect."
        }


		private void connectClient()
		{
			var serverAddress = new IPAddress(new byte[] { 127, 0, 0, 1 });
			client = TcpClient.Connect(serverAddress, SERVER_PORT);
		}

		class ThreadSafeState
		{
			public Connection Connection { get; set; }
			public int TargetBuffer { get; private set; }
			public IQueryable<byte> Buffer { get; set; }
			public ThreadSafeState(Connection connection, int targetBuffer)
			{
				Connection = connection;
				Buffer = new byte[0].AsQueryable();
				TargetBuffer = targetBuffer;
			}
		}
	}
}
