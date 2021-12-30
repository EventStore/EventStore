using System;

namespace EventStore.Core.TransactionLog.FileNamingStrategy {
	public interface IFileNamingStrategy {
		string GetFilenameFor(int index, int version);
		string DetermineBestVersionFilenameFor(int index);
		string[] GetAllVersionsFor(int index);
		string[] GetAllPresentFiles();
		void EnumerateAllFiles(
			Func<string, int> getNextFileNumber,
			Action<string, int, int> onLatestVersionFound = null,
			Action<string, int> onOldVersionFound = null,
			Action<string, int> onFileMissing = null);

		string GetTempFilename();
		string[] GetAllTempFiles();
	}
}
