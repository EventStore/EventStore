using Microsoft.Extensions.Configuration;

namespace EventStore.Common.Configuration {
	public class YamlSource : FileConfigurationSource {
		public override IConfigurationProvider Build(IConfigurationBuilder builder) {
			FileProvider ??= builder.GetFileProvider();
			return new Yaml(this);
		}
	}
}
