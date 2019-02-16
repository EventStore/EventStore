namespace EventStore.Core.Authentication {
	public abstract class PasswordHashAlgorithm {
		public abstract void Hash(string password, out string hash, out string salt);
		public abstract bool Verify(string password, string hash, string salt);
	}
}
