// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Collections.Generic;
using EventStore.Core.Services.Archive.Archiver;
using EventStore.Core.Services.Archive.Storage;
using EventStore.Core.Services.Archive.Archiver.Unmerger;
using EventStore.Core.TransactionLog.FileNamingStrategy;
using EventStore.Plugins;
using EventStore.Plugins.Licensing;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EventStore.Core.Services.Archive;

public class ArchivePlugableComponent : IPlugableComponent {
	public string Name => "Archiver";

	public string DiagnosticsName => Name;

	public KeyValuePair<string, object>[] DiagnosticsTags => [];

	public string Version => "0.0.1";

	public bool Enabled { get; private set; }

	public string LicensePublicKey => LicenseConstants.LicensePublicKey;

	private readonly bool _isArchiver;

	public ArchivePlugableComponent(bool isArchiver) {
		_isArchiver = isArchiver;
	}

	public void ConfigureApplication(IApplicationBuilder builder, IConfiguration configuration) {
		if (!Enabled)
			return;

		_ = builder.ApplicationServices.GetService<ArchiverService>();

		var licenseService = builder.ApplicationServices.GetRequiredService<ILicenseService>();

		_ = LicenseMonitor.MonitorAsync(
			featureName: Name,
			requiredEntitlements: ["ARCHIVE"],
			licenseService: licenseService,
			onLicenseException: licenseService.RejectLicense,
			logger: builder.ApplicationServices.GetRequiredService<ILoggerFactory>().CreateLogger(GetType()));
	}

	public void ConfigureServices(IServiceCollection services, IConfiguration configuration) {
		var options = configuration.GetSection("EventStore:Archive").Get<ArchiveOptions>();
		Enabled = options?.Enabled ?? false; // disabled by default

		if (options is null || !Enabled || options.StorageType is StorageType.None)
			return;

		services.AddSingleton(options);
		services.AddScoped<IArchiveStorageFactory, ArchiveStorageFactory>();
		services.Decorate<IReadOnlyList<IClusterVNodeStartupTask>>(AddArchiveCatchupTask);

		if (_isArchiver) {
			services.AddSingleton<IChunkUnmerger, ChunkUnmerger>();
			services.AddSingleton<ArchiverService>();
		}
	}

	private static IReadOnlyList<IClusterVNodeStartupTask> AddArchiveCatchupTask(
		IReadOnlyList<IClusterVNodeStartupTask> startupTasks,
		IServiceProvider serviceProvider) {

		var newStartupTasks = new List<IClusterVNodeStartupTask>();
		if (startupTasks != null)
			newStartupTasks.AddRange(startupTasks);

		var standardComponents = serviceProvider.GetRequiredService<StandardComponents>();
		newStartupTasks.Add(new ArchiveCatchup.ArchiveCatchup(
			dbPath: standardComponents.DbConfig.Path,
			writerCheckpoint: standardComponents.DbConfig.WriterCheckpoint,
			replicationCheckpoint: standardComponents.DbConfig.ReplicationCheckpoint,
			chunkSize: standardComponents.DbConfig.ChunkSize,
			serviceProvider.GetRequiredService<IVersionedFileNamingStrategy>(),
			serviceProvider.GetRequiredService<IArchiveStorageFactory>()));

		return newStartupTasks;
	}
}
