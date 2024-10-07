// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using EventStore.Core.Tests;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.core_projection.multi_phase;

[TestFixture(typeof(LogFormat.V2), typeof(string))]
[TestFixture(typeof(LogFormat.V3), typeof(uint))]
class when_starting_a_multi_phase_projection<TLogFormat, TStreamId> : specification_with_multi_phase_core_projection<TLogFormat, TStreamId> {
	protected override void When() {
		_coreProjection.Start();
	}

	[Test]
	public void it_starts() {
	}
}
