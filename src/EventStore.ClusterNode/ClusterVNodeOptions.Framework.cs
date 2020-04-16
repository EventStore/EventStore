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

		internal IReadOnlyList<ISubsystem> Subsystems { get; private init; } = Array.Empty<ISubsystem>();

		public string GetComponentName() => $"{Interface.ExtIp}-{Interface.HttpPort}-cluster-node";

		static ClusterVNodeOptions() {
			OptionSections = typeof(ClusterVNodeOptions)
				.GetProperties(BindingFlags.Public | BindingFlags.Instance)
				.Select(p => p.PropertyType);
			HelpText = GetHelpText();
		}

		public static IEnumerable<KeyValuePair<string, object?>> DefaultValues =>
			OptionSections.SelectMany(GetDefaultValues);

		private static IEnumerable<KeyValuePair<string, object?>> GetDefaultValues(Type type) {
			var defaultInstance = Activator.CreateInstance(type)!;

			return type.GetProperties().Select(property =>
				new KeyValuePair<string, object?>(property.Name, GetDefaultValue(property)));

			object? GetDefaultValue(PropertyInfo property) => property.PropertyType switch {
				{IsArray: true} => string.Join(",", ((Array)property.GetValue(defaultInstance)!).OfType<object>()),
				_ => property.GetValue(defaultInstance)
			};
		}

		public ClusterVNodeOptions WithSubsystem(ISubsystem subsystem) => this with {
			Subsystems = new List<ISubsystem>(Subsystems) {subsystem}
		};

		public string? DumpOptions() =>
			ConfigurationRoot == null ? null : new OptionsDumper(OptionSections).Dump(ConfigurationRoot);

		public string? GetDeprecationWarnings() {
			var defaultValues = new Dictionary<string, object?>(DefaultValues, StringComparer.OrdinalIgnoreCase);
			var builder = OptionSections.SelectMany(section => section.GetProperties())
				.Where(option => option.GetCustomAttribute<DeprecatedAttribute>() != null)
				.Select(option => new {
					name = option.Name,
					configured = ConfigurationRoot.AsEnumerable().Any(x =>
						string.Equals(x.Key, option.Name, StringComparison.OrdinalIgnoreCase)),
					value = ConfigurationRoot.GetValue<object?>(option.Name),
					deprecationWarning = option.GetCustomAttribute<DeprecatedAttribute>()!.Message
				})
				.Where(_ => _.configured && defaultValues.TryGetValue(_.name, out var defaultValue) &&
				            _.value != defaultValue)
				.Aggregate(new StringBuilder(), (builder, _) => builder.AppendLine(_.deprecationWarning));

			return builder.Length != 0 ? builder.ToString() : null;
		}

		private  static EndPoint ParseEndPoint(string val) {
			var parts = val.Split(':', 2);
			return IPAddress.TryParse(parts[0], out var ip)
				? new IPEndPoint(ip, int.Parse(parts[1]))
				: new DnsEndPoint(parts[0], int.Parse(parts[1]));
		}

		private static string GetHelpText() {
			const string OPTION = nameof(OPTION);
			const string DESCRIPTION = nameof(DESCRIPTION);
			const string DEFAULT = nameof(DEFAULT);

			var optionColumnWidth = OptionNames().Max(OptionHeaderColumnWidth);
			var defaultColumnWidth = Options().Select(DefaultValue).Max(DefaultValueColumnWidth);

			var header =
				$"{OPTION.PadRight(optionColumnWidth, ' ')}{DEFAULT.PadRight(defaultColumnWidth, ' ')}{DESCRIPTION}";

			var optionGroups = Options().GroupBy(option =>
				option.DeclaringType?.GetCustomAttribute<DescriptionAttribute>()?.Description ?? string.Empty);

			return optionGroups
				.Aggregate(new StringBuilder().Append(header).AppendLine(), (builder, optionGroup) =>
					optionGroup.Aggregate(
						builder.AppendLine().Append(optionGroup.Key).AppendLine(),
						(stringBuilder, property) => stringBuilder.Append(Line(property)).AppendLine()))
				.ToString();

			string Line(PropertyInfo property) {
				var gnuOption = new string(GnuOption(property.Name).ToArray());
				var defaultValue = DefaultValue(property);
				var description = property.GetCustomAttribute<DescriptionAttribute>()?.Description;
				if (property.PropertyType.IsEnum) {
					description += $" ({string.Join(", ", Enum.GetNames(property.PropertyType))})";
				}

				return Runtime.IsWindows
					? Environment.NewLine + $" -{property.Name}"
					: $"{gnuOption.PadRight(optionColumnWidth, ' ')}{defaultValue.PadRight(defaultColumnWidth, ' ')}{description}";
			}

			static IEnumerable<PropertyInfo> Options() => OptionSections.SelectMany(type => type.GetProperties());

			static IEnumerable<string> OptionNames() => Options().Select(p => p.Name);

			static int OptionWidth(string x) => x.Count(char.IsUpper) + 1 + x.Length;

			int OptionHeaderColumnWidth(string x) => Math.Max(OptionWidth(x) + 1, OPTION.Length);

			static string DefaultValue(PropertyInfo option) =>
				option.GetCustomAttribute<DefaultValueAttribute>()?.Value?.ToString() ?? string.Empty;

			int DefaultValueColumnWidth(string x) => Math.Max(x?.Length + 1 ?? 0, DEFAULT.Length);

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
	}
}
