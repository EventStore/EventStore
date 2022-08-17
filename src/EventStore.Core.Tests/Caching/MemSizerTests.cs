using System.Collections.Generic;
using System.Runtime.CompilerServices;
using EventStore.Core.Caching;
using EventStore.Core.Services.Storage.ReaderIndex;
using NUnit.Framework;

namespace EventStore.Core.Tests.Caching {
	// these tests may break in the future if there are changes in the CLR.
	// the constants can then be adjusted accordingly.
	[TestFixture]
	public class MemSizerTests {

		private class TestObject1 {
			public readonly long TestField1;

			public TestObject1() {
				TestField1 = 0;
			}
		}

		private class TestObject2 {
			public readonly long TestField1;
			public readonly long TestField2;

			public TestObject2() {
				TestField1 = 0;
				TestField2 = 0;
			}
		}

		[Test]
		public void object_header_size_is_correct() {
			// .NET objects have a minimum size of 24 bytes, but the object header size is 16 bytes long.
			// to be able to test the correctness of the object header size constant, we need to have at
			// least one field in the object
			Assert.AreEqual(
				MemSizer.ObjectHeaderSize + sizeof(long),
				MemUsage<TestObject1>.Calculate(() => new TestObject1(), out _));

			Assert.AreEqual(
				MemSizer.ObjectHeaderSize + 2 * sizeof(long),
				MemUsage<TestObject2>.Calculate(() => new TestObject2(), out _));
		}

		[Test]
		public void array_size_is_correct() {
			Assert.AreEqual(
				MemSizer.ArraySize,
				MemUsage<object[]>.Calculate(() => new object[0], out _));

			Assert.AreEqual(
				MemSizer.ArraySize,
				MemUsage<string[]>.Calculate(() => new string[0], out _));
		}

		[Test]
		public void string_size_is_correct() {
			var mem = MemUsage<string>.Calculate(() => new string('x', 1234), out var s);
			Assert.AreEqual(mem, MemSizer.SizeOf(s));
		}

		[Test]
		public void empty_string_size_is_correct() {
			var mem = MemUsage<string>.Calculate(() => new string('x', 0), out var s);
			Assert.AreEqual(mem, MemSizer.SizeOf(s));
		}

		[Test]
		public void string_array_size_is_correct() {
			var mem = MemUsage<string[]>.Calculate(() => {
				var s1 = new string('x', 123);
				var s2 = new string('y', 321);
				return new[] { s1, s2 };
			}, out var arr);

			Assert.AreEqual(mem, MemSizer.SizeOf(arr));
		}
	}
}
