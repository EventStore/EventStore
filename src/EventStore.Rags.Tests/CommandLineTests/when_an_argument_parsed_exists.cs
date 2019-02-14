using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EventStore.Rags.Tests.CommandLineTests {
	[TestFixture]
	public class when_an_argument_parsed_exists {
		[Test]
		public void it_should_return_a_single_option_source() {
			IEnumerable<OptionSource> result = CommandLine.Parse<TestType>(new[] {"--name=bar"});
			Assert.AreEqual(1, result.Count());
			Assert.AreEqual("name", result.First().Name);
			Assert.AreEqual(false, result.First().IsTyped);
			Assert.AreEqual("bar", result.First().Value.ToString());
		}
	}
}
