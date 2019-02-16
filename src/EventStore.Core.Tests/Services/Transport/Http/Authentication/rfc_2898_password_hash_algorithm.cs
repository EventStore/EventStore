using EventStore.Core.Services.Transport.Http.Authentication;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.Transport.Http.Authentication {
	namespace rfc_2898_password_hash_algorithm {
		[TestFixture]
		public class when_hashing_a_password {
			private Rfc2898PasswordHashAlgorithm _algorithm;
			private string _password;
			private string _hash;
			private string _salt;

			[SetUp]
			public void SetUp() {
				_password = "Pa55w0rd!";
				_algorithm = new Rfc2898PasswordHashAlgorithm();
				_algorithm.Hash(_password, out _hash, out _salt);
			}

			[Test]
			public void verifies_correct_password() {
				Assert.That(_algorithm.Verify(_password, _hash, _salt));
			}

			[Test]
			public void does_not_verify_incorrect_password() {
				Assert.That(!_algorithm.Verify(_password.ToUpper(), _hash, _salt));
			}
		}

		[TestFixture]
		public class when_hashing_a_password_twice {
			private Rfc2898PasswordHashAlgorithm _algorithm;
			private string _password;
			private string _hash1;
			private string _salt1;
			private string _hash2;
			private string _salt2;

			[SetUp]
			public void SetUp() {
				_password = "Pa55w0rd!";
				_algorithm = new Rfc2898PasswordHashAlgorithm();
				_algorithm.Hash(_password, out _hash1, out _salt1);
				_algorithm.Hash(_password, out _hash2, out _salt2);
			}

			[Test]
			public void uses_different_salt() {
				Assert.That(_salt1 != _salt2);
			}

			[Test]
			public void generates_different_hashes() {
				Assert.That(_hash1 != _hash2);
			}
		}
	}
}
