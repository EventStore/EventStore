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

			ret += new ConsoleString("Usage: " + exeName + " [arguments]");

			ret += Environment.NewLine;

			ret += GetOptionsUsage(typeof(T).GetProperties(), false);

			ret += Environment.NewLine;

			return ret;
		}

		private static ConsoleString GetOptionsUsage(IEnumerable<PropertyInfo> opts, bool ignoreActionProperties) {
			if (!opts.Any()) {
				return new ConsoleString("There are no options");
			}

			var usageInfos = opts.Select(o => new ArgumentUsageInfo(o));

			var ret = new ConsoleString();

			var currentGroup = string.Empty;

			var longestArg = usageInfos.Select(u => u.Name)
				.Aggregate("", (max, cur) => max.Length > cur.Length ? max : cur);

			foreach (var usageInfo in usageInfos.OrderBy(x => x.Group)) {
				if (currentGroup != usageInfo.Group && !string.IsNullOrEmpty(usageInfo.Group)) {
					currentGroup = usageInfo.Group;
					ret += $"{Environment.NewLine}{currentGroup}:{Environment.NewLine}";
				}

				ret += string.Format("  -{0,-" + (longestArg.Length + 2) + "}{1}", usageInfo.Name,
					usageInfo.Description);

				if (usageInfo.PossibleValues.Any()) {
					ret += $" ({string.Join(", ", usageInfo.PossibleValues)})";
				}

				ret += Environment.NewLine;
			}

			return ret;
		}
	}
}
