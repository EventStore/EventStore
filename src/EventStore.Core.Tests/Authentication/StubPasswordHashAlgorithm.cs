// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Linq;
using EventStore.Core.Authentication.InternalAuthentication;

namespace EventStore.Core.Tests.Authentication;

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
