using EventStore.Common.Utils;

namespace EventStore.Core.LogV3 {
	public class LogV3StreamIdValidator : IValidator<long> {
		public LogV3StreamIdValidator() {
		}

		// Corresponding to LogV2StreamIdValidator we just check here that the steamId is
		// in the right space. The stream does not have to exist.
		public void Validate(long streamId) {
			Ensure.Nonnegative(streamId, "streamId");
		}
	}
}
