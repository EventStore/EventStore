using NUnit.Framework;
using System.Linq;

namespace EventStore.Rags.Tests.ExtensionsTests.UseAliasTests {
	[TestFixture]
	public class when_using_aliases {
		public class TestType {
			[ArgAlias("FirstFlagAlias")] public string FirstFlag { get; set; }

			[ArgAlias("SecondFlagAlias", "AlternateSecondFlagAlias")]
			public string SecondFlag { get; set; }
		}

		[Test]
		public void with_a_property_that_contains_a_single_alias() {
			var optionSources = new OptionSource[] {new OptionSource("test", "firstflagalias", false, "value")}
				.UseAliases<TestType>();
			Assert.AreEqual(1, optionSources.Count());
			Assert.NotNull(optionSources.FirstOrDefault(x =>
				x.Name.Equals("firstflag", System.StringComparison.OrdinalIgnoreCase)));
			Assert.AreEqual("value",
				optionSources.FirstOrDefault(x => x.Name.Equals("firstflag", System.StringComparison.OrdinalIgnoreCase))
					.Value);
		}

		[Test]
		public void with_a_property_that_contains_a_single_alias_but_not_used() {
			var optionSources = new OptionSource[] {new OptionSource("test", "firstflag", false, "value")}
				.UseAliases<TestType>();
			Assert.AreEqual(1, optionSources.Count());
			Assert.NotNull(optionSources.FirstOrDefault(x =>
				x.Name.Equals("firstflag", System.StringComparison.OrdinalIgnoreCase)));
			Assert.AreEqual("value",
				optionSources.FirstOrDefault(x => x.Name.Equals("firstflag", System.StringComparison.OrdinalIgnoreCase))
					.Value);
		}

		[Test]
		public void with_a_property_that_contains_multiple_aliases() {
			var optionSources = new OptionSource[]
				{new OptionSource("test", "alternatesecondflagalias", false, "value")}.UseAliases<TestType>();
			Assert.AreEqual(1, optionSources.Count());
			Assert.NotNull(optionSources.FirstOrDefault(x =>
				x.Name.Equals("secondflag", System.StringComparison.OrdinalIgnoreCase)));
			Assert.AreEqual("value",
				optionSources
					.FirstOrDefault(x => x.Name.Equals("secondflag", System.StringComparison.OrdinalIgnoreCase)).Value);
		}
	}
}
