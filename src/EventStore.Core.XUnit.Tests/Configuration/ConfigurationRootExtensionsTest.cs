// #nullable enable
//
// using System.Collections.Generic;
// using EventStore.Core.Configuration;
// using EventStore.Core.Configuration.Sources;
// using Microsoft.Extensions.Configuration;
// using Xunit;
//
// namespace EventStore.Core.XUnit.Tests.Configuration;
//
// public class ConfigurationRootExtensionsTest {
// 	const string CONFIG_FILE_KEY = "Config";
// 	
// 	[Fact]
// 	public void user_specified_config_file_through_environment_variables_returns_true() {
// 		var configurationRoot = new ConfigurationBuilder()
// 			.AddEventStoreEnvironmentVariables(new Dictionary<string, string> {
// 				{ "EVENTSTORE_CONFIG", "pathToConfigFileOnMachine" }
// 			})
// 			.Build();
//
// 		var result = configurationRoot
// 			.IsSettingUserSpecified($"{EventStoreConfigurationKeys.Prefix}:{CONFIG_FILE_KEY}");
//
// 		Assert.True(result);
// 	}
// 	
// 	[Fact]
// 	public void user_specified_config_file_through_command_line_returns_true() {
// 		var args = new[] {
// 			"--config=pathToConfigFileOnMachine"
// 		};
//
// 		var configurationRoot = new ConfigurationBuilder()
// 			.AddEventStoreCommandLine(args)
// 			.Build();
//
// 		var result = configurationRoot
// 			.IsSettingUserSpecified($"{EventStoreConfigurationKeys.Prefix}:{CONFIG_FILE_KEY}");
//
// 		Assert.True(result);
// 	}
//
// 	[Fact]
// 	public void user_did_not_specified_config_file_returns_false() {
// 		var configurationRoot = new ConfigurationBuilder()
// 			.AddEventStoreDefaultValues(new Dictionary<string, string?>())
// 			.Build();
//
// 		var result = configurationRoot
// 			.IsSettingUserSpecified($"{EventStoreConfigurationKeys.Prefix}:{CONFIG_FILE_KEY}");
//
// 		Assert.False(result);
// 	}
// }
