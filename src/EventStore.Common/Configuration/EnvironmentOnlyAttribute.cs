using System;

namespace EventStore.Common.Configuration;

[AttributeUsage(AttributeTargets.Property)]
public class EnvironmentOnlyAttribute(string message) : Attribute {
	public string Message { get; } = message;
}
