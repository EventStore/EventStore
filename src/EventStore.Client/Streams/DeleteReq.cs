using EventStore.Client.Shared;

namespace EventStore.Client.Streams {
	partial class DeleteReq {
		public DeleteReq WithAnyStreamRevision(AnyStreamRevision expectedRevision) {
			if (expectedRevision == AnyStreamRevision.Any) {
				Options.Any = new Empty();
			} else if (expectedRevision == AnyStreamRevision.NoStream) {
				Options.NoStream = new Empty();
			} else if (expectedRevision == AnyStreamRevision.StreamExists) {
				Options.StreamExists = new Empty();
			}

			return this;
		}
	}
}
