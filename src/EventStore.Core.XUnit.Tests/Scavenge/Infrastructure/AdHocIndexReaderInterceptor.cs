using System;
using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Core.TransactionLog.Scavenging;

namespace EventStore.Core.XUnit.Tests.Scavenge {
	public class AdHocIndexReaderInterceptor<TStreamId> : IIndexReaderForCalculator<TStreamId> {
		private readonly IIndexReaderForCalculator<TStreamId> _wrapped;
		private readonly Func<
			Func<StreamHandle<TStreamId>, long, int, ScavengePoint, IndexReadEventInfoResult>,
			StreamHandle<TStreamId>, long, int, ScavengePoint, IndexReadEventInfoResult> _f;


		public AdHocIndexReaderInterceptor(
			IIndexReaderForCalculator<TStreamId> wrapped,
			Func<
				Func<StreamHandle<TStreamId>, long, int, ScavengePoint, IndexReadEventInfoResult>,
				StreamHandle<TStreamId>, long, int, ScavengePoint, IndexReadEventInfoResult> f) {

			_wrapped = wrapped;
			_f = f;
		}

		public long GetLastEventNumber(
			StreamHandle<TStreamId> streamHandle,
			ScavengePoint scavengePoint) {

			return _wrapped.GetLastEventNumber(streamHandle, scavengePoint);
		}

		public IndexReadEventInfoResult ReadEventInfoForward(
			StreamHandle<TStreamId> stream,
			long fromEventNumber,
			int maxCount,
			ScavengePoint scavengePoint) {

			return _f(_wrapped.ReadEventInfoForward, stream, fromEventNumber, maxCount, scavengePoint);
		}

		public bool IsTombstone(long logPosition) {
			return _wrapped.IsTombstone(logPosition);
		}
	}
}
