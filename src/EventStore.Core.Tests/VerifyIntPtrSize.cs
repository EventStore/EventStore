using System;
using NUnit.Framework;

namespace EventStore.Core.Tests {
	[TestFixture]
	public class VerifyIntPtrSize {
		[Test]
		public void TestIntPtrSize() {
			Assert.AreEqual(8, IntPtr.Size);
		}
	}
}
