namespace EventStore.Core.TransactionLog.Hashes {
	public interface IHasher {
		uint Hash(string s);
		uint Hash(byte[] data);
		uint Hash(byte[] data, int offset, uint len, uint seed);
	}
}
