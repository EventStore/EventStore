using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace EventStore.Rags.Tests.OptionApplicatorTests {
	[TestFixture]
	public class when_option_applicator_get {
		[Test]
		public void with_a_typed_option_that_exists_should_set() {
			var result = OptionApplicator.Get<TestType>(new OptionSource[] {
				new OptionSource("test", "Flag", true, true)
			});
			Assert.AreEqual(true, result.Flag);
		}

		[Test]
		public void with_a_non_typed_option_that_exists_should_set() {
			var result = OptionApplicator.Get<TestType>(new OptionSource[] {
				new OptionSource("test", "Flag", false, "foo")
			});
			Assert.AreEqual("foo", result.Name);
		}

		[Test]
		public void with_a_non_typed_complex_option_that_exists_should_set() {
			var result = OptionApplicator.Get<TestType>(new OptionSource[] {
				new OptionSource("test", "IpEndpoint", false, "10.0.0.1:2113")
			});
			Assert.AreEqual(new IPEndPoint(IPAddress.Parse("10.0.0.1"), 2113), result.IpEndpoint);
		}

		[Test]
		public void with_a_non_typed_option_that_does_not_exist_should_not_set() {
			var referenceType = new TestType();
			var result = OptionApplicator.Get<TestType>(new OptionSource[] {
				new OptionSource("test", "NonExistent", false, "bar")
			});
			Assert.AreEqual(referenceType.Flag, result.Flag);
			Assert.AreEqual(referenceType.IpEndpoint, result.IpEndpoint);
			Assert.AreEqual(referenceType.Name, result.Name);
		}

		[Test]
		public void with_a_non_typed_option_that_is_an_array_should_set() {
			var result = OptionApplicator.Get<TestType>(new OptionSource[] {
				new OptionSource("test", "Names", false, new string[] {"four", "five", "six"})
			});
			Assert.AreEqual(new string[] {"four", "five", "six"}, result.Names);
		}

		[Test]
		public void with_a_non_typed_complex_option_that_is_an_array_should_set() {
			var result = OptionApplicator.Get<TestType>(new OptionSource[] {
				new OptionSource("test", "IpEndpoints", false, new string[] {"10.0.0.1:2112", "10.0.0.2:2113"})
			});
			Assert.AreEqual(2, result.IpEndpoints.Length);
			Assert.AreEqual(new IPEndPoint[] {
					new IPEndPoint(IPAddress.Parse("10.0.0.1"), 2112),
					new IPEndPoint(IPAddress.Parse("10.0.0.2"), 2113)
				},
				result.IpEndpoints);
		}
	}
}
