// Copyright (c) Kurrent, Inc and/or licensed to Kurrent, Inc under one or more agreements.
// Kurrent, Inc licenses this file to you under the Kurrent License v1 (see LICENSE.md).

using System.ComponentModel;
using Kurrent.Surge;
using Kurrent.Surge.Connectors;
using Kurrent.Surge.Connectors.Sinks;

namespace EventStore.Streaming.Connectors.Sinks;

[PublicAPI]
public record SourceOptions : IConnectorOptions {
	static SourceOptions() {
		// *******************************************************************************************************************
		// Warning! Using TypeConverterAttribute(typeof(LogPosition)) will not work
		// *******************************************************************************************************************
		//
		// The root of all evil is a flaw in the TypeDescriptorAttribute implementation.
		// The attribute has two constructor overloads, one for a plaintext type specification
		// (which is — not unexpectedly — pure magic at runtime) and one for an early-bound typeof() reference.
		// If you use the second path, what could possibly go wrong? Well, actually the attribute only uses the first path.
		// The real and correct runtime type reference is flattened into plaintext, and here there be dragons.
		// So it's no use writing a typeof() — it's always the plaintext-and-magic scenario inside.
		//
		// source: https://stackoverflow.com/a/18450516/503085
		// *******************************************************************************************************************

		TypeDescriptor.AddAttributes(
			typeof(LogPosition),
			new TypeConverterAttribute(typeof(LogPositionConverter).FullName!)
		);
	}

	public string InstanceTypeName { get; init; } = "";
	public LoggingOptions Logging { get; init; } = new();
}
