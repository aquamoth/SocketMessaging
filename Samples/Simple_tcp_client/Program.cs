using SocketMessaging;
using System;
using System.Net;
using System.Text;

namespace Simple_tcp_client
{
    class Program
    {
        static void Main(string[] args)
        {

            Console.WriteLine($"Welcome to the simple Tcp client.");
            Console.WriteLine("");

            var (serverName, port) = ReadConfigurationFromUser();

            Console.WriteLine($"Connecting to {serverName}:{port}...");

            if (serverName == "localhost")
                serverName = "127.0.0.1";

            try
            {
                var client = SocketMessaging.TcpClient.Connect(IPAddress.Parse(serverName), port);
                Console.WriteLine($"Connected.");

                RunChatClient(client);
            }
            catch (System.Net.Sockets.SocketException ex)
            {
                Console.WriteLine("ERROR: " + ex.Message);
            }
        }

        private static void RunChatClient(SocketMessaging.TcpClient client)
        {
            client.SetMode(MessageMode.DelimiterBound);
            client.ReceivedMessage += (sender, e) => Console.Write(client.ReceiveMessageString());

            Console.WriteLine("Write something in the chat.");
            Console.WriteLine("Press Enter on an empty line to disconnect.");
            Console.WriteLine("");
            Console.WriteLine("Communication log:");
            Console.WriteLine("==================");


            string message;
            do
            {
                message = Console.ReadLine();
                if (message == "")
                    break;

                client.Send(message);
            } while (client.IsConnected);

            if (!client.IsConnected)
            {
                Console.WriteLine("Forcefully disconnected by server.");
            }
            else
            {
                client.Close();
                Console.WriteLine("User disconnected.");
            }
        }

        private static (string serverName, int port) ReadConfigurationFromUser()
        {
            Console.WriteLine("Select a server:port to connect to (leave empty for [localhost:8778]): ");
            var input = Console.ReadLine();

            var serverAndPortString = input.Split(':', 2);

            var serverName = serverAndPortString[0];
            if (serverName == "")
                serverName = "localhost";

            var port = 8778;
            if (serverAndPortString.Length > 1)
                int.TryParse(serverAndPortString[1], out port);

            return (serverName, port);
        }
    }
}
