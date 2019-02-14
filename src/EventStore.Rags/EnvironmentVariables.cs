using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace EventStore.Rags {
	public static class EnvironmentVariables {
		static readonly Regex _regex = new Regex(@"^\$\{env\:(\w+)\}$");

		public static IEnumerable<OptionSource> Parse<TOptions>(Func<string, string> nameTranslator)
			where TOptions : class {
			return
				(from property in typeof(TOptions).GetProperties()
					let environmentVariableName = nameTranslator(property.Name)
					let environmentVariableValue = Environment.GetEnvironmentVariable(environmentVariableName.ToUpper())
					where !String.IsNullOrEmpty(environmentVariableValue)
					select ParseReferenceEnvironmentVariable(
						OptionSource.String("Environment Variable", property.Name, environmentVariableValue,
							_regex.IsMatch(environmentVariableValue)
						)));
		}

		public static OptionSource ParseReferenceEnvironmentVariable(OptionSource source) {
			if (!source.IsReference) return source;
			source.Value = Environment.GetEnvironmentVariable(_regex.Match(source.Value.ToString()).Groups[1].Value);
			return source;
		}
	}
}
