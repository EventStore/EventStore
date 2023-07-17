using System;

namespace EventStore.Common.Configuration;

[AttributeUsage(AttributeTargets.Property)]
public class UnitAttribute(string unit) : Attribute {
	public string Unit { get; } = unit;
}



