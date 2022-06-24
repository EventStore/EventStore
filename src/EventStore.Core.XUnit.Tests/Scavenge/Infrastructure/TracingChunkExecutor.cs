using System.Threading;
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

		public void Execute(
			ScavengePoint scavengePoint,
			IScavengeStateForChunkExecutor<TStreamId> state,
			ITFChunkScavengerLog scavengerLogger,
			CancellationToken cancellationToken) {

			_tracer.TraceIn($"Executing chunks for {scavengePoint.GetName()}");
			try {
				_wrapped.Execute(scavengePoint, state, scavengerLogger, cancellationToken);
				_tracer.TraceOut("Done");
			} catch {
				_tracer.TraceOut("Exception executing chunks");
				throw;
			}
		}

		public void Execute(
			ScavengeCheckpoint.ExecutingChunks checkpoint,
			IScavengeStateForChunkExecutor<TStreamId> state,
			ITFChunkScavengerLog scavengerLogger,
			CancellationToken cancellationToken) {

			_tracer.TraceIn($"Executing chunks from checkpoint: {checkpoint}");
			try {
				_wrapped.Execute(checkpoint, state, scavengerLogger, cancellationToken);
				_tracer.TraceOut("Done");
			} catch {
				_tracer.TraceOut("Exception executing chunks");
				throw;
			}
		}
	}
}
