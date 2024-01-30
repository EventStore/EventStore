#nullable enable

using System.IO;
using System.Linq;
using EventStore.Common.Utils;
using Microsoft.Extensions.Configuration;

namespace EventStore.Core.Configuration;

public static class ConfigurationBuilderExtensions {
	public static IConfigurationBuilder AddConfigFile(
		this IConfigurationBuilder builder,
		string configFilePath,
		bool optional = false,
		bool reloadOnChange = false) {

		if (!Locations.TryLocateConfigFile(configFilePath, out var directory, out var fileName)) {
			if (optional)
				return builder;

			throw new FileNotFoundException(
				$"Could not find {configFilePath} in the following directories: {string.Join(", ", Locations.GetPotentialConfigurationDirectories())}");
		}

		builder.AddJsonFile(fileName, optional, reloadOnChange);
		
		Serilog.Log.Logger.Information($"Loaded configuration file {Path.Combine(directory, fileName)}");
		
		return builder;
	}

	// public static IConfigurationBuilder AddConfigFiles(this IConfigurationBuilder builder, string subdirectory, string pattern, bool optional = false,
	// 	bool reloadOnChange = true) {
	// 	// when searching for a file we check the directories in forward order until we find it
	// 	// so when adding all the files we apply them in reverse order to keep the same precedence
	// 	foreach (var directory in Locations.GetPotentialConfigurationDirectories().Reverse()) {
	// 		var configDirectory = Path.Combine(directory, subdirectory);
	// 		if (!Directory.Exists(configDirectory))
	// 			continue;
	//
	// 		foreach (var configFile in Directory.EnumerateFiles(configDirectory, pattern).Order()) 
	// 			builder.AddConfigFile(configFile, optional: optional, reloadOnChange: reloadOnChange);
	// 	}
	//
	// 	return builder;
	// }
	
	/// <summary>
	/// DefaultConfigurationDirectory,
	/// ApplicationDirectory,
	/// </summary>
	
	public static IConfigurationBuilder AddConfigFiles(this IConfigurationBuilder builder, string[] directories, string pattern, bool optional = false, bool reloadOnChange = true) {
		foreach (var configFile in directories.Where(Directory.Exists).SelectMany(x => Directory.EnumerateFiles(x, pattern))) 
			builder.AddConfigFile(configFile, optional: optional, reloadOnChange: reloadOnChange);

		return builder;
	}
}
