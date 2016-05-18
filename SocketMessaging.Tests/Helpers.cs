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
	}
}
