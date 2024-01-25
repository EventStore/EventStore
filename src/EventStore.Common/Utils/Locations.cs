﻿using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace EventStore.Common.Utils {
	public class Locations {
		public static readonly string ApplicationDirectory;
		public static readonly string WebContentDirectory;
		public static readonly string ProjectionsDirectory;
		public static readonly string PreludeDirectory;
		public static readonly string PreludeResourcesPath;
		public static readonly string PluginsDirectory;
		public static readonly string DefaultContentDirectory;
		public static readonly string DefaultConfigurationDirectory;
		public static readonly string DefaultDataDirectory;
		public static readonly string DefaultLogDirectory;
		public static readonly string DefaultTestClientLogDirectory;
		public static readonly string FallbackDefaultDataDirectory;
		public static readonly string DefaultTrustedRootCertificateDirectory;

		static Locations() {
			ApplicationDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ??
			                       Path.GetFullPath(".");

			PluginsDirectory = Path.Combine(ApplicationDirectory, "plugins");
			FallbackDefaultDataDirectory = Path.Combine(ApplicationDirectory, "data");

			switch (Platforms.GetPlatform()) {
				case Platform.Linux:
					DefaultContentDirectory = "/usr/share/eventstore";
					DefaultConfigurationDirectory = "/etc/eventstore";
					DefaultDataDirectory = "/var/lib/eventstore";
					DefaultLogDirectory = "/var/log/eventstore";
					DefaultTrustedRootCertificateDirectory = "/etc/ssl/certs";
					DefaultTestClientLogDirectory = Path.Combine(ApplicationDirectory, "testclientlog");
					if (!Directory.Exists(PluginsDirectory))
						PluginsDirectory = Path.Combine(DefaultContentDirectory, "plugins");
					break;
				case Platform.Mac:
					DefaultContentDirectory = "/usr/local/share/eventstore";
					DefaultConfigurationDirectory = "/etc/eventstore";
					DefaultDataDirectory = "/var/lib/eventstore";
					DefaultLogDirectory = "/var/log/eventstore";
					DefaultTestClientLogDirectory = Path.Combine(ApplicationDirectory, "testclientlog");
					if (!Directory.Exists(PluginsDirectory))
						PluginsDirectory = Path.Combine(DefaultContentDirectory, "plugins");
					break;
				default:
					DefaultContentDirectory = ApplicationDirectory;
					DefaultConfigurationDirectory = ApplicationDirectory;
					DefaultDataDirectory = Path.Combine(ApplicationDirectory, "data");
					DefaultLogDirectory = Path.Combine(ApplicationDirectory, "logs");
					DefaultTestClientLogDirectory = Path.Combine(ApplicationDirectory, "testclientlog");
					break;
			}

			WebContentDirectory = GetPrecededLocation(
				Path.Combine(ApplicationDirectory, "clusternode-web"),
				Path.Combine(DefaultContentDirectory, "clusternode-web")
			);
			ProjectionsDirectory = GetPrecededLocation(
				Path.Combine(ApplicationDirectory, "projections"),
				Path.Combine(DefaultContentDirectory, "projections")
			);
			PreludeDirectory = GetPrecededLocation(
				Path.Combine(ApplicationDirectory, "Prelude"),
				Path.Combine(DefaultContentDirectory, "Prelude")
			);
			PreludeResourcesPath = "EventStore.Projections.Core.Prelude";
		}

		/// <summary>
		/// Returns the preceded location by checking the existence of the directory.
		/// The local directory should be the first priority as the first element followed by
		/// the global default location as last element.
		/// </summary>
		/// <param name="locations">the locations ordered by prioity starting with the preceded location</param>
		/// <returns>the preceded location</returns>
		public static string GetPrecededLocation(params string[] locations) {
			var precedenceList = locations.Distinct().ToList();
			return precedenceList.FirstOrDefault(Directory.Exists) ??
			       precedenceList.Last();
		}

		/// <summary>
		/// Returns the directories that potentially contain any configuration files.
		/// </summary>
		/// <returns></returns>
		public static string[] GetPotentialConfigurationDirectories() => new[] {
			DefaultConfigurationDirectory,
			ApplicationDirectory,
		}.Distinct().ToArray();

		/// Tries to identify the name and path of the given config file.
		/// Given a full path to a file it just splits the path into directory and file.
		/// Given a only config file name, looks for that file in the potential directories.
		public static bool TryLocateConfigFile(string configFilePath, out string directory, out string fileName) {
			directory = Path.IsPathRooted(configFilePath)
				? Path.GetDirectoryName(configFilePath)
				: GetPotentialConfigurationDirectories().FirstOrDefault(d =>
					File.Exists(Path.Combine(d, configFilePath)));

			fileName = Path.GetFileName(configFilePath);

			return directory is not null;
		}
	}
}
