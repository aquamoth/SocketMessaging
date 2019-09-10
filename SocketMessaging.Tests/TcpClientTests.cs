using System;
using System.Net;
using System.Net.Sockets;
using Xunit;

//[assembly: CollectionBehavior(CollectionBehavior.CollectionPerAssembly)]

namespace SocketMessaging.Tests
{
    [Collection("Sequential")]
    public class TcpClientTests : IDisposable
	{
		const int SERVER_PORT = 7783;
		readonly Server.TcpServer server;
		TcpClient client = null;

		public TcpClientTests()
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
		public void Can_connect_and_disconnect_to_running_server()
		{
			connectClient();
			Assert.True(client.IsConnected, "IsConnected should be true after connection.");
			var clientPollThread = client._pollThread;
			client.Close();
			Assert.False(client.IsConnected, "IsConnected should be false after disconnection.");
			Helpers.WaitFor(() => clientPollThread.ThreadState == System.Threading.ThreadState.Stopped);
			Assert.Equal(System.Threading.ThreadState.Stopped, clientPollThread.ThreadState);//, "Polling thread stops when client disconnects"
        }

		[Fact]
		public void Does_not_connect_to_closed_server()
		{
            Assert.ThrowsAny<SocketException>(() =>
            {
			    server.Stop();
			    connectClient();
            });
		}



		private void connectClient()
		{
			var serverAddress = new IPAddress(new byte[] { 127, 0, 0, 1 });
			client = TcpClient.Connect(serverAddress, SERVER_PORT);
		}
	}
}
