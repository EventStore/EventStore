namespace EventStore.Core.TransactionLog.FileNamingStrategy {
	public interface IVersionedFileNamingStrategy {
		string GetFilenameFor(int index, int version);
		string DetermineBestVersionFilenameFor(int index);
		string[] GetAllVersionsFor(int index);
		string[] GetAllPresentFiles();
		string GetTempFilename();
		string[] GetAllTempFiles();
		int GetIndexFor(string fileName);
		int GetVersionFor(string fileName);
	}
}
