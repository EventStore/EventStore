// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System.Threading;
using System.Threading.Tasks;
using EventStore.Core.TransactionLog.Chunks;
using EventStore.Core.TransactionLog.Scavenging;

namespace EventStore.Core.XUnit.Tests.Scavenge {
	public class TracingChunkExecutor<TStreamId> : IChunkExecutor<TStreamId> {
		private readonly IChunkExecutor<TStreamId> _wrapped;
		private readonly Tracer _tracer;

		public TracingChunkExecutor(IChunkExecutor<TStreamId> wrapped, Tracer tracer) {
			_wrapped = wrapped;
			_tracer = tracer;
		}

		public async ValueTask Execute(
			ScavengePoint scavengePoint,
			IScavengeStateForChunkExecutor<TStreamId> state,
			ITFChunkScavengerLog scavengerLogger,
			CancellationToken cancellationToken) {

			_tracer.TraceIn($"Executing chunks for {scavengePoint.GetName()}");
			try {
				await _wrapped.Execute(scavengePoint, state, scavengerLogger, cancellationToken);
				_tracer.TraceOut("Done");
			} catch {
				_tracer.TraceOut("Exception executing chunks");
				throw;
			}
		}

		public async ValueTask Execute(
			ScavengeCheckpoint.ExecutingChunks checkpoint,
			IScavengeStateForChunkExecutor<TStreamId> state,
			ITFChunkScavengerLog scavengerLogger,
			CancellationToken cancellationToken) {

			_tracer.TraceIn($"Executing chunks from checkpoint: {checkpoint}");
			try {
				await _wrapped.Execute(checkpoint, state, scavengerLogger, cancellationToken);
				_tracer.TraceOut("Done");
			} catch {
				_tracer.TraceOut("Exception executing chunks");
				throw;
			}
		}
	}
}
