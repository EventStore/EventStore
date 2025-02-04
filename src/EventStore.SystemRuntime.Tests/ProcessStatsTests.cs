// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Diagnostics;
using System.IO;
using System.Runtime;
using Xunit;

namespace EventStore.SystemRuntime.Tests;

public sealed class ProcessStatsTests : IDisposable {
	private readonly DirectoryInfo _directory;

	public ProcessStatsTests() {
		string directoryPath = Path.Combine(Path.GetTempPath(), string.Format("ESX-{0}-{1}", Guid.NewGuid(), nameof(ProcessStatsTests)));
		_directory = Directory.CreateDirectory(directoryPath);
		File.WriteAllText(Path.Combine(directoryPath, "file.txt"), "the data");
	}

	public void Dispose() {
		_directory.Delete(recursive: true);
	}

	[Fact]
	public void TestProcessStats() {
		var diskIo = ProcessStats.GetDiskIo();

		Assert.True(diskIo.ReadBytes > 0);
		Assert.True(diskIo.WrittenBytes > 0);

		if (RuntimeInformation.OsPlatform != RuntimeOSPlatform.OSX) {
			// ops not supported on OSX
			Assert.True(diskIo.ReadOps > 0);
			Assert.True(diskIo.WriteOps > 0);
		}
	}
}
