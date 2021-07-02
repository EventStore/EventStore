using System;
using EventStore.Core.LogAbstraction;
using EventStore.Core.Services;

namespace EventStore.Core.LogV3 {
	public class StreamExistenceFilterValidator : INameExistenceFilter {
		private readonly INameExistenceFilter _wrapped;

		public StreamExistenceFilterValidator(INameExistenceFilter wrapped) {
			_wrapped = wrapped;
		}

		public long CurrentCheckpoint => _wrapped.CurrentCheckpoint;

		public void Initialize(INameExistenceFilterInitializer source) => _wrapped.Initialize(source);

		public void Add(string streamName, long checkpoint) {
			ValidateStreamName(streamName);
			_wrapped.Add(streamName, checkpoint);
		}

		public void Add(ulong hash, long checkpoint) => throw new NotSupportedException();

		public bool MightContain(string streamName) {
			ValidateStreamName(streamName);
			return _wrapped.MightContain(streamName);
		}

		private void ValidateStreamName(string streamName) {
			if (string.IsNullOrEmpty(streamName))
				throw new ArgumentException($"{nameof(streamName)} must not be null or empty", nameof(streamName));

			if (SystemStreams.IsMetastream(streamName))
				throw new ArgumentException($"{nameof(streamName)} must not be a metastream", nameof(streamName));

			if (LogV3SystemStreams.TryGetVirtualStreamId(streamName, out _))
				throw new ArgumentException($"{nameof(streamName)} must not be a virtual stream", nameof(streamName));
		}

		public void Dispose() => _wrapped?.Dispose();
	}
}
