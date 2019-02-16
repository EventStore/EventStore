using NUnit.Framework;
using System.IO;
using System.Linq;

namespace EventStore.Rags.Tests.YamlTests {
	[TestFixture]
	public class when_config_is_parsed {
		[Test]
		public void it_should_return_the_options_from_the_config_file() {
			var result = Yaml.FromFile(Path.Combine(TestContext.CurrentContext.WorkDirectory,
				"YamlTests", "valid_config.yaml"));
			Assert.AreEqual(1, result.Count());
			Assert.AreEqual("Name", result.First().Name);
			Assert.AreEqual(false, result.First().IsTyped);
			Assert.AreEqual("foo", result.First().Value);
		}
	}
}
