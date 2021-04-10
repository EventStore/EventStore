using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Configuration;
using YamlDotNet.RepresentationModel;

namespace EventStore.Common.Configuration {
	internal class YamlConfigurationFileParser {
		private readonly IDictionary<string, string> _data =
			new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase);

		private readonly Stack<string> _context = new();
		private string _currentPath;

		public IDictionary<string, string> Parse(Stream input) {
			_data.Clear();
			_context.Clear();

			// https://dotnetfiddle.net/rrR2Bb
			var yaml = new YamlStream();
			yaml.Load(new StreamReader(input, true));

			if (yaml.Documents.Any()) {
				var mapping = (YamlMappingNode)yaml.Documents[0].RootNode;

				// The document node is a mapping node
				VisitYamlMappingNode(mapping);
			}

			return _data;
		}

		private void VisitYamlNodePair(KeyValuePair<YamlNode, YamlNode> yamlNodePair) {
			var context = ((YamlScalarNode)yamlNodePair.Key).Value;
			VisitYamlNode(context, yamlNodePair.Value);
		}

		private void VisitYamlNode(string context, YamlNode node) {
			if (node is YamlScalarNode scalarNode) {
				VisitYamlScalarNode(context, scalarNode);
			}

			if (node is YamlSequenceNode sequenceNode) {
				VisitYamlSequenceNode(context, sequenceNode);
			}
		}

		private void VisitYamlScalarNode(string context, YamlScalarNode yamlValue) {
			//a node with a single 1-1 mapping
			EnterContext(context);
			var currentKey = _currentPath;

			if (_data.ContainsKey(currentKey)) {
				throw new FormatException($"Duplicate key '{currentKey}' found.");
			}

			_data[currentKey] = IsNullValue(yamlValue) ? null : yamlValue.Value;
			ExitContext();
		}

		private void VisitYamlMappingNode(YamlMappingNode node) {
			foreach (var yamlNodePair in node.Children) {
				VisitYamlNodePair(yamlNodePair);
			}
		}

		private void VisitYamlSequenceNode(string context, YamlSequenceNode yamlValue) {
			//a node with an associated list
			EnterContext(context);

			VisitYamlSequenceNode(yamlValue);

			ExitContext();
		}

		private void VisitYamlSequenceNode(YamlSequenceNode node) {
			for (int i = 0; i < node.Children.Count; i++) {
				VisitYamlNode(i.ToString(), node.Children[i]);
			}
		}

		private void EnterContext(string context) {
			_context.Push(context);
			_currentPath = ConfigurationPath.Combine(_context.Reverse());
		}

		private void ExitContext() {
			_context.Pop();
			_currentPath = ConfigurationPath.Combine(_context.Reverse());
		}

		private bool IsNullValue(YamlScalarNode yamlValue) =>
			yamlValue.Style == YamlDotNet.Core.ScalarStyle.Plain &&
			(yamlValue.Value == "~" ||
			 yamlValue.Value == "null" ||
			 yamlValue.Value == "Null" ||
			 yamlValue.Value == "NULL");
	}
}
