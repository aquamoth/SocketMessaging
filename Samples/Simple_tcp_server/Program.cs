using SocketMessaging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Simple_tcp_server
{
    class Program
    {
        static void Main(string[] args)
        {
            _tcpServer = new SocketMessaging.Server.TcpServer();
            _tcpServer.Connected += TcpServer_Connected;
            _tcpServer.Start(8778);

            Console.WriteLine($"Tcp server started on port {_tcpServer.LocalEndpoint.Port}.");
            Console.WriteLine("Press Enter to stop server.");
            Console.ReadLine();
        }

        private static void TcpServer_Connected(object sender, ConnectionEventArgs e)
        {
            e.Connection.SetMode(MessageMode.DelimiterBound);
            e.Connection.ReceivedMessage += Connection_ReceivedMessage;
            e.Connection.Disconnected += Connection_Disconnected;

            var tcpServer = sender as SocketMessaging.Server.TcpServer;
            var otherClients = tcpServer.Connections.Except(new[] { e.Connection });
            foreach (var client in otherClients)
            {
                e.Connection.Send($"#{client.Id} has joined.");
                client.Send($"#{e.Connection.Id} has joined.");
            }
        }

        private static void Connection_Disconnected(object sender, EventArgs e)
        {
            var disconnectedClient = sender as Connection;
            var otherClients = _tcpServer.Connections.Except(new[] { disconnectedClient });
            foreach (var client in otherClients)
            {
                client.Send($"#{disconnectedClient.Id} has left.");
            }
        }

        private static void Connection_ReceivedMessage(object sender, EventArgs e)
        {
            var connection = sender as Connection;
            var message = connection.ReceiveMessageString();

            var otherClients = _tcpServer.Connections.Except(new[] { connection });
            foreach (var client in otherClients)
            {
                client.Send($"#{connection.Id}: {message}");
            }
        }


        static SocketMessaging.Server.TcpServer _tcpServer;
    }
}
