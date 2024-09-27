// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

namespace EventStore.Core.TransactionLog.FileNamingStrategy {
	public interface IVersionedFileNamingStrategy {
		string GetFilenameFor(int index, int version);
		string DetermineBestVersionFilenameFor(int index, int initialVersion);
		string[] GetAllVersionsFor(int index);
		string[] GetAllPresentFiles();
		string GetTempFilename();
		string[] GetAllTempFiles();
		int GetIndexFor(string fileName);
		int GetVersionFor(string fileName);
	}
}
