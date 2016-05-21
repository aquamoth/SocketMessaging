using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SocketMessaging.Tests
{
	[TestClass]
	public class FixedSizedQueueTests
	{
		[TestMethod]
		public void Queue_inits_as_empty()
		{
			var queue = new FixedSizedQueue(1024);
			Assert.AreEqual(0, queue.Count);
		}

		[TestMethod]
		public void Can_write_to_queue_until_full()
		{
			var queue = new FixedSizedQueue(1024);
			queue.Write(new byte[256]);
			Assert.AreEqual(256, queue.Count);
			queue.Write(new byte[256]);
			Assert.AreEqual(512, queue.Count);
			queue.Write(new byte[256]);
			Assert.AreEqual(768, queue.Count);
			queue.Write(new byte[256]);
			Assert.AreEqual(1024, queue.Count);
		}

		[TestMethod]
		[ExpectedException(typeof(OverflowException))]
		public void Cant_write_past_queue_full()
		{
			var queue = new FixedSizedQueue(1024);
			queue.Write(new byte[1025]);
		}

		[TestMethod]
		public void Can_read_entire_queue_at_once()
		{
			var queue = new FixedSizedQueue(1024);
			var writeBuffer = new byte[256];
			new Random().NextBytes(writeBuffer);
			queue.Write(writeBuffer);
			var readBuffer = queue.Read();
			CollectionAssert.AreEqual(writeBuffer, readBuffer);
		}

		[TestMethod]
		public void Can_read_to_queue_empty()
		{
			var queue = new FixedSizedQueue(1024);

			var writeBuffer = new byte[256];
			new Random().NextBytes(writeBuffer);
			queue.Write(writeBuffer);

			var readBuffer = queue.Read(200);
			Assert.AreEqual(200, readBuffer.Length);

			readBuffer = queue.Read(200);
			Assert.AreEqual(56, readBuffer.Length);

			readBuffer = queue.Read(200);
			Assert.AreEqual(0, readBuffer.Length);
		}

		[TestMethod]
		public void Can_read_and_write_more_than_queue_max_size()
		{
			var r = new Random();
			var queue = new FixedSizedQueue(400);

			var write1 = new byte[256];
			r.NextBytes(write1);
			queue.Write(write1);
			var read1 = queue.Read(200);

			var write2 = new byte[256];
			r.NextBytes(write2);
			queue.Write(write2);

			var read2 = queue.Read();

			CollectionAssert.AreEqual(write1.Concat(write2).ToArray(), read1.Concat(read2).ToArray());
		}

	}
}
