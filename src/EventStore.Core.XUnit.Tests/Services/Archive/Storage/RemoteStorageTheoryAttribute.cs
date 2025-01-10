// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Diagnostics;
using System.IO;
using Xunit;

namespace EventStore.Core.XUnit.Tests.Services.Archive.Storage;

public sealed class RemoteStorageTheoryAttribute : TheoryAttribute {
	public RemoteStorageTheoryAttribute() {
		Skip = "Cloud-based storage tests are disabled. You can enable them with RUN_S3_TESTS conditional symbol";
		CheckPrerequisites();
	}

	[Conditional("RUN_S3_TESTS")]
	[Conditional("RUN_GCP_TESTS")]
	private void CheckPrerequisites() {
		CheckAwsCliPrerequisites();
		CheckGcpPrerequisites();
		Skip = null;
	}

	[Conditional("RUN_GCP_TESTS")]
	private static void CheckGcpPrerequisites() => throw new NotImplementedException();

	[Conditional("RUN_S3_TESTS")]
	private static void CheckAwsCliPrerequisites() {
		const string awsDirectoryName = ".aws";
		var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
		homeDir = Path.Combine(homeDir, awsDirectoryName);
		if (!Directory.Exists(homeDir))
			throw new AwsCliDirectoryNotFoundException(homeDir);
	}
}
