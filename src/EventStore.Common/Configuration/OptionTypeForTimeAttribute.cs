using System;

namespace EventStore.Common.Configuration;

[AttributeUsage(AttributeTargets.Property)]
public class OptionTypeForTimeAttribute : Attribute {
	public string Message { get; }

	public OptionTypeForTimeAttribute(string message) {
		Message = message;
	}
}



