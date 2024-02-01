// ReSharper disable CheckNamespace

#nullable enable

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text;
using EventStore.Common.Configuration;
using EventStore.Common.Utils;
using EventStore.Core.Configuration;
using EventStore.Core.Configuration.Sources;
using Microsoft.Extensions.Configuration;

namespace EventStore.Core;

public partial record ClusterVNodeOptions {
	public static readonly IEnumerable<Type> OptionSections;
	public static readonly string HelpText;
	public static readonly List<SectionMetadata> Metadata;
	public static readonly IEnumerable<KeyValuePair<string, object?>> DefaultValues;

	static ClusterVNodeOptions() {
		OptionSections = typeof(ClusterVNodeOptions)
			.GetProperties(BindingFlags.Public | BindingFlags.Instance)
			.Where(p => p.GetCustomAttribute<OptionGroupAttribute>() != null)
			.Select(p => p.PropertyType);
		
		HelpText = GetHelpText();
		
		Metadata = typeof(ClusterVNodeOptions)
			.GetProperties(BindingFlags.Public | BindingFlags.Instance)
			.Where(prop => prop.GetCustomAttribute<OptionGroupAttribute>() != null)
			// .Where(p => p.Name.EndsWith("Options", StringComparison.OrdinalIgnoreCase)
			.Select(SectionMetadata.FromPropertyInfo)
			.ToList();
		
		DefaultValues = OptionSections.SelectMany(GetDefaultValues);
		
		return;

		static IEnumerable<KeyValuePair<string, object?>> GetDefaultValues(Type type) {
			var defaultInstance = Activator.CreateInstance(type)!;

			return type.GetProperties().Select(property =>
				new KeyValuePair<string, object?>(property.Name, property.PropertyType switch {
					{IsArray: true} => string.Join(",",
						((Array)(property.GetValue(defaultInstance) ?? Array.Empty<object>())).OfType<object>()),
					_ => property.GetValue(defaultInstance)
				}));
		}
	}

	public IReadOnlyDictionary<string, DisplayOption> DisplayOptions { get; init; } = new Dictionary<string, DisplayOption>();
	
	public static IReadOnlyDictionary<string, DisplayOption> GetDisplayOptions(IConfigurationRoot configurationRoot) {
		var printableOptions = new Dictionary<string, DisplayOption>();
		
		// because we always start with defaults, we can just add them all first.
		// then we can override them with the actual values.
		foreach (var provider in configurationRoot.Providers) {
			var providerType = provider.GetType();
	
			foreach (var option in ClusterVNodeOptions.Metadata.SelectMany(x => x.Options)) {
				if (!provider.TryGet(option.Value.Key, out var value)) continue;
				
				var title             = GetTitle(option);
				var sourceDisplayName = GetSourceDisplayName(providerType);
				var isDefault         = providerType == typeof(EventStoreDefaultValuesConfigurationProvider);
					
				printableOptions[option.Value.Key] = new(
					Metadata: option.Value,
					Title: title,
					Value: value,
					SourceDisplayName: sourceDisplayName,
					IsDefault: isDefault
				);
			}
		}
	
		return printableOptions;
		
		static string CombineByPascalCase(string name, string token = " ") {
			var regex = new System.Text.RegularExpressions.Regex(
				@"(?<=[A-Z])(?=[A-Z][a-z])|(?<=[^A-Z])(?=[A-Z])|(?<=[A-Za-z])(?=[^A-Za-z])");
			return regex.Replace(name, token);
		}

		static string GetSourceDisplayName(Type source) {
			var name = source == typeof(EventStoreDefaultValuesConfigurationProvider)
				? "<DEFAULT>"
				: CombineByPascalCase(
					source.Name
						.Replace("EventStore", "")
						.Replace("ConfigurationProvider", "")
				);
			
			return $"({name})";
		}

		string GetTitle(KeyValuePair<string, OptionMetadata> option) => 
			CombineByPascalCase(EventStoreConfigurationKeys.StripConfigurationPrefix(option.Value.Key)).ToUpper();
	}
		
	public string GetComponentName() => $"{Interface.NodeIp}-{Interface.NodePort}-cluster-node";

	public string? DumpOptions() =>
		ConfigurationRoot != null 
			? ClusterVNodeOptionsPrinter.Print(DisplayOptions) 
			: null;
	
	public string? GetDeprecationWarnings() {
		var defaultValues = new Dictionary<string, object?>(DefaultValues, StringComparer.OrdinalIgnoreCase);

		var deprecationWarnings = from section in OptionSections
			from option in section.GetProperties()
			let deprecationWarning = option.GetCustomAttribute<DeprecatedAttribute>()?.Message
			where deprecationWarning is not null
			let value = ConfigurationRoot?.GetValue<string?>(option.Name)
			where defaultValues.TryGetValue(option.Name, out var defaultValue)
			      && !string.Equals(value, defaultValue?.ToString(), StringComparison.OrdinalIgnoreCase)
			select deprecationWarning;

		var builder = deprecationWarnings
			.Aggregate<string, StringBuilder>(new StringBuilder(), (builder, deprecationWarning) => builder.AppendLine(deprecationWarning));

		return builder.Length != 0 ? builder.ToString() : null;
	}

	public string? CheckForEnvironmentOnlyOptions() => 
		ConfigurationRoot.CheckProvidersForEnvironmentVariables(OptionSections);

	// static string GetHelpText() {
	// 	const string OPTION      = nameof(OPTION);
	// 	const string DESCRIPTION = nameof(DESCRIPTION);
	// 	const string DEFAULT     = nameof(DEFAULT);
	//
	// 	var optionColumnWidth = Options().Max(o => OptionHeaderColumnWidth(o.Name, DefaultValue(o)));
	//
	// 	var header = $"{OPTION.PadRight(optionColumnWidth, ' ')}{DESCRIPTION}";
	// 	
	// 	var environmentOnlyOptions = Metadata.SelectMany(section => section.Options.Values)
	// 		.Where(option => option.IsEnvironmentOnly)
	// 		.Select(option => option)
	// 		.ToList();
	// 	
	// 	var environmentOnlyOptionsBuilder = environmentOnlyOptions
	// 		.Aggregate(new StringBuilder(), (builder, option) => builder.Append(GetEnvironmentOptionHelpText(option, optionColumnWidth)).AppendLine())
	// 		.ToString();
	//
	// 	var sections = Metadata.Select(
	// 			section => section with {
	// 				Options = section.Options.Values
	// 					.Where(option => !option.IsEnvironmentOnly)
	// 					.ToDictionary(option => option.Key, x => x)
	// 			}
	// 		)
	// 		.ToList();
	//
	// 	var builder = new StringBuilder().Append(header).AppendLine();
	// 	
	// 	foreach (var section in sections) {
	// 		builder.AppendLine().Append(section.Key).AppendLine();
	// 		
	// 	}
	// 	
	// 	//
	// 	// var optionGroups = options.GroupBy(option =>
	// 	// 	option.DeclaringType?.GetCustomAttribute<DescriptionAttribute>()?.Description ?? string.Empty);
	//
	// 	return optionGroups
	// 		.Aggregate(new StringBuilder().Append(header).AppendLine(), (builder, optionGroup) =>
	// 			optionGroup.Aggregate(
	// 				builder.AppendLine().Append(optionGroup.Key).AppendLine(),
	// 				(stringBuilder, property) => stringBuilder.Append(Line(property)).AppendLine()))
	// 		.AppendLine().AppendLine("EnvironmentOnly Options").Append(environmentOnlyOptionsBuilder)
	// 		.ToString();
	// 		
	//
	// 	string Line(OptionMetadata option) {
	// 		var description = option.Description;
	// 		
	// 		if (option.AllowedValues.Any()) 
	// 			description += $" ({string.Join(", ", option.AllowedValues)})";
	//
	// 		return GetOption(option).PadRight(optionColumnWidth, ' ') + description;
	// 	}
	//
	// 	static string GetOption(OptionMetadata option) {
	// 		var builder = new StringBuilder();
	// 		
	// 		builder.AppendJoin(string.Empty, GnuOption(option.Name));
	//
	// 		var defaultValue = DefaultValue(option);
	// 		if (defaultValue != string.Empty) {
	// 			builder.Append(" (Default:").Append(defaultValue).Append(')');
	// 		}
	//
	// 		return builder.ToString();
	// 	}
	// 		
	// 	static IEnumerable<PropertyInfo> Options() => OptionSections.SelectMany(type => type.GetProperties());
	//
	// 	static int OptionWidth(string name, string @default) =>
	// 		(name + @default).Count(char.IsUpper) + 1 + 1 + (name + @default).Length;
	//
	// 	int OptionHeaderColumnWidth(string name, string @default) =>
	// 		Math.Max(OptionWidth(name, @default) + 1, OPTION.Length);
	//
	// 	static string DefaultValue(OptionMetadata option) {
	// 		var value = option.GetValue(Activator.CreateInstance(option.DeclaringType!));
	// 		return (value, Runtime.IsWindows) switch {
	// 			(bool b, false) => b.ToString().ToLower(),
	// 			(bool b, true) => b.ToString(),
	// 			(Array {Length: 0}, _) => string.Empty,
	// 			(Array {Length: >0} a, _) => string.Join(",", a.OfType<object>()),
	// 			_ => value?.ToString() ?? string.Empty
	// 		};
	// 	}
	//
	// 	static IEnumerable<char> GnuOption(string x) {
	// 		yield return '-';
	// 		foreach (var c in x) {
	// 			if (char.IsUpper(c))
	// 				yield return '-';
	//
	// 			yield return char.ToLower(c);
	// 		}
	// 	}
	// 	
	// 	static string GetEnvironmentOptionHelpText(OptionMetadata option, int optionColumnWidth) {
	// 		var envVar  = option.FullKey.Replace(":", "_").ToUpper();
	// 		return envVar.PadRight(optionColumnWidth, ' ') + option.ErrorMessage;
	// 	}
	// }
	
	static string GetHelpText() {
		const string OPTION      = nameof(OPTION);
		const string DESCRIPTION = nameof(DESCRIPTION);
		const string DEFAULT     = nameof(DEFAULT);
	
		var optionColumnWidth = Options().Max(o =>
			OptionHeaderColumnWidth(o.Name, DefaultValue(o)));
	
		var header = $"{OPTION.PadRight(optionColumnWidth, ' ')}{DESCRIPTION}";
			
		var environmentOnlyOptions = OptionSections.SelectMany(section => section.GetProperties())
			.Where(option => option.GetCustomAttribute<EnvironmentOnlyAttribute>() != null)
			.Select(option => option)
			.ToList();
	
		var environmentOnlyOptionsBuilder = environmentOnlyOptions
			.Aggregate(new StringBuilder(),
				(builder, property) => builder.Append(GetEnvironmentOption(property, optionColumnWidth)).AppendLine())
			.ToString();
	
		var options = Options().Where(option =>
			environmentOnlyOptions.All(environmentOption => environmentOption.Name != option.Name)).ToList();
	
		var optionGroups = options.GroupBy(option =>
			option.DeclaringType?.GetCustomAttribute<DescriptionAttribute>()?.Description ?? string.Empty);
	
		return optionGroups
			.Aggregate(new StringBuilder().Append(header).AppendLine(), (builder, optionGroup) =>
				optionGroup.Aggregate(
					builder.AppendLine().Append(optionGroup.Key).AppendLine(),
					(stringBuilder, property) => stringBuilder.Append(Line(property)).AppendLine()))
			.AppendLine().AppendLine("EnvironmentOnly Options").Append(environmentOnlyOptionsBuilder)
			.ToString();
			
	
		string Line(PropertyInfo property) {
			var description = property.GetCustomAttribute<DescriptionAttribute>()?.Description;
			if (property.PropertyType.IsEnum) {
				description += $" ({string.Join(", ", Enum.GetNames(property.PropertyType))})";
			}
	
			return GetOption(property).PadRight(optionColumnWidth, ' ') + description;
		}
	
		string GetOption(PropertyInfo property) {
			var builder = new StringBuilder();
			builder.AppendJoin(string.Empty, GnuOption(property.Name));
	
			var defaultValue = DefaultValue(property);
			if (defaultValue != string.Empty) {
				builder.Append(" (Default:").Append(defaultValue).Append(')');
			}
	
			return builder.ToString();
		}
			
		static IEnumerable<PropertyInfo> Options() => OptionSections.SelectMany(type => type.GetProperties());
	
		static int OptionWidth(string name, string @default) =>
			(name + @default).Count(char.IsUpper) + 1 + 1 + (name + @default).Length;
	
		int OptionHeaderColumnWidth(string name, string @default) =>
			Math.Max(OptionWidth(name, @default) + 1, OPTION.Length);
	
		static string DefaultValue(PropertyInfo option) {
			var value = option.GetValue(Activator.CreateInstance(option.DeclaringType!));
			return (value, Runtime.IsWindows) switch {
				(bool b, false) => b.ToString().ToLower(),
				(bool b, true) => b.ToString(),
				(Array {Length: 0}, _) => string.Empty,
				(Array {Length: >0} a, _) => string.Join(",", a.OfType<object>()),
				_ => value?.ToString() ?? string.Empty
			};
		}
	
		static IEnumerable<char> GnuOption(string x) {
			yield return '-';
			foreach (var c in x) {
				if (char.IsUpper(c)) {
					yield return '-';
				}
	
				yield return char.ToLower(c);
			}
		}
		
		static string GetEnvironmentOption(PropertyInfo property, int optionColumnWidth) {
			const string Prefix = "EVENTSTORE";

			var builder = new StringBuilder();
			
			builder.Append($"{Prefix}_")
				.Append(CombineByPascalCase(property.Name, "_").ToUpper());
			
			var description = property.GetCustomAttribute<EnvironmentOnlyAttribute>()?.Message;
					
			return builder.ToString().PadRight(optionColumnWidth, ' ') + description;
			
			static string CombineByPascalCase(string name, string token = " ") {
				var regex = new System.Text.RegularExpressions.Regex(
					@"(?<=[A-Z])(?=[A-Z][a-z])|(?<=[^A-Z])(?=[A-Z])|(?<=[A-Za-z])(?=[^A-Za-z])");
				return regex.Replace(name, token);
			}
		}
	}

	[AttributeUsage(AttributeTargets.Property)]
	internal class OptionGroupAttribute : Attribute;
}

public record SectionMetadata(string Key, string SectionName, string Description, Type SectionType, Dictionary<string, OptionMetadata> Options, int Sequence) {
	public static SectionMetadata FromPropertyInfo(PropertyInfo property, int sequence) {
		var name        = property.Name.Replace("Options", String.Empty);
		var key         = EventStoreConfigurationKeys.Normalize(name);
		var description = property.PropertyType.GetCustomAttribute<DescriptionAttribute>()?.Description ?? String.Empty;
				
		var options = property.PropertyType.GetProperties()
			.OrderBy(x => x.Name)
			.Select(OptionMetadata.FromPropertyInfo)
			.ToDictionary(option => option.Key, x => x);

		return new(
			Key: key,
			SectionName: name,
			Description: description,
			SectionType: property.PropertyType,
			Options: options,
			Sequence: sequence
		);
	}
}

public record OptionMetadata(
	string Key, string FullKey, string Name, string Description, string SectionName, string[] AllowedValues,
	bool IsSensitive, bool IsEnvironmentOnly, string? ErrorMessage, int Sequence
) {
	public override string ToString() => FullKey;
	
	public static OptionMetadata FromPropertyInfo(PropertyInfo property, int sequence) {
		var sectionName = property.DeclaringType?.Name.Replace("Options", "") ?? String.Empty;
		var key         = EventStoreConfigurationKeys.Normalize(property.Name);
		var fullKey     = $"EventStore:{sectionName}:{EventStoreConfigurationKeys.StripConfigurationPrefix(property.Name)}";

		var description = property.GetCustomAttribute<DescriptionAttribute>()?.Description ?? String.Empty;
		var isSensitive = property.GetCustomAttribute<SensitiveAttribute>() != null;

		var environmentOnlyAttribute = property.GetCustomAttribute<EnvironmentOnlyAttribute>();
		var isEnvironmentOnly = environmentOnlyAttribute != null;
		var errorMessage      = environmentOnlyAttribute?.Message;
		
		string[] allowedValues = property.PropertyType.IsEnum 
			? property.PropertyType.GetEnumNames() 
			: [];

		return new(
			Key: key,
			FullKey: fullKey,
			Name: property.Name,
			Description: description,
			SectionName: sectionName,
			AllowedValues: allowedValues,
			IsSensitive: isSensitive,
			IsEnvironmentOnly: isEnvironmentOnly,
			ErrorMessage: errorMessage,
			Sequence: sequence
		);
	}
}
