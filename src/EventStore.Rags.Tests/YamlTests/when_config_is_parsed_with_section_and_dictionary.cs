using NUnit.Framework;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace EventStore.Rags.Tests.YamlTests {
	[TestFixture]
	public class when_config_is_parsed_with_section_and_dictionary {
		[Test]
		public void it_should_return_the_options_from_the_config_file() {
			var result = Yaml.FromFile(Path.Combine(TestContext.CurrentContext.WorkDirectory,
				"YamlTests", "config_with_section_and_dictionary.yaml"), "Section");
			Assert.AreEqual(1, result.Count());
			Assert.AreEqual("Roles", result.First().Name);
			Assert.AreEqual(true, result.First().IsTyped);
			var dictionary = result.First().Value as Dictionary<string, string>;

			Assert.AreEqual(new Dictionary<string, string> {
					{"accounting", "$admins"},
					{"it-experts", "$experts"}
				},
				dictionary);
		}
	}
}
