using EventStore.Core.TransactionLog.Scavenging;

namespace EventStore.Core.XUnit.Tests.Scavenge {
	public class TracingChunkManagerForChunkExecutor<TStreamId, TRecord> :
		IChunkManagerForChunkExecutor<TStreamId, TRecord> {

		private readonly IChunkManagerForChunkExecutor<TStreamId, TRecord> _wrapped;
		private readonly Tracer _tracer;

		public TracingChunkManagerForChunkExecutor(
			IChunkManagerForChunkExecutor<TStreamId, TRecord> wrapped, Tracer tracer) {

			_wrapped = wrapped;
			_tracer = tracer;
		}

		public IChunkWriterForExecutor<TStreamId, TRecord> CreateChunkWriter(
			IChunkReaderForExecutor<TStreamId, TRecord> sourceChunk) {

			return new TracingChunkWriterForExecutor<TStreamId, TRecord>(
				_wrapped.CreateChunkWriter(sourceChunk),
				_tracer);
		}

		public IChunkReaderForExecutor<TStreamId, TRecord> GetChunkReaderFor(long position) {
			var reader = _wrapped.GetChunkReaderFor(position);
			return new TrackingChunkReaderForExecutor<TStreamId, TRecord>(reader, _tracer);
		}
	}
}
