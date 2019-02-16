using System;

namespace EventStore.Rags {
	public class OptionException : Exception {
		private readonly string option;

		public OptionException() {
		}

		public OptionException(string message, string optionName)
			: base(message) {
			option = optionName;
		}

		public OptionException(string message, string optionName, Exception innerException)
			: base(message, innerException) {
			option = optionName;
		}

		public string OptionName {
			get { return option; }
		}
	}
}
