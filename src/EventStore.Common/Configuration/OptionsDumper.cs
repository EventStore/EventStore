using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace EventStore.Common.Configuration {
	public class OptionsDumper {
		private readonly IEnumerable<Type> _optionSections;
		private readonly List<string> _sortOrder;

		public OptionsDumper(IEnumerable<Type> optionSections) {
			_optionSections = optionSections;
			_sortOrder = _optionSections.SelectMany(optionSection =>
					optionSection.GetProperties()
						.Select(p => NameTranslators.CombineByPascalCase(p.Name, " ").ToUpper()))
				.OrderBy(x => x)
				.ToList();
		}

		public string Dump(IConfigurationRoot configurationRoot) {
			var info = GetOptionSourceInfo(configurationRoot);

			var @default = info.Where(x => x.Value.source == typeof(Default));
			var modified = info.Where(x => x.Value.source != typeof(Default));

			var nameColumnWidth = info.Keys.Select(x => x.Length).Max() + 7;

			return PrintOptions(nameof(modified), modified) + Environment.NewLine +
			       PrintOptions(nameof(@default), @default);

			string PrintOptions(string name,
				IEnumerable<KeyValuePair<string, (Type source, string value)>> options) =>
				options.OrderBy(pair => _sortOrder.IndexOf(pair.Key)).Aggregate(
					new StringBuilder().Append($"{name.ToUpper()} OPTIONS:").AppendLine().AppendLine(),
					(builder, pair) => builder
						.Append($"     {pair.Key}:".PadRight(nameColumnWidth, ' '))
						.Append(pair.Value.value).Append(' ').Append(FormatSourceName(pair.Value.source))
						.AppendLine()).ToString();

			static string FormatSourceName(Type source) =>
				$"({(source == typeof(Default) ? "<DEFAULT>" : NameTranslators.CombineByPascalCase(source.Name, " "))})"
			;
		}

		private IDictionary<string, (Type source, string value)> GetOptionSourceInfo(
			IConfigurationRoot configurationRoot) {
			var sensitiveOptions = _optionSections.SelectMany(section => section.GetProperties())
				.Where(option => option.GetCustomAttribute<SensitiveAttribute>() != null)
				.Select(option => option.Name)
				.ToArray();
			var values = new Dictionary<string, (Type source, string value)>();

			foreach (var provider in configurationRoot.Providers) {
				var source = provider.GetType();
				foreach (var key in provider.GetChildKeys(Enumerable.Empty<string>(), default)) {
					if (provider.TryGet(key, out var value)) {
						values[NameTranslators.CombineByPascalCase(key, " ").ToUpper()] =
							(source, FormatValue(key, value));
					}
				}
			}

			return values;

			string FormatValue(string key, string value) =>
				string.IsNullOrEmpty(value) ? "<empty>" : sensitiveOptions.Contains(key) ? "****" : value;
		}

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

	}
}
