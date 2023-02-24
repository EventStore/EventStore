using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EventStore.Common.Utils;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;

namespace EventStore.Common.Configuration {
	public class TelemetryConfiguration {
		public static IConfiguration FromFile(string telemetryConfig = "telemetryconfig.json") {
			var configurationDirectory = Path.IsPathRooted(telemetryConfig)
				? Path.GetDirectoryName(telemetryConfig)
				: Locations
					.GetPotentialConfigurationDirectories()
					.FirstOrDefault(directory => File.Exists(Path.Combine(directory, telemetryConfig)));

			if (configurationDirectory == null) {
				throw new FileNotFoundException(
					$"Could not find {telemetryConfig} in the following directories: {string.Join(", ", Locations.GetPotentialConfigurationDirectories())}");
			}

			var configurationRoot = new ConfigurationBuilder()
				.AddJsonFile(config => {
					config.Optional = false;
					config.FileProvider = new PhysicalFileProvider(configurationDirectory);
					config.OnLoadException = context => Serilog.Log.Error(context.Exception, "err");
					config.Path = Path.GetFileName(telemetryConfig);
				})
				.Build();

			return configurationRoot;
		}

		public enum StatusTracker {
			Index = 1,
			Node,
			Scavenge
		}

		public enum Checkpoint {
			Chaser = 1,
			Epoch,
			Index,
			Proposal,
			Replication,
			StreamExistenceFilter,
			Truncate,
			Writer,
		}

		public enum GrpcMethod {
			StreamRead = 1,
			StreamAppend,
			StreamBatchAppend,
			StreamDelete,
			StreamTombstone,
		}

		public class LabelMappingCase {
			public string Regex { get; set; }
			public string Label { get; set; }
		}

		public string[] Meters { get; set; } = Array.Empty<string>();

		public StatusTracker[] StatusTrackers { get; set; } = Array.Empty<StatusTracker>();

		public Checkpoint[] Checkpoints { get; set; } = Array.Empty<Checkpoint>();

		public Dictionary<GrpcMethod, string> GrpcMethods { get; set; } = new();

		// must be 0, 1, 5, 10 or a multiple of 15
		public int ExpectedScrapeIntervalSeconds { get; set; }

		public LabelMappingCase[] Queues { get; set; } = Array.Empty<LabelMappingCase>();

		public LabelMappingCase[] MessageTypes { get; set; } = Array.Empty<LabelMappingCase>();
	}
}
