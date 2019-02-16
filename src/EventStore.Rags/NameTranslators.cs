namespace EventStore.Rags {
	public static class NameTranslators {
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
