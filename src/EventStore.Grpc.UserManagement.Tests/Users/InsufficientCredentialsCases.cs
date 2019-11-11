using System;
using System.Collections;
using System.Collections.Generic;

namespace EventStore.Grpc.Users {
	public class InsufficientCredentialsCases : IEnumerable<object[]> {
		public IEnumerator<object[]> GetEnumerator() {
			var loginName = Guid.NewGuid().ToString();

			yield return new object[] {loginName, null};
			loginName = Guid.NewGuid().ToString();
			yield return new object[] {loginName, new UserCredentials(Guid.NewGuid().ToString(), "password")};
			loginName = Guid.NewGuid().ToString();
			yield return new object[] {loginName, new UserCredentials(loginName, "wrong-password")};
		}

		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
	}
}
