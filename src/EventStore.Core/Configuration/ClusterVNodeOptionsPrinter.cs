#nullable enable

using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace EventStore.Core.Configuration;

public static class ClusterVNodeOptionsPrinter {
	public static string Print(IReadOnlyDictionary<string, LoadedOption> loadedOptions) {
		var modified = Print(loadedOptions, modifiedOnly: true);
		var defaults = Print(loadedOptions, modifiedOnly: false);
		return new StringBuilder().AppendLine(modified).Append(defaults).ToString();
	}

	private static string Print(IReadOnlyDictionary<string, LoadedOption> loadedOptions, bool modifiedOnly) {
		var nameColumnWidth = loadedOptions.Keys.Select(x => x.Length).Max() + 11;

		var output = new StringBuilder()
			.AppendLine()
			.Append($"{(modifiedOnly ? "MODIFIED" : "DEFAULT")} OPTIONS:");

		foreach (var section in ClusterVNodeOptions.Metadata.OrderBy(x => x.SectionName)) {
			var options = loadedOptions
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
