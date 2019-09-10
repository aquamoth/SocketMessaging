using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;

namespace SocketMessaging.Tests
{
	internal class Helpers
	{
		public static bool IsTcpPortListening(int port)
		{
			return IPGlobalProperties.GetIPGlobalProperties()
				.GetActiveTcpListeners()
				.Where(x => x.Port == port)
				.Any();
		}

		public static void WaitFor(Func<bool> func, int timeout = 1000)
		{
			int timeoutCounter = timeout / 10;
			while (!func() && --timeoutCounter > 0)
				System.Threading.Thread.Sleep(10);
		}
	}
}
