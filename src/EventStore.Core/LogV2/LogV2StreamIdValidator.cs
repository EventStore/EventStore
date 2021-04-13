using EventStore.Common.Utils;

namespace EventStore.Core.LogV2 {
	public class LogV2StreamIdValidator : IValidator<string> {
		public LogV2StreamIdValidator() {
		}

		public void Validate(string streamId) {
			Ensure.NotNullOrEmpty(streamId, "streamId");
		}
	}
}
