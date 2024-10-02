// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

// ReSharper disable CheckNamespace

#nullable enable

namespace EventStore.Core;

public record LoadedOption {
	public LoadedOption(
		OptionMetadata metadata,
		string title,
		string? value,
		string sourceDisplayName,
		bool isDefault) {
		RawValue = value;
		Metadata = metadata;
		Title = title;
		DisplayValue = metadata.IsSensitive ? new('*', 8) : value ?? string.Empty;
		SourceDisplayName = sourceDisplayName;
		IsDefault = isDefault;
	}

	string? RawValue { get; }

	public OptionMetadata Metadata { get; }
	public string Title { get; }
	public string DisplayValue { get; }
	public string SourceDisplayName { get; }
	public bool IsDefault { get; }

	/// <summary>
	/// Returns the raw value of the option. Be aware that this value may be sensitive.
	/// </summary>
	public string? GetRawValue() => RawValue;

	public override string ToString() => DisplayValue;
}
