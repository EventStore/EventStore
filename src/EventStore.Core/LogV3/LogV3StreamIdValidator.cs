using EventStore.Common.Utils;

namespace EventStore.Core.LogV3 {
	public class LogV3StreamIdValidator : IValidator<long> {
		public LogV3StreamIdValidator() {
		}

		public void Validate(long streamId) {
			Ensure.Positive(streamId, "streamId");
		}
	}
}
