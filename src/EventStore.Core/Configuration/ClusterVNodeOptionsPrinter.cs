#nullable enable

using System.Collections.Generic;
using System.Linq;
using System.Text;
using static System.String;

namespace EventStore.Core.Configuration;

public record DisplayOption(OptionMetadata Metadata, string Title, string? Value, string SourceDisplayName, bool IsDefault) {
	public string DisplayValue { get; } = Metadata.IsSensitive ? new('*', 8) : Value ?? Empty;
	
	public override string ToString() => DisplayValue;
}

public static class ClusterVNodeOptionsPrinter {
	public static string Print(IReadOnlyDictionary<string, DisplayOption> displayOptions) {
		var modified = Print(displayOptions, modifiedOnly: true);
		var defaults = Print(displayOptions, modifiedOnly: false);
		return new StringBuilder().AppendLine(modified).Append(defaults).ToString();
	}
	
	static string Print(IReadOnlyDictionary<string, DisplayOption> displayOptions, bool modifiedOnly) {
		var nameColumnWidth = displayOptions.Keys.Select(x => x.Length).Max() + 11;

		var output = new StringBuilder()
			.AppendLine()
			.Append($"{(modifiedOnly ? "MODIFIED" : "DEFAULT")} OPTIONS:");

		foreach (var section in ClusterVNodeOptions.Metadata.OrderBy(x => x.SectionName)) {
			var options = displayOptions
				.Where(x => x.Value.Metadata.SectionName == section.SectionName).ToList();
			
			var firstOption = true;
			
			foreach (var option in options.OrderBy(x => x.Value.Metadata.Name)) {
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
