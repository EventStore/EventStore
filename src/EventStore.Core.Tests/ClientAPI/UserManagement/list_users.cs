// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using EventStore.ClientAPI.SystemData;
using NUnit.Framework;

namespace EventStore.Core.Tests.ClientAPI.UserManagement;

[Category("ClientAPI"), Category("LongRunning")]
[TestFixture(typeof(LogFormat.V2), typeof(string))]
[TestFixture(typeof(LogFormat.V3), typeof(uint))]
public class list_users<TLogFormat, TStreamId> : TestWithNode<TLogFormat, TStreamId> {
	[Test]
	public async System.Threading.Tasks.Task list_all_users_worksAsync() {
		await _manager.CreateUserAsync("ouro", "ourofull", new[] { "foo", "bar" }, "ouro",
			new UserCredentials("admin", "changeit"));
		var x = await _manager.ListAllAsync(new UserCredentials("admin", "changeit"));
		Assert.AreEqual(3, x.Count);
		Assert.AreEqual("admin", x[0].LoginName);
		Assert.AreEqual("KurrentDB Administrator", x[0].FullName);
		Assert.AreEqual("ops", x[1].LoginName);
		Assert.AreEqual("KurrentDB Operations", x[1].FullName);
		Assert.AreEqual("ouro", x[2].LoginName);
		Assert.AreEqual("ourofull", x[2].FullName);
	}
}
