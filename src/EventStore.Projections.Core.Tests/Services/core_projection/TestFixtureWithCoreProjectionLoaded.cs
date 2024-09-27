// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

namespace EventStore.Projections.Core.Tests.Services.core_projection {
	public abstract class TestFixtureWithCoreProjectionLoaded<TLogFormat, TStreamId> : TestFixtureWithCoreProjection<TLogFormat, TStreamId> {
		protected override void PreWhen() {
			_coreProjection.LoadStopped();
		}
	}
}
