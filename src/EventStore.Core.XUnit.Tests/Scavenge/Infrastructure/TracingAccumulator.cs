using System.Threading;
using EventStore.Core.TransactionLog.Scavenging;

namespace EventStore.Core.XUnit.Tests.Scavenge {
	public class TracingAccumulator<TStreamId> : IAccumulator<TStreamId> {
		private readonly IAccumulator<TStreamId> _wrapped;
		private readonly Tracer _tracer;

		public TracingAccumulator(IAccumulator<TStreamId> wrapped, Tracer tracer) {
			_wrapped = wrapped;
			_tracer = tracer;
		}

		public void Accumulate(
			ScavengePoint prevScavengePoint,
			ScavengePoint scavengePoint,
			IScavengeStateForAccumulator<TStreamId> state,
			CancellationToken cancellationToken) {

			_tracer.TraceIn($"Accumulating from {prevScavengePoint?.GetName() ?? "start"} to {scavengePoint.GetName()}");
			try {
				_wrapped.Accumulate(prevScavengePoint, scavengePoint, state, cancellationToken);
				_tracer.TraceOut("Done");
			} catch {
				_tracer.TraceOut("Exception accumulating");
				throw;
			}
		}

		public void Accumulate(
			ScavengeCheckpoint.Accumulating checkpoint,
			IScavengeStateForAccumulator<TStreamId> state,
			CancellationToken cancellationToken) {

			_tracer.TraceIn($"Accumulating from checkpoint: {checkpoint}");
			try {
				_wrapped.Accumulate(checkpoint, state, cancellationToken);
				_tracer.TraceOut("Done");
			} catch {
				_tracer.TraceOut("Exception accumulating");
				throw;
			}
		}
	}
}
