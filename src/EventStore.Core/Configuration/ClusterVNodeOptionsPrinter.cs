#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using EventStore.Core.Configuration.Sources;
using Microsoft.Extensions.Configuration;
using static System.String;

namespace EventStore.Core.Configuration;

public record PrintableOption(OptionMetadata Metadata, string Title, string? Value, string SourceDisplayName, bool IsDefault) {
	public string DisplayValue { get; } = Metadata.IsSensitive ? new('*', 8) : Value ?? Empty;
	
	public override string ToString() => DisplayValue;
}

public class ClusterVNodeOptionsPrinter {
	public ClusterVNodeOptionsPrinter(IConfigurationRoot configurationRoot) {
		ConfigurationRoot = configurationRoot;
		PrintableOptions  = GetPrintableOptions();
	}

	IConfigurationRoot                  ConfigurationRoot { get; }
	Dictionary<string, PrintableOption> PrintableOptions  { get; }

	public IReadOnlyDictionary<string, PrintableOption> Options => PrintableOptions;
	
	Dictionary<string, PrintableOption> GetPrintableOptions() {
		var printableOptions = new Dictionary<string, PrintableOption>();
		
		// because we always start with defaults, we can just add them all first.
		// then we can override them with the actual values.
		foreach (var provider in ConfigurationRoot.Providers) {
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
	
	public string Print() {
		var modified = Print(modifiedOnly: true);
		var defaults = Print(modifiedOnly: false);
		return new StringBuilder().AppendLine(modified).Append(defaults).ToString();
	}
	
	string Print(bool modifiedOnly) {
		var nameColumnWidth = PrintableOptions.Keys.Select(x => x.Length).Max() + 11;

		var output = new StringBuilder()
			.AppendLine()
			.Append($"{(modifiedOnly ? "MODIFIED" : "DEFAULT")} OPTIONS:");

		foreach (var section in ClusterVNodeOptions.Metadata.OrderBy(x => x.SectionName)) {
			var options = PrintableOptions
				.Where(x => x.Value.Metadata.SectionName == section.SectionName).ToList();
			
			bool firstOption = true;
			
			foreach (var option in options) {
				if (option.Value.IsDefault == modifiedOnly)
					continue;

				if (firstOption) {
					output
						.AppendLine()
						.Append($"    {section.Description.ToUpper()}:")
						.AppendLine();
					firstOption = false;
				}

				output
					.Append($"         {option.Value.Title}:".PadRight(nameColumnWidth, ' '))
					.Append(option.Value)
					.Append(' ')
					.Append(option.Value.SourceDisplayName)
					.AppendLine();
			}
		}
			
		return output.ToString();
	}
}
