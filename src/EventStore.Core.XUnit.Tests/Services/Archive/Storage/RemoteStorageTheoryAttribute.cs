// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using EventStore.Core.Services.Archive;
using Xunit.Sdk;

namespace EventStore.Core.XUnit.Tests.Services.Archive.Storage;

// if no symbol is required for the storage type then run the test (or not, according to skip).
// else a symbol is required
//    if required symbol is defined then check the prereqs, which throws if they are not present
//    else the required symbol is not defined -> skip
public static class StorageData {
	public abstract class RemoteStorageDataAttribute : DataAttribute {
		public RemoteStorageDataAttribute() {
			var symbolSet = false;
			CheckPrerequisites(out var symbol, ref symbolSet);
			if (!symbolSet)
				Skip = $"This remote storage test is disabled. Enable with {symbol} symbol.";
		}

		protected abstract StorageType StorageType { get; }
		protected abstract void CheckPrerequisites(out string symbol, ref bool symbolSet);
		public override IEnumerable<object[]> GetData(MethodInfo testMethod) => [[StorageType]];
	}


	public sealed class S3Attribute : RemoteStorageDataAttribute {
		const string Symbol = "RUN_S3_TESTS";
		protected override StorageType StorageType => StorageType.S3;
		protected override void CheckPrerequisites(out string symbol, ref bool symbolSet) {
			symbol = Symbol;
			CheckPrerequisitesImpl(ref symbolSet);
		}

		[Conditional(Symbol)]
		private static void CheckPrerequisitesImpl(ref bool symbolSet) {
			symbolSet = true;
			const string awsDirectoryName = ".aws";
			var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
			homeDir = Path.Combine(homeDir, awsDirectoryName);
			if (!Directory.Exists(homeDir))
				throw new AwsCliDirectoryNotFoundException(homeDir);
		}
	}

	public sealed class FileSystemAttribute : RemoteStorageDataAttribute {
		protected override StorageType StorageType => StorageType.FileSystem;

		protected override void CheckPrerequisites(out string symbol, ref bool symbolSet) {
			symbol = default;
			symbolSet = true;
		}
	}
}
