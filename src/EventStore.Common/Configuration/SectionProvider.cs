// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;

namespace EventStore.Common.Configuration;

public sealed class SectionProvider : ConfigurationProvider, IDisposable {
	private readonly IConfigurationRoot _configuration;
	private readonly string _sectionName;
	private readonly IDisposable _registration;

	public SectionProvider(string sectionName, IConfigurationRoot configuration) {
		_configuration = configuration;
		_sectionName = sectionName;
		_registration = ChangeToken.OnChange(
			configuration.GetReloadToken,
			Load);
	}

	public IEnumerable<IConfigurationProvider> Providers => _configuration.Providers;

	public void Dispose() {
		_registration.Dispose();
	}

	public override void Load() {
		var data = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		foreach (var kvp in _configuration.AsEnumerable()) {
			data[_sectionName + ":" + kvp.Key] = kvp.Value;
		}
		Data = data;
		OnReload();
	}
}
