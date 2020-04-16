using System;

namespace EventStore.Common.Configuration {
	[AttributeUsage(AttributeTargets.Property)]
	public class DeprecatedAttribute : Attribute {
		public string Message { get; }

		public DeprecatedAttribute(string message) {
			Message = message;
		}
	}
}
