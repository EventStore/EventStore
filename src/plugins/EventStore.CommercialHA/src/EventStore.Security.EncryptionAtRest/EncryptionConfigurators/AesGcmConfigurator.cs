// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Collections.Generic;
using EventStore.Plugins.Transforms;
using EventStore.Security.EncryptionAtRest.Transforms.AesGcm;
using Serilog;

namespace EventStore.Security.EncryptionAtRest.EncryptionConfigurators;

public class AesGcmConfigurator : IEncryptionConfigurator<EncryptionAtRestOptions.AesGcmOptions> {
	private static readonly ILogger _logger = Log.ForContext<AesGcmConfigurator>();
	public string Name => "AesGcm";

	public void Configure(EncryptionAtRestOptions.AesGcmOptions options, IReadOnlyList<MasterKey> masterKeys, out IDbTransform encryptionTransform) {
		_logger.Information($"Encryption-At-Rest: ({Name}) Using key size: {options.KeySize} bits");

		try {
			encryptionTransform = new AesGcmDbTransform(masterKeys, options.KeySize);
		} catch (Exception exc) {
			_logger.Error(exc, $"Encryption-At-Rest: ({Name}) Error while creating encryption transform.");
			throw;
		}
	}
}
