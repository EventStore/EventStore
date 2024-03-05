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
	string FullKey,
	string Name,
	string Description,
	string SectionName,
	string[] AllowedValues,
	bool IsSensitive,
	bool IsEnvironmentOnly,
	string? ErrorMessage,
	int Sequence) {
	public override string ToString() => FullKey;

	public static OptionMetadata FromPropertyInfo(PropertyInfo property, int sequence) {
		var sectionName = property.DeclaringType?.Name.Replace("Options", "") ?? String.Empty;
		var key = EventStoreConfigurationKeys.Normalize(property.Name);
		var fullKey =
			$"EventStore:{sectionName}:{EventStoreConfigurationKeys.StripConfigurationPrefix(property.Name)}";

		var description = property.GetCustomAttribute<DescriptionAttribute>()?.Description ?? String.Empty;
		var isSensitive = property.GetCustomAttribute<SensitiveAttribute>() != null;

		var environmentOnlyAttribute = property.GetCustomAttribute<EnvironmentOnlyAttribute>();
		var isEnvironmentOnly = environmentOnlyAttribute != null;
		var errorMessage = environmentOnlyAttribute?.Message;

		string[] allowedValues = property.PropertyType.IsEnum
			? property.PropertyType.GetEnumNames()
			: [];

		return new(
			Key: key,
			FullKey: fullKey,
			Name: property.Name,
			Description: description,
			SectionName: sectionName,
			AllowedValues: allowedValues,
			IsSensitive: isSensitive,
			IsEnvironmentOnly: isEnvironmentOnly,
			ErrorMessage: errorMessage,
			Sequence: sequence
		);
	}
}
