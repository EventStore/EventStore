// ReSharper disable CheckNamespace

#nullable enable

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;

namespace EventStore.Core;

public record SectionMetadata(
	string SectionName,
	string Description,
	Type SectionType,
	Dictionary<string, OptionMetadata> Options,
	int Sequence) {

	public static SectionMetadata FromPropertyInfo(PropertyInfo property, int sequence) {
		var description = property.PropertyType.GetCustomAttribute<DescriptionAttribute>()?.Description ?? "";

		var options = property.PropertyType.GetProperties()
			.Select(OptionMetadata.FromPropertyInfo)
			.ToDictionary(option => option.Key, x => x);

		return new(
			SectionName: property.Name,
			Description: description,
			SectionType: property.PropertyType,
			Options: options,
			Sequence: sequence
		);
	}
}
