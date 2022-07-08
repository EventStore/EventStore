using System.Threading;
using EventStore.Core.TransactionLog.Scavenging;

namespace EventStore.Core.XUnit.Tests.Scavenge {
	public class TracingCalculator<TStreamId> : ICalculator<TStreamId> {
		private readonly ICalculator<TStreamId> _wrapped;
		private readonly Tracer _tracer;

		public TracingCalculator(ICalculator<TStreamId> wrapped, Tracer tracer) {
			_wrapped = wrapped;
			_tracer = tracer;
		}

		public void Calculate(
			ScavengePoint scavengePoint,
			IScavengeStateForCalculator<TStreamId> source,
			CancellationToken cancellationToken) {

			_tracer.TraceIn($"Calculating {scavengePoint.GetName()}");
			try {
				_wrapped.Calculate(scavengePoint, source, cancellationToken);
				_tracer.TraceOut("Done");
			} catch {
				_tracer.TraceOut("Exception calculating");
				throw;
			}
		}

		public void Calculate(
			ScavengeCheckpoint.Calculating<TStreamId> checkpoint,
			IScavengeStateForCalculator<TStreamId> source,
			CancellationToken cancellationToken) {

			_tracer.TraceIn($"Calculating from checkpoint: {checkpoint}");
			try {
				_wrapped.Calculate(checkpoint, source, cancellationToken);
				_tracer.TraceOut("Done");
			} catch {
				_tracer.TraceOut("Exception calculating");
				throw;
			}
		}
	}
}
