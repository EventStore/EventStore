using System;

namespace EventStore.Common.Configuration;

public record PrintableOption {
	public readonly string Name;
	public readonly string Description;
	public readonly string Group;
	public readonly string[] AllowedValues;
	public readonly string Value;
	public readonly Type Source;

	private readonly bool _isSensitive;

	public PrintableOption(
		string name,
		string description,
		string group,
		string[] allowedValues,
		bool isSensitive,
		string value = null,
		Type source = null) {
		Name = name;
		Description = description;
		Group = group;
		AllowedValues = allowedValues;
		_isSensitive = isSensitive;
		Value = isSensitive ? new string('*', 8) : value ?? string.Empty;
		Source = source;
	}

	public PrintableOption WithValue(string value, Type source) =>
		new(Name, Description, Group, AllowedValues, _isSensitive, value, source);
}
