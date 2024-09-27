using Microsoft.Extensions.Configuration;

namespace EventStore.Common.Configuration;

public class SectionProvider : ConfigurationProvider {
	private readonly IConfiguration _configuration;
	private readonly string _sectionName;

	public SectionProvider(string sectionName, IConfiguration configuration) {
		_configuration = configuration;
		_sectionName = sectionName;
	}

	public override void Load() {
		foreach (var kvp in _configuration.AsEnumerable()) {
			Data[_sectionName + ":" + kvp.Key] = kvp.Value;
		}
	}
}
