using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Reflection;

namespace EventStore.Rags {
	public class ArgumentUsageInfo {
		public string Name { get; set; }

		public List<string> Aliases { get; private set; }

		public List<string> PossibleValues { get; private set; }

		public string Description { get; set; }

		public string Group { get; set; }

		public PropertyInfo Property { get; set; }

		private ArgumentUsageInfo() {
			Aliases = new List<string>();
			PossibleValues = new List<string>();
		}

		public ArgumentUsageInfo(PropertyInfo toAutoGen)
			: this() {
			Property = toAutoGen;
			Name = "-" + NameTranslators.CombineByPascalCase(toAutoGen.Name, "-").ToLower();

			Aliases.Add("-" + Property.Name);
			if (Aliases.Count == 0) {
				Aliases.Add("-" + Name.ToLower());
			}

			var argAliases = Property.HasAttr<ArgAliasAttribute>() ? Property.Attr<ArgAliasAttribute>().Aliases : null;
			if (argAliases != null) {
				Aliases.AddRange(argAliases.Select(x => "-" + NameTranslators.CombineByPascalCase(x, "-")));
			}

			Description = Property.HasAttr<ArgDescriptionAttribute>()
				? Property.Attr<ArgDescriptionAttribute>().Description
				: "";
			Group = Property.HasAttr<ArgDescriptionAttribute>() ? Property.Attr<ArgDescriptionAttribute>().Group : "";

			if (Property.PropertyType.IsEnum) {
				foreach (var val in toAutoGen.PropertyType.GetFields().Where(v => v.IsSpecialName == false)) {
					var description = val.HasAttr<ArgDescriptionAttribute>()
						? " - " + val.Attr<ArgDescriptionAttribute>().Description
						: "";
					var valText = val.Name;
					PossibleValues.Add(valText + description);
				}
			}
		}
	}

	public static class ArgUsage {
		public static string GetUsage<T>(string exeName = null) {
			return GetStyledUsage<T>(exeName).ToString();
		}

		public static ConsoleString GetStyledUsage<T>(string exeName = null) {
			if (exeName == null) {
				var assembly = Assembly.GetEntryAssembly();
				if (assembly == null) {
					throw new InvalidOperationException(
						"EventStore.Rags could not determine the name of your executable automatically.  This may happen if you run GetUsage<T>() from within unit tests.  Use GetUsageT>(string exeName) in unit tests to avoid this exception.");
				}

				exeName = Path.GetFileNameWithoutExtension(Assembly.GetEntryAssembly().Location);
			}

			ConsoleString ret = new ConsoleString();

			ret += new ConsoleString("Usage: " + exeName, ConsoleColor.Cyan);

			ret.AppendUsingCurrentFormat(" options\n\n");

			ret += GetOptionsUsage(typeof(T).GetProperties(), false);

			ret += "\n";

			return ret;
		}

		private static ConsoleString GetOptionsUsage(IEnumerable<PropertyInfo> opts, bool ignoreActionProperties) {
			if (opts.Count() == 0) {
				return new ConsoleString("There are no options");
			}

			var usageInfos = opts.Select(o => new ArgumentUsageInfo(o));

			List<ConsoleString> columnHeaders = new List<ConsoleString>() {
				new ConsoleString("OPTION", ConsoleColor.Yellow),
				new ConsoleString("DESCRIPTION", ConsoleColor.Yellow),
			};

			List<List<ConsoleString>> rows = new List<List<ConsoleString>>();
			string currentGroup = String.Empty;
			foreach (ArgumentUsageInfo usageInfo in usageInfos.OrderBy(x => x.Group)) {
				if (currentGroup != usageInfo.Group && !String.IsNullOrEmpty(usageInfo.Group)) {
					currentGroup = usageInfo.Group;
					rows.Add(new List<ConsoleString>() {ConsoleString.Empty, ConsoleString.Empty, ConsoleString.Empty});
					rows.Add(new List<ConsoleString>() {
						new ConsoleString(currentGroup),
						ConsoleString.Empty,
						ConsoleString.Empty
					});
				}

				var descriptionString = new ConsoleString(usageInfo.Description);

				var aliases = usageInfo.Aliases.OrderBy(a => a.Length).ToList();
				var maxInlineAliasLength = 8;
				string inlineAliasInfo = "";

				int aliasIndex;
				for (aliasIndex = 0; aliasIndex < aliases.Count; aliasIndex++) {
					var proposedInlineAliases = inlineAliasInfo == string.Empty
						? aliases[aliasIndex]
						: inlineAliasInfo + ", " + aliases[aliasIndex];
					if (proposedInlineAliases.Length <= maxInlineAliasLength) {
						inlineAliasInfo = proposedInlineAliases;
					} else {
						break;
					}
				}

				if (inlineAliasInfo != string.Empty) inlineAliasInfo = " (" + inlineAliasInfo + ")";

				rows.Add(new List<ConsoleString>() {
					new ConsoleString("") + ("-" + usageInfo.Name + inlineAliasInfo),
					descriptionString,
				});

				for (int i = aliasIndex; i < aliases.Count; i++) {
					rows.Add(new List<ConsoleString>() {
						new ConsoleString("  " + aliases[i]),
						ConsoleString.Empty,
					});
				}

				foreach (var possibleValue in usageInfo.PossibleValues) {
					rows.Add(new List<ConsoleString>() {
						ConsoleString.Empty,
						new ConsoleString("  " + possibleValue),
					});
				}
			}

			return FormatAsTable(columnHeaders, rows, "   ");
		}

		private static ConsoleString FormatAsTable(List<ConsoleString> columns, List<List<ConsoleString>> rows,
			string rowPrefix = "") {
			if (Environment.OSVersion.Platform == PlatformID.Win32NT) {
				return FormatAsTableWindows(columns, rows, rowPrefix);
			} else {
				return FormatAsTableUnix(columns, rows, rowPrefix);
			}
		}

		private static ConsoleString FormatAsTableWindows(List<ConsoleString> columns, List<List<ConsoleString>> rows,
			string rowPrefix = "") {
			if (rows.Count == 0) return new ConsoleString();

			Dictionary<int, int> maximums = new Dictionary<int, int>();

			for (int i = 0; i < columns.Count; i++) {
				maximums.Add(i, columns[i].Length);
			}

			for (int i = 0; i < columns.Count; i++) {
				foreach (var row in rows) {
					maximums[i] = Math.Max(maximums[i], row[i].Length);
				}
			}

			ConsoleString ret = new ConsoleString();
			int buffer = 3;

			ret += rowPrefix;
			for (int i = 0; i < columns.Count; i++) {
				var val = columns[i];
				while (val.Length < maximums[i] + buffer) val += " ";
				ret += val;
			}

			ret += "\n";

			foreach (var row in rows) {
				ret += rowPrefix;
				for (int i = 0; i < columns.Count; i++) {
					var val = row[i];
					while (val.Length < maximums[i] + buffer) val += " ";

					ret += val;
				}

				ret += "\n";
			}

			return ret;
		}

		private static ConsoleString FormatAsTableUnix(List<ConsoleString> columns, List<List<ConsoleString>> rows,
			string rowPrefix = "") {
			if (rows.Count == 0) return new ConsoleString();

			Dictionary<int, int> maximums = new Dictionary<int, int>();

			int optionDescriptionWidth = 41;
			int standardColumnWidth = 80;

			List<int> columnWidths = new List<int>();

			for (int i = 0; i < columns.Count; i++) {
				columnWidths.Add(i == 0 ? optionDescriptionWidth : standardColumnWidth - optionDescriptionWidth);
				maximums.Add(i, columns[i].Length);
			}

			for (int i = 0; i < columns.Count; i++) {
				foreach (var row in rows) {
					maximums[i] = columnWidths[i];
				}
			}

			ConsoleString ret = new ConsoleString();
			int buffer = 3;

			ret += rowPrefix;
			for (int i = 0; i < columns.Count; i++) {
				var val = columns[i];
				while (val.Length < maximums[i] + buffer) val += " ";
				ret += val;
			}

			ret += "\n";

			foreach (var row in rows) {
				ret += rowPrefix;
				for (int i = 0; i < columns.Count; i++) {
					var val = row[i];
					while (val.Length < maximums[i] + buffer) val += " ";

					ret += val;
				}

				ret += "\n";
			}

			return ret;
		}
	}
}
