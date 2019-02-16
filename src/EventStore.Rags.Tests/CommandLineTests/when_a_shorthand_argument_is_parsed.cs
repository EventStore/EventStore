using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EventStore.Rags.Tests.CommandLineTests {
	[TestFixture]
	public class when_a_shorthand_argument_is_parsed {
		[Test]
		public void with_a_trailing_dash_should_return_the_symbol() {
			IEnumerable<OptionSource> result = CommandLine.Parse<TestType>(new[] {"--flag-"});
			Assert.AreEqual(result.Count(), 1);
			Assert.AreEqual(result.First().Name, "flag");
			Assert.AreEqual(false, result.First().IsTyped);
			Assert.AreEqual("-", result.First().Value);
		}

		[Test]
		public void with_a_trailing_positive_should_return_the_symbol() {
			IEnumerable<OptionSource> result = CommandLine.Parse<TestType>(new[] {"--flag+"});
			Assert.AreEqual(result.Count(), 1);
			Assert.AreEqual(result.First().Name, "flag");
			Assert.AreEqual(false, result.First().IsTyped);
			Assert.AreEqual("+", result.First().Value);
		}

		[Test]
		public void with_no_trailing_symbol_should_not_return_empty_string() {
			IEnumerable<OptionSource> result = CommandLine.Parse<TestType>(new[] {"--flag"});
			Assert.AreEqual(result.Count(), 1);
			Assert.AreEqual(result.First().Name, "flag");
			Assert.AreEqual(false, result.First().IsTyped);
			Assert.AreEqual("", result.First().Value);
		}
	}
}
