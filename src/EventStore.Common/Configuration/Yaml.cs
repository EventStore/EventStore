using System.IO;
using Microsoft.Extensions.Configuration;

namespace EventStore.Common.Configuration {
	internal class Yaml : FileConfigurationProvider {
		public Yaml(YamlSource source) : base(source) { }

		public override void Load(Stream stream) {
			var parser = new YamlConfigurationFileParser();

			Data = parser.Parse(stream);
		}
	}
}
