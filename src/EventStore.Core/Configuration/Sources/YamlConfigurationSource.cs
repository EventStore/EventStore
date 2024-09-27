// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Configuration;
using YamlDotNet.RepresentationModel;
using static System.StringComparer;

namespace EventStore.Core.Configuration.Sources {
	public class YamlConfigurationSource : FileConfigurationSource {
		public string? Prefix { get; init; }

		public override IConfigurationProvider Build(IConfigurationBuilder builder) {
			FileProvider ??= builder.GetFileProvider();
			return new YamlConfigurationProvider(this);
		}
	}

	internal class YamlConfigurationProvider(YamlConfigurationSource configurationSource)
		: FileConfigurationProvider(configurationSource) {

		public override void Load(Stream stream) {
			var parsed = new YamlConfigurationFileParser().Parse(stream);
			var prefix = configurationSource.Prefix;

			Data = string.IsNullOrWhiteSpace(prefix)
				? parsed
				: parsed.ToDictionary(kvp => $"{prefix}:{kvp.Key}", kvp => kvp.Value, OrdinalIgnoreCase);
		}
	}

	internal class YamlConfigurationFileParser {
		private readonly SortedDictionary<string, string?> _data = new(OrdinalIgnoreCase);

		private readonly Stack<string> _context = new();
		private string _currentPath = string.Empty;

		public IDictionary<string, string?> Parse(Stream input) {
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
			var context = ((YamlScalarNode)yamlNodePair.Key).Value!;
			VisitYamlNode(context, yamlNodePair.Value);
		}

		private void VisitYamlNode(string context, YamlNode node) {
			if (node is YamlScalarNode scalarNode)
				VisitYamlScalarNode(context, scalarNode);

			if (node is YamlSequenceNode sequenceNode)
				VisitYamlSequenceNode(context, sequenceNode);
		}

		private void VisitYamlScalarNode(string context, YamlScalarNode yamlValue) {
			//a node with a single 1-1 mapping
			EnterContext(context);
			var currentKey = _currentPath;

			if (_data.ContainsKey(currentKey))
				throw new FormatException($"Duplicate key '{currentKey}' found.");

			if (currentKey.Contains(":")) {
				var key = currentKey.Split(":").First();
				var value = IsNullValue(yamlValue) ? null : yamlValue.Value;
				_data[key] = _data.ContainsKey(key) ? $"{_data[key]},{value}" : value;
			} else {
				_data[currentKey] = IsNullValue(yamlValue) ? null : yamlValue.Value;
			}

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
			yamlValue.Value is "~" or "null" or "Null" or "NULL";
	}
}
