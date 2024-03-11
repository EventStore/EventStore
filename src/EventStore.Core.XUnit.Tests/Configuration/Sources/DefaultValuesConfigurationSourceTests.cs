using System.Linq;
using EventStore.Core.Configuration.Sources;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace EventStore.Core.XUnit.Tests.Configuration {
	public class DefaultValuesConfigurationSourceTests {
		[Fact]
		public void Adds() {
			// Arrange
			var defaults = ClusterVNodeOptions.DefaultValues.OrderBy(x => x.Key).ToList();
		
			// Act
			var configuration = new ConfigurationBuilder()
				.AddEventStoreDefaultValues()
				.Build()
				.GetSection(EventStoreConfigurationKeys.Prefix);
		
			// Assert
			foreach (var (key, expectedValue) in defaults) {
				configuration.GetValue<object>(key)
					.Should()
					.BeEquivalentTo(expectedValue?.ToString(), $"because {key} should be {expectedValue}");
			}
		}
	}
}