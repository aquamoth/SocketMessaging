using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SocketMessaging
{
	public enum MessageMode
	{
		Raw = 0,
		DelimiterBound,
		PrefixedLength,
		FixedLength,
	}
}
