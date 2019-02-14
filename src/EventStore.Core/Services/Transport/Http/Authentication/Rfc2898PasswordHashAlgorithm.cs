using System.Security.Cryptography;
using EventStore.Core.Authentication;

namespace EventStore.Core.Services.Transport.Http.Authentication {
	public class Rfc2898PasswordHashAlgorithm : PasswordHashAlgorithm {
		private const int HashSize = 20;
		private const int SaltSize = 16;

		public override void Hash(string password, out string hash, out string salt) {
			var salt_ = new byte[SaltSize];
			var randomProvider = new RNGCryptoServiceProvider();
			randomProvider.GetBytes(salt_);
			var hash_ = new Rfc2898DeriveBytes(password, salt_).GetBytes(HashSize);
			hash = System.Convert.ToBase64String(hash_);
			salt = System.Convert.ToBase64String(salt_);
		}

		public override bool Verify(string password, string hash, string salt) {
			var salt_ = System.Convert.FromBase64String(salt);

			var newHash = System.Convert.ToBase64String(new Rfc2898DeriveBytes(password, salt_).GetBytes(HashSize));

			return hash == newHash;
		}
	}
}
