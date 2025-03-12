// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using EventStore.Security.EncryptionAtRest.MasterKeySources;
using EventStore.Security.EncryptionAtRest.MasterKeySources.File;
using Serilog;

namespace EventStore.Security.EncryptionAtRest.MasterKeySourceConfigurators;

public class FileConfigurator : IMasterKeySourceConfigurator<EncryptionAtRestOptions.FileConfiguratorOptions> {
	private static readonly ILogger _logger = Log.ForContext<FileConfigurator>();
	public string Name => "File";

	public void Configure(EncryptionAtRestOptions.FileConfiguratorOptions options, out IMasterKeySource masterKeySource) {
		if (string.IsNullOrWhiteSpace(options.KeyPath))
			LogErrorAndThrow($"Encryption-At-Rest: ({Name}) '{nameof(options.KeyPath)}' not specified.");

		try {
			masterKeySource = new FileSource(options.KeyPath);
		} catch (Exception exc) {
			_logger.Error(exc, $"Encryption-At-Rest: ({Name}) Error while configuring master key source.");
			throw;
		}
	}

	private static void LogErrorAndThrow(string error) {
		_logger.Error(error);
		throw new Exception(error);
	}
}
