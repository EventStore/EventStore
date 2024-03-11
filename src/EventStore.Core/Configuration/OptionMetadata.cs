// ReSharper disable CheckNamespace

#nullable enable

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
	string[] AllowedValues,
	bool IsSensitive,
	bool IsEnvironmentOnly,
	string? ErrorMessage,
	SectionMetadata SectionMetadata,
	int Sequence) {

	public static OptionMetadata FromPropertyInfo(SectionMetadata sectionMetadata, PropertyInfo property, int sequence) {
		var sectionName = property.DeclaringType?.Name.Replace("Options", "") ?? "";
		var key         = EventStoreConfigurationKeys.Normalize(property.Name);
		var fullKey     = $"{EventStoreConfigurationKeys.Prefix}:{sectionName}:{EventStoreConfigurationKeys.StripConfigurationPrefix(property.Name)}";
		
		var description = property.GetCustomAttribute<DescriptionAttribute>()?.Description ?? "";
		var isSensitive = property.GetCustomAttribute<SensitiveAttribute>() != null;

		var envOnlyAttribute  = property.GetCustomAttribute<EnvironmentOnlyAttribute>();
		var isEnvironmentOnly = envOnlyAttribute != null;
		var errorMessage      = envOnlyAttribute?.Message;

		var allowedValues = property.PropertyType.IsEnum
			? property.PropertyType.GetEnumNames()
			: [];

		return new(
			Key: key,
			FullKey: fullKey,
			Name: property.Name,
			Description: description,
			AllowedValues: allowedValues,
			IsSensitive: isSensitive,
			IsEnvironmentOnly: isEnvironmentOnly,
			ErrorMessage: errorMessage,
			SectionMetadata: sectionMetadata,
			Sequence: sequence
		);
	}
}
