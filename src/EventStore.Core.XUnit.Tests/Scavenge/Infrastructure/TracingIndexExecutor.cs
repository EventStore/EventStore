using System.Threading;
using EventStore.Core.Index;
using EventStore.Core.TransactionLog.Scavenging;

namespace EventStore.Core.XUnit.Tests.Scavenge {
	public class TracingIndexExecutor<TStreamId> : IIndexExecutor<TStreamId> {
		private readonly IIndexExecutor<TStreamId> _wrapped;
		private readonly Tracer _tracer;

		public TracingIndexExecutor(IIndexExecutor<TStreamId> wrapped, Tracer tracer) {
			_wrapped = wrapped;
			_tracer = tracer;
		}

		public void Execute(
			ScavengePoint scavengePoint,
			IScavengeStateForIndexExecutor<TStreamId> state,
			IIndexScavengerLog scavengerLogger,
			CancellationToken cancellationToken) {

			_tracer.TraceIn($"Executing index for {scavengePoint.GetName()}");
			try {
				_wrapped.Execute(scavengePoint, state, scavengerLogger, cancellationToken);
				_tracer.TraceOut("Done");
			} catch {
				_tracer.TraceOut("Exception executing index");
				throw;
			}
		}

		public void Execute(
			ScavengeCheckpoint.ExecutingIndex checkpoint,
			IScavengeStateForIndexExecutor<TStreamId> state,
			IIndexScavengerLog scavengerLogger,
			CancellationToken cancellationToken) {

			_tracer.TraceIn($"Executing index from checkpoint: {checkpoint}");
			try {
				_wrapped.Execute(checkpoint, state, scavengerLogger, cancellationToken);
				_tracer.TraceOut("Done");
			} catch {
				_tracer.TraceOut("Exception executing index");
				throw;
			}
		}
	}
}
