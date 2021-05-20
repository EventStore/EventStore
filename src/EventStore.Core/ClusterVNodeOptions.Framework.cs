using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using EventStore.Common.Configuration;
using EventStore.Common.Utils;
using Microsoft.Extensions.Configuration;

#nullable enable
namespace EventStore.Core {
	public partial record ClusterVNodeOptions {
		private static readonly IEnumerable<Type> OptionSections;
		public static readonly string HelpText;

		public string GetComponentName() => $"{Interface.ExtIp}-{Interface.HttpPort}-cluster-node";

		static ClusterVNodeOptions() {
			OptionSections = typeof(ClusterVNodeOptions)
				.GetProperties(BindingFlags.Public | BindingFlags.Instance)
				.Where(p => p.GetCustomAttribute<OptionGroupAttribute>() != null)
				.Select(p => p.PropertyType);
			HelpText = GetHelpText();
		}

		public static IEnumerable<KeyValuePair<string, object?>> DefaultValues =>
			OptionSections.SelectMany(GetDefaultValues);

		private static IEnumerable<KeyValuePair<string, object?>> GetDefaultValues(Type type) {
			var defaultInstance = Activator.CreateInstance(type)!;

			return type.GetProperties().Select(property =>
				new KeyValuePair<string, object?>(property.Name, property.PropertyType switch {
					{IsArray: true} => string.Join(",",
						((Array)(property.GetValue(defaultInstance) ?? Array.Empty<object>())).OfType<object>()),
					_ => property.GetValue(defaultInstance)
				}));
		}

		public string? DumpOptions() =>
			ConfigurationRoot == null ? null : new OptionsDumper(OptionSections).Dump(ConfigurationRoot);

		public string? GetDeprecationWarnings() {
			var defaultValues = new Dictionary<string, object?>(DefaultValues, StringComparer.OrdinalIgnoreCase);

			var deprecationWarnings = from section in OptionSections
				from option in section.GetProperties()
				let deprecationWarning = option.GetCustomAttribute<DeprecatedAttribute>()?.Message
				where deprecationWarning is not null
				let value = ConfigurationRoot.GetValue<string?>(option.Name)
				where defaultValues.TryGetValue(option.Name, out var defaultValue)
				      && !string.Equals(value, defaultValue?.ToString(), StringComparison.OrdinalIgnoreCase)
				      select deprecationWarning;

			var builder = deprecationWarnings
				.Aggregate(new StringBuilder(), (builder, deprecationWarning) => builder.AppendLine(deprecationWarning));

			return builder.Length != 0 ? builder.ToString() : null;
		}

		private static EndPoint ParseEndPoint(string val) {
			var parts = val.Split(':', 2);
			return IPAddress.TryParse(parts[0], out var ip)
				? new IPEndPoint(ip, int.Parse(parts[1]))
				: new DnsEndPoint(parts[0], int.Parse(parts[1]));
		}

		private static string GetHelpText() {
			const string OPTION = nameof(OPTION);
			const string DESCRIPTION = nameof(DESCRIPTION);
			const string DEFAULT = nameof(DEFAULT);

			var optionColumnWidth = Options().Max(o =>
				OptionHeaderColumnWidth(o.Name, DefaultValue(o)));

			var header = $"{OPTION.PadRight(optionColumnWidth, ' ')}{DESCRIPTION}";

			var optionGroups = Options().GroupBy(option =>
				option.DeclaringType?.GetCustomAttribute<DescriptionAttribute>()?.Description ?? string.Empty);

			return optionGroups
				.Aggregate(new StringBuilder().Append(header).AppendLine(), (builder, optionGroup) =>
					optionGroup.Aggregate(
						builder.AppendLine().Append(optionGroup.Key).AppendLine(),
						(stringBuilder, property) => stringBuilder.Append(Line(property)).AppendLine()))
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
				if (Runtime.IsWindows) {
					builder.Append('-').Append(property.Name);
				} else {
					builder.AppendJoin(string.Empty, GnuOption(property.Name));
				}

				var defaultValue = DefaultValue(property);

				if (defaultValue != string.Empty) {
					builder.Append('=').Append(defaultValue);
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
		}

		[AttributeUsage(AttributeTargets.Property)]
		internal class OptionGroupAttribute : Attribute{}
	}
}
