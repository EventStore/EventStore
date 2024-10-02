// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using Microsoft.Extensions.Configuration;

namespace EventStore.Common.Configuration;

public class SectionSource : IConfigurationSource {
	private readonly string _sectionName;
	private readonly Action<IConfigurationBuilder> _configure;

	public SectionSource(string sectionName, Action<IConfigurationBuilder> configure) {
		_sectionName = sectionName;
		_configure = configure;
	}

	public IConfigurationProvider Build(IConfigurationBuilder builder) {
		var subBuilder = new ConfigurationBuilder();
		_configure(subBuilder);
		var configuration = subBuilder.Build();

		return new SectionProvider(_sectionName, configuration);
	}
}

public static class SectionConfigurationExtensions {
	// Allows configuration to be mounted inside a specified section
	public static IConfigurationBuilder AddSection(this IConfigurationBuilder self, string sectionName,
		Action<IConfigurationBuilder> configure) =>
		self.Add(new SectionSource(sectionName, configure));
}
