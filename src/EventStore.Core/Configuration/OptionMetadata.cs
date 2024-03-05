// ReSharper disable CheckNamespace

#nullable enable

using System;
using System.ComponentModel;
using System.Reflection;
using EventStore.Common.Configuration;
using EventStore.Core.Configuration.Sources;

namespace EventStore.Core;

public record OptionMetadata(
	string Key,
	string Name,
	string Description,
	string SectionName,
	string[] AllowedValues,
	bool IsSensitive,
	bool IsEnvironmentOnly,
	string? EnvironmentOnlyMessage,
	int Sequence) {

	public static OptionMetadata FromPropertyInfo(PropertyInfo property, int sequence) {
		var sectionName = property.DeclaringType?.Name.Replace("Options", "") ?? "";
		var key = EventStoreConfigurationKeys.Normalize(property.Name);

		var description = property.GetCustomAttribute<DescriptionAttribute>()?.Description ?? "";
		var isSensitive = property.GetCustomAttribute<SensitiveAttribute>() != null;

		var environmentOnlyAttribute = property.GetCustomAttribute<EnvironmentOnlyAttribute>();
		var isEnvironmentOnly = environmentOnlyAttribute != null;
		var environmentOnlyMessage = environmentOnlyAttribute?.Message;

		string[] allowedValues = property.PropertyType.IsEnum
			? property.PropertyType.GetEnumNames()
			: [];

		return new(
			Key: key,
			Name: property.Name,
			Description: description,
			SectionName: sectionName,
			AllowedValues: allowedValues,
			IsSensitive: isSensitive,
			IsEnvironmentOnly: isEnvironmentOnly,
			EnvironmentOnlyMessage: environmentOnlyMessage,
			Sequence: sequence
		);
	}
}
