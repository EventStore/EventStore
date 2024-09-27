// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System.Threading;
using EventStore.Core.TransactionLog.Scavenging;

namespace EventStore.Core.XUnit.Tests.Scavenge {
	public class TracingCleaner : ICleaner {
		private readonly ICleaner _wrapped;
		private readonly Tracer _tracer;

		public TracingCleaner(ICleaner wrapped, Tracer tracer) {
			_wrapped = wrapped;
			_tracer = tracer;
		}

		public void Clean(
			ScavengePoint scavengePoint,
			IScavengeStateForCleaner state,
			CancellationToken cancellationToken) {

			_tracer.TraceIn($"Cleaning for {scavengePoint.GetName()}");
			try {
				_wrapped.Clean(scavengePoint, state, cancellationToken);
				_tracer.TraceOut("Done");
			} catch {
				_tracer.TraceOut("Exception cleaning");
				throw;
			}
		}

		public void Clean(
			ScavengeCheckpoint.Cleaning checkpoint,
			IScavengeStateForCleaner state,
			CancellationToken cancellationToken) {

			_tracer.TraceIn($"Cleaning from checkpoint {checkpoint}");
			try {
				_wrapped.Clean(checkpoint, state, cancellationToken);
				_tracer.TraceOut("Done");
			} catch {
				_tracer.TraceOut("Exception cleaning");
				throw;
			}
		}
	}
}
