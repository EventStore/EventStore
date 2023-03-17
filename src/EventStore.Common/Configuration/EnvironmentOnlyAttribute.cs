using System;

namespace EventStore.Common.Configuration {
	[AttributeUsage(AttributeTargets.Property)]
	public class EnvironmentOnlyAttribute : Attribute {
		public string Message { get; }

		public EnvironmentOnlyAttribute(string message) {
			Message = message;
		}
	}
}

