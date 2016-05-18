using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Net;

namespace SocketMessaging.Tests
{
	[TestClass]
	public class TcpServerTests : IDisposable
	{
		const int SERVER_PORT = 7732;
		readonly TcpServer server;

		public TcpServerTests()
		{
			Assert.IsFalse(Helpers.IsTcpPortListening(SERVER_PORT), "Port should be closed at start of test.");
			server = new TcpServer();
			server.Start(SERVER_PORT);
		}

		public void Dispose()
		{
			server.Stop();
		}

		[TestMethod]
		public void Starts_and_stops_multiple_times()
		{
			Assert.IsTrue(server.IsStarted);
			Assert.IsTrue(Helpers.IsTcpPortListening(SERVER_PORT), "Port should be open when server has started.");

			server.Stop();
			Assert.IsFalse(server.IsStarted);
			Assert.IsFalse(Helpers.IsTcpPortListening(SERVER_PORT), "Port should be closed when server has stopped.");

			server.Start(SERVER_PORT);
			Assert.IsTrue(server.IsStarted);
			Assert.IsTrue(Helpers.IsTcpPortListening(SERVER_PORT), "Port should be open when server has started.");
		}

		[TestMethod]
		[ExpectedException(typeof(InvalidOperationException))]
		public void Should_not_start_when_started()
		{
			server.Start(SERVER_PORT + 1);
		}

		[TestMethod]
		public void Exposes_local_endpoint()
		{
			var endpoint = server.LocalEndpoint as IPEndPoint;
			Assert.AreEqual(SERVER_PORT, endpoint.Port);
		}
	}
}
