using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text;
using EventStore.Common.Configuration;
using EventStore.Core.Configuration.Sources;
using Microsoft.Extensions.Configuration;

namespace EventStore.Core.Configuration;

[Obsolete("Use the printer instead")]
public class OptionsDumper {
	private readonly IEnumerable<Type> _optionSections;
	private readonly List<(string, List<string>)> _groups;

	public OptionsDumper(IEnumerable<Type> optionSections) {
		_optionSections = optionSections;
		_groups = _optionSections
			.Select(sectionType => {
				var description = sectionType.GetCustomAttribute<DescriptionAttribute>()?.Description.ToUpper()
				                  ?? string.Empty;
				var props = sectionType
					.GetProperties()
					.Select(x => EventStoreConfigurationKeys.Normalize(x.Name))
					.OrderBy(x => x)
					.ToList();
				return (description, props);
			})
			.OrderBy(x => x.description)
			.ToList();
	}

	public string Dump(IConfigurationRoot configurationRoot) {
		var options = GetOptionSourceInfo(configurationRoot);
		var info = options
			.Where(x => x.Value.Source is not null)
			.ToDictionary(
				x => x.Key,
				y => (y.Value.Source, y.Value.Value)
			);

		bool Modified(Type x) => x != typeof(EventStoreDefaultValuesConfigurationProvider);
		bool Default(Type x) => x == typeof(EventStoreDefaultValuesConfigurationProvider);
			
		var nameColumnWidth = info.Keys.Select(x => x.Length).Max() + 11;
			
		return PrintOptions(nameof(Modified), info, Modified) + Environment.NewLine +
		       PrintOptions(nameof(Default), info, Default);

		string PrintOptions(
			string name,
			IDictionary<string, (Type source, string value)> configOptions,
			Func<Type, bool> filter) {

			var dumpOptions = new StringBuilder();
			dumpOptions.AppendLine().Append($"{name.ToUpper()} OPTIONS:");

			foreach (var (groupName, groupOptions) in _groups) {
				bool firstOptionInGroup = true;

				foreach (var option in groupOptions) {
					if (!configOptions.TryGetValue(option, out var optionValue) ||
					    !filter(optionValue.source))
						continue;
							
					if (firstOptionInGroup) {
						dumpOptions.AppendLine().Append($"    {groupName}:").AppendLine();
						firstOptionInGroup = false;
					}
						
					dumpOptions.Append($"         {FormatOptionKey(EventStoreConfigurationKeys.StripConfigurationPrefix(option))}:".PadRight(nameColumnWidth, ' '))
						.Append(optionValue.value).Append(' ')
						.Append(FormatSourceName(optionValue.source))
						.AppendLine();
				}
			}
			return dumpOptions.ToString();
		}
			
		static string FormatSourceName(Type source) =>
			$"({(source == typeof(EventStoreDefaultValuesConfigurationProvider) ? "<DEFAULT>" : NameTranslators.CombineByPascalCase(source.Name
				.Replace("EventStore", "")
				.Replace("ConfigurationProvider", ""), " "))})"
		;
	}
	static string FormatOptionKey(string option) =>
		NameTranslators.CombineByPascalCase(option, " ").ToUpper();

	internal static class NameTranslators {
		public static string PrefixEnvironmentVariable(string name, string prefix) {
			return prefix + CombineByPascalCase(name, "_");
		}

		public static string CombineByPascalCase(string name, string token) {
			var regex = new System.Text.RegularExpressions.Regex(
				@"(?<=[A-Z])(?=[A-Z][a-z])|(?<=[^A-Z])(?=[A-Z])|(?<=[A-Za-z])(?=[^A-Za-z])");
			return regex.Replace(name, token);
		}
			
		public static string None(string name) {
			return name;
		}
	}

	public Dictionary<string, PrintableOption> GetOptionSourceInfo(IConfigurationRoot configurationRoot) {
		if (configurationRoot is null) return null;

		var result = new Dictionary<string, PrintableOption>();
		foreach (var section in _optionSections) {
			foreach (var property in section.GetProperties()) {
				var      argumentDescriptionAttribute = property.GetCustomAttribute<DescriptionAttribute>();
				var      sensitiveAttribute           = property.GetCustomAttribute<SensitiveAttribute>();
				string[] allowedValues                = null;
				if (property.PropertyType.IsEnum) {
					allowedValues = property.PropertyType.GetEnumNames();
				}

				result[EventStoreConfigurationKeys.Normalize(property.Name)] = new PrintableOption(
					property.Name,
					argumentDescriptionAttribute?.Description,
					section.Name,
					allowedValues,
					sensitiveAttribute != null
				);
			}
		}

		foreach (var provider in configurationRoot.Providers) {
			var source = provider.GetType();
			foreach (var key in provider.GetChildKeys().Select(EventStoreConfigurationKeys.Normalize)) {
				if (provider.TryGet(key, out var value)) {
					if(result.TryGetValue(key, out var opt)) {
						result[key] = opt.WithValue(value, source);
					}
				}
			}
		}

		return result;
	}
	
	public record PrintableOption {
		public readonly string Name;
		public readonly string Description;
		public readonly string Group;
		public readonly string[] AllowedValues;
		public readonly string Value;
		public readonly Type Source;

		private readonly bool _isSensitive;

		public PrintableOption(
			string name,
			string description,
			string group,
			string[] allowedValues,
			bool isSensitive,
			string value = null,
			Type source = null) {
			Name          = name;
			Description   = description;
			Group         = group;
			AllowedValues = allowedValues;
			_isSensitive  = isSensitive;
			Value         = isSensitive ? new string('*', 8) : value ?? string.Empty;
			Source        = source;
		}

		public PrintableOption WithValue(string value, Type source) =>
			new(Name, Description, Group, AllowedValues, _isSensitive, value, source);
	}
}


