using System;

namespace EventStore.Common.Configuration {
	[AttributeUsage(AttributeTargets.Property)]
	public class SensitiveAttribute : Attribute;
}
