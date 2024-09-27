// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using EventStore.Projections.Core.Services.Processing;
using EventStore.Projections.Core.Services.Processing.Checkpointing;
using EventStore.Projections.Core.Services.Processing.MultiStream;

namespace EventStore.Projections.Core.Tests.Services.core_projection.checkpoint_manager.multi_stream {
	public abstract class TestFixtureWithMultiStreamCheckpointManager<TLogFormat, TStreamId> : TestFixtureWithCoreProjectionCheckpointManager<TLogFormat, TStreamId> {
		protected new string[] _streams;

		protected override void Given() {
			base.Given();
			_projectionVersion = new ProjectionVersion(1, 0, 0);
			_streams = new[] {"a", "b", "c"};
		}

		protected override DefaultCheckpointManager GivenCheckpointManager() {
			return new MultiStreamMultiOutputCheckpointManager(
				_bus, _projectionCorrelationId, _projectionVersion, null, _ioDispatcher, _config, _projectionName,
				new MultiStreamPositionTagger(0, _streams), _namingBuilder, _checkpointsEnabled, true, true,
				_checkpointWriter);
		}
	}
}
