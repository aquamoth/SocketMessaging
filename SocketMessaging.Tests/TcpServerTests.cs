using System;
using System.Net;
using Xunit;
using SocketMessaging.Server;
using System.Linq;

namespace SocketMessaging.Tests
{
    [Collection("Sequential")]
    public class TcpServerTests : IDisposable
    {
        const int SERVER_PORT = 7732;
        readonly Server.TcpServer server;

        public TcpServerTests()
        {
            Assert.False(Helpers.IsTcpPortListening(SERVER_PORT), "Port should be closed at start of test.");
            server = new Server.TcpServer();
            server.Start(SERVER_PORT);
        }

        public void Dispose()
        {
            server.Stop();
        }

        [Fact]
        public void Starts_and_stops_multiple_times()
        {
            Assert.True(server.IsStarted);
            Assert.True(Helpers.IsTcpPortListening(SERVER_PORT), "Port should be open when server has started.");
            var serverPollThread = server._pollThread;

            server.Stop();
            Assert.False(server.IsStarted);
            Assert.False(Helpers.IsTcpPortListening(SERVER_PORT), "Port should be closed when server has stopped.");
            Assert.True(System.Threading.ThreadState.Stopped == serverPollThread.ThreadState, "Polling thread stops when server stops.");

            server.Start(SERVER_PORT);
            Assert.True(server.IsStarted);
            Assert.True(Helpers.IsTcpPortListening(SERVER_PORT), "Port should be open when server has started.");
        }

        [Fact]
        public void Should_not_start_when_started()
        {
            Assert.Throws<InvalidOperationException>(() =>
            {
                server.Start(SERVER_PORT + 1);
            });
        }

        [Fact]
        public void Exposes_local_endpoint()
        {
            var endpoint = server.LocalEndpoint as IPEndPoint;
            Assert.Equal(SERVER_PORT, endpoint.Port);
        }

        [Fact]
        public void Server_triggers_Connected()
        {
            Connection connectedClient = null;

            server.Connected += (s1, e1) => {
                connectedClient = e1.Connection;
            };

            Assert.Null(connectedClient);//, "Server should not publish connected client before connection."
            var serverAddress = new IPAddress(new byte[] { 127, 0, 0, 1 });
            var client = TcpClient.Connect(serverAddress, SERVER_PORT);
            try
            {
                Helpers.WaitFor(() => connectedClient != null);
                Assert.NotNull(connectedClient);//, "Server should publish connected client after connection."
            }
            finally
            {
                client.Close();
            }
        }

        [Fact]
        public void Enumerates_connected_clients()
        {
            var serverAddress = new IPAddress(new byte[] { 127, 0, 0, 1 });

            Assert.Equal(0, server.Connections.Count());//, "No clients before first connection"

            var client1 = TcpClient.Connect(serverAddress, SERVER_PORT);
            Helpers.WaitFor(() => server.Connections.Any());
            Assert.Equal(1, server.Connections.Count());//, "One client connected"
            Assert.Equal(1, server.Connections.First().Id);//, "Connection Id"

            var client2 = TcpClient.Connect(serverAddress, SERVER_PORT);
            Helpers.WaitFor(() => server.Connections.Count() >= 2);
            Assert.Equal(2, server.Connections.Count());//, "Two clients connected"
            Assert.Equal(1, server.Connections.First().Id);//, "Connection Id"
            Assert.Equal(2, server.Connections.Skip(1).First().Id);//, "Connection Id"

            client1.Close();
            Helpers.WaitFor(() => server.Connections.Count() != 2);
            Assert.Equal(1, server.Connections.Count());//, "One client disconnected"
            Assert.Equal(2, server.Connections.First().Id);//, "Connection Id"

            client2.Close();
            Helpers.WaitFor(() => !server.Connections.Any());
            Assert.Equal(0, server.Connections.Count());//, "All clients disconnected"
        }
    }
}
