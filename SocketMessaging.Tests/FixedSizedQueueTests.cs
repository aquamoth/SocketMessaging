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
		public void Can_read_until_byte_array_is_found()
		{
			var queue = new FixedSizedQueue(1024);

			var message1 = "This is a simple message|||";
			var message2 = "With a three-pipe delimiter|||";
			var writeBuffer = Encoding.UTF8.GetBytes(message1 + message2);
			queue.Write(writeBuffer);

			var readBuffer = queue.ReadUntil(Encoding.UTF8.GetBytes("|||"), 100);
			Assert.AreEqual(message1, Encoding.UTF8.GetString(readBuffer));
		}

		[TestMethod]
		public void Can_read_with_part_of_delimiter_in_message()
		{
			var queue = new FixedSizedQueue(1024);

			var message = "This is a simple message||With a three-pipe delimiter|||";
			queue.Write(Encoding.UTF8.GetBytes(message));

			var readBuffer = queue.ReadUntil(Encoding.UTF8.GetBytes("|||"), 100);
			Assert.AreEqual(message, Encoding.UTF8.GetString(readBuffer));
		}

		[TestMethod]
		public void Can_read_with_delimiter_crossing_queue_length()
		{
			var queue = new FixedSizedQueue(55);

			var message1 = "This is a simple message|||";
			queue.Write(Encoding.UTF8.GetBytes(message1));
			var readBuffer = queue.ReadUntil(Encoding.UTF8.GetBytes("|||"), 55);

			var message2 = "With a three-pipe delimiter!||And End|||";
			queue.Write(Encoding.UTF8.GetBytes(message2));
			readBuffer = queue.ReadUntil(Encoding.UTF8.GetBytes("|||"), 55);
			Assert.AreEqual(message2, Encoding.UTF8.GetString(readBuffer));
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

		[TestMethod]
		public void Ensure_queue_consistency_around_queue_end()
		{
			var r = new Random();
			var queue = new FixedSizedQueue(10);

			var write1 = new byte[10];
			r.NextBytes(write1);
			queue.Write(write1);
			var read1 = queue.Read();
			CollectionAssert.AreEqual(write1, read1);

			var write2 = new byte[2];
			r.NextBytes(write2);
			queue.Write(write2);
			var read2 = queue.Read();
			CollectionAssert.AreEqual(write2, read2);
		}

		[TestMethod]
		[ExpectedException(typeof(OverflowException))]
		public void Peeking_past_end_of_queue_throws_exception()
		{
			var queue = new FixedSizedQueue(1024);
			var actual = queue.Peek(0, 1);
		}

		[TestMethod]
		public void Can_peek_at_queue()
		{
			var queue = new FixedSizedQueue(100);

			var writeBuffer = new byte[100];
			new Random().NextBytes(writeBuffer);
			queue.Write(writeBuffer);

			var expected = writeBuffer.Skip(70).Take(10).ToArray();
			var actual = queue.Peek(70, 10);
			CollectionAssert.AreEqual(expected, actual, "Peeking past end of queue should return null");
		}

		[TestMethod]
		public void Can_peek_more_than_queue_max_size()
		{
			var queue = new FixedSizedQueue(100);

			var writeBuffer1 = new byte[60];
			new Random().NextBytes(writeBuffer1);
			queue.Write(writeBuffer1);
			queue.Read(50);
			var writeBuffer2 = new byte[60];
			new Random().NextBytes(writeBuffer2);
			queue.Write(writeBuffer2);

			var expected = writeBuffer2.Skip(35).Take(10).ToArray();
			var actualPeeked = queue.Peek(45, 10);
			CollectionAssert.AreEqual(expected, actualPeeked, "Peeking past queue max size should work fine");

			queue.Read(45);
			var actualRead = queue.Read(10);
			CollectionAssert.AreEqual(expected, actualRead, "Reading should return same data as was previously peeked");
		}
	}
}
