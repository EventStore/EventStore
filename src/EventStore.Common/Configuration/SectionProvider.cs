// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using Microsoft.Extensions.Configuration;

namespace EventStore.Common.Configuration;

public class SectionProvider : ConfigurationProvider {
	private readonly IConfiguration _configuration;
	private readonly string _sectionName;

	public SectionProvider(string sectionName, IConfiguration configuration) {
		_configuration = configuration;
		_sectionName = sectionName;
	}

	public override void Load() {
		foreach (var kvp in _configuration.AsEnumerable()) {
			Data[_sectionName + ":" + kvp.Key] = kvp.Value;
		}
	}
}
