namespace EventStore.Core.TransactionLog.FileNamingStrategy {
	public interface IFileNamingStrategy {
		string GetFilenameFor(int index, int version);
		string DetermineBestVersionFilenameFor(int index);
		string[] GetAllVersionsFor(int index);
		string[] GetAllPresentFiles();

		string GetTempFilename();
		string[] GetAllTempFiles();
	}
}
