// ReSharper disable CheckNamespace

#nullable enable

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using EventStore.Core.Configuration.Sources;

namespace EventStore.Core;

public record SectionMetadata(
	string Key,
	string SectionName,
	string Description,
	Type SectionType,
	Dictionary<string, OptionMetadata> Options,
	int Sequence) {
	public static SectionMetadata FromPropertyInfo(PropertyInfo property, int sequence) {
		var name = property.Name.Replace("Options", String.Empty);
		var key = EventStoreConfigurationKeys.Normalize(name);
		var description = property.PropertyType.GetCustomAttribute<DescriptionAttribute>()?.Description ??
		                  String.Empty;

		var options = property.PropertyType.GetProperties()
			.Select(OptionMetadata.FromPropertyInfo)
			.ToDictionary(option => option.Key, x => x);

		return new(
			Key: key,
			SectionName: name,
			Description: description,
			SectionType: property.PropertyType,
			Options: options,
			Sequence: sequence
		);
	}
}
