using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using YamlDotNet.RepresentationModel;

namespace EventStore.Rags {
	public class Yaml {
		public static IEnumerable<OptionSource> FromFile(string fileName, string sectionName = null) {
			var options = new List<OptionSource>();
			if (!File.Exists(fileName)) {
				throw new FileNotFoundException(fileName);
			}

			var yamlStream = new YamlStream();
			var reader = new StringReader(File.ReadAllText(fileName));
			try {
				yamlStream.Load(reader);
			} catch (Exception ex) {
				throw new OptionException(
					String.Format("An invalid configuration file has been specified. {0}{1}", Environment.NewLine,
						ex.Message), "config");
			}

			var yamlNode = (YamlMappingNode)yamlStream.Documents[0].RootNode;

			if (!String.IsNullOrEmpty(sectionName)) {
				Func<KeyValuePair<YamlNode, YamlNode>, bool> predicate = x =>
					x.Key.ToString() == sectionName && x.Value.GetType() == typeof(YamlMappingNode);

				var nodeExists = yamlNode.Children.Any(predicate);
				if (nodeExists) {
					yamlNode = (YamlMappingNode)yamlNode.Children.First(predicate).Value;
				}
			}

			foreach (var yamlElement in yamlNode.Children) {
				var yamlScalarNode = yamlElement.Value as YamlScalarNode;
				var yamlSequenceNode = yamlElement.Value as YamlSequenceNode;
				var yamlMappingNode = yamlElement.Value as YamlMappingNode;
				if (yamlSequenceNode != null) {
					var values = yamlSequenceNode.Children.Select(x => ((YamlScalarNode)x).Value);
					try {
						//TODO GFY DO WE PREFER STRINGS OR TYPES HERE?
						options.Add(OptionSource.String("Config File", yamlElement.Key.ToString(), values.ToArray()));
					} catch (InvalidCastException) {
						var message = String.Format("Please ensure that {0} is a valid YAML array.{1}", yamlElement.Key,
							Environment.NewLine);
						throw new OptionException(message, yamlElement.Key.ToString());
					}
				} else if (yamlScalarNode != null) {
					options.Add(OptionSource.String("Config File", yamlElement.Key.ToString(),
						yamlElement.Value.ToString()));
				} else if (yamlMappingNode != null && !string.IsNullOrEmpty(sectionName)) {
					options.Add(OptionSource.Typed("Config File", yamlElement.Key.ToString(),
						yamlMappingNode.ToDictionary(x => x.Key.ToString(), x => x.Value.ToString())));
				}
			}

			return options;
		}
	}
}
