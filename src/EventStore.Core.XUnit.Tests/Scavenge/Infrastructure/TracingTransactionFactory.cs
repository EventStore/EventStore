using EventStore.Core.TransactionLog.Scavenging;

namespace EventStore.Core.XUnit.Tests.Scavenge {
	public class TracingTransactionFactory<TTransaction> : ITransactionFactory<TTransaction> {
		private readonly ITransactionFactory<TTransaction> _wrapped;
		private readonly Tracer _tracer;

		public TracingTransactionFactory(ITransactionFactory<TTransaction> wrapped, Tracer tracer) {
			_wrapped = wrapped;
			_tracer = tracer;
		}

		public TTransaction Begin() {
			_tracer.TraceIn("Begin");
			return _wrapped.Begin();
		}

		public void Commit(TTransaction transaction) {
			_wrapped.Commit(transaction);
			_tracer.TraceOut("Commit");
		}

		public void Rollback(TTransaction transaction) {
			_wrapped.Rollback(transaction);
			_tracer.TraceOut("Rollback");
		}
	}
}
