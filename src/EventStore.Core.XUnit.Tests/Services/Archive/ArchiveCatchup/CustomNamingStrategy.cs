// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using EventStore.Core.TransactionLog.FileNamingStrategy;

namespace EventStore.Core.XUnit.Tests.Services.Archive.ArchiveCatchup;

// this naming strategy uses the version field to store the chunk's end number
internal class CustomNamingStrategy : IVersionedFileNamingStrategy {
	public string GetFilenameFor(int chunkStartNumber, int chunkEndNumber) => $"chunk-{chunkStartNumber}.{chunkEndNumber}";
	public string DetermineNewVersionFilenameForIndex(int index, int defaultVersion) => throw new NotImplementedException();
	public string[] GetAllVersionsFor(int index) => throw new NotImplementedException();
	public string[] GetAllPresentFiles() => throw new NotImplementedException();
	public string CreateTempFilename() => throw new NotImplementedException();
	public string[] GetAllTempFiles() => throw new NotImplementedException();

	public int GetIndexFor(string fileName) {
		var idx1 = fileName.IndexOf('-') + 1;
		var idx2 = fileName.IndexOf('.');
		return int.Parse(fileName[idx1..idx2]);
	}

	public int GetVersionFor(string fileName) {
		var idx = fileName.IndexOf('.') + 1;
		return int.Parse(fileName[idx..]);
	}

	public string GetPrefixFor(int? index, int? version) => throw new NotImplementedException();
}
