using NUnit.Framework;
using System.Linq;

namespace EventStore.Rags.Tests.ExtensionsTests.NormalizeTests {
	[TestFixture]
	public class when_normalizing {
		[Test]
		public void with_a_positive_as_a_value_it_should_return_true_as_the_value() {
			var normalizedValues = new OptionSource[] {new OptionSource("test", "flag", false, "+")}.Normalize();
			Assert.AreEqual(1, normalizedValues.Count());
			Assert.AreEqual("flag", normalizedValues.First().Name);
			Assert.AreEqual(true, normalizedValues.First().IsTyped);
			Assert.AreEqual(true, normalizedValues.First().Value);
		}

		[Test]
		public void with_a_negative_as_a_value_it_should_return_false_as_the_value() {
			var normalizedValues = new OptionSource[] {new OptionSource("test", "flag", false, "-")}.Normalize();
			Assert.AreEqual(1, normalizedValues.Count());
			Assert.AreEqual("flag", normalizedValues.First().Name);
			Assert.AreEqual(true, normalizedValues.First().IsTyped);
			Assert.AreEqual(false, normalizedValues.First().Value);
		}

		[Test]
		public void with_an_empty_string_as_a_value_it_should_return_true_as_the_value() {
			var normalizedValues = new OptionSource[] {new OptionSource("test", "flag", false, "")}.Normalize();
			Assert.AreEqual(1, normalizedValues.Count());
			Assert.AreEqual("flag", normalizedValues.First().Name);
			Assert.AreEqual(true, normalizedValues.First().IsTyped);
			Assert.AreEqual(true, normalizedValues.First().Value);
		}
	}
}
