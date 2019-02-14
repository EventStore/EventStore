using System;
using System.Linq;
using EventStore.Core.Authentication;

namespace EventStore.Core.Tests.Authentication {
	public class StubPasswordHashAlgorithm : PasswordHashAlgorithm {
		public override void Hash(string password, out string hash, out string salt) {
			hash = password;
			salt = ReverseString(password);
		}

		public override bool Verify(string password, string hash, string salt) {
			return password == hash && ReverseString(password) == salt;
		}

		private static string ReverseString(string s) {
			return new String(s.Reverse().ToArray());
		}
	}
}
