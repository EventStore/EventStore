// ReSharper disable CheckNamespace

#nullable enable

namespace EventStore.Core;

public record LoadedOption {
	public OptionMetadata Metadata { get; }
	public string Title { get; }
	public string DisplayValue { get; }
	public string SourceDisplayName { get; }
	public bool IsDefault { get; }

	public LoadedOption(
		OptionMetadata metadata,
		string title,
		string? value,
		string sourceDisplayName,
		bool isDefault) {

		Metadata = metadata;
		Title = title;
		DisplayValue = metadata.IsSensitive ? new('*', 8) : value ?? string.Empty;
		SourceDisplayName = sourceDisplayName;
		IsDefault = isDefault;
	}

	public override string ToString() => DisplayValue;
}
