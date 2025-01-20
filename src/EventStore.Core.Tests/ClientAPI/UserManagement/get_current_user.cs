// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System.Threading.Tasks;
using EventStore.ClientAPI.SystemData;
using NUnit.Framework;

namespace EventStore.Core.Tests.ClientAPI.UserManagement;

[Category("ClientAPI"), Category("LongRunning")]
[TestFixture(typeof(LogFormat.V2), typeof(string))]
[TestFixture(typeof(LogFormat.V3), typeof(uint))]
public class get_current_user<TLogFormat, TStreamId> : TestWithNode<TLogFormat, TStreamId> {
	[Test]
	public async Task returns_the_current_user() {
		var x = await _manager.GetCurrentUserAsync(new UserCredentials("admin", "changeit"));
		Assert.AreEqual("admin", x.LoginName);
		Assert.AreEqual("KurrentDB Administrator", x.FullName);
	}
}
