// ReSharper disable CheckNamespace

#nullable enable

namespace EventStore.Core;

public record LoadedOption(
	OptionMetadata Metadata,
	string Title,
	string? Value,
	string SourceDisplayName,
	bool IsDefault) {
	public string DisplayValue { get; } = Metadata.IsSensitive ? new('*', 8) : Value ?? string.Empty;

	public override string ToString() => DisplayValue;
}
