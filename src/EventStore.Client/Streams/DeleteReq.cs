namespace EventStore.Client.Streams {
	partial class DeleteReq {
		public DeleteReq WithAnyStreamRevision(AnyStreamRevision expectedRevision) {
			if (expectedRevision == AnyStreamRevision.Any) {
				Options.Any = new Types.Empty();
			} else if (expectedRevision == AnyStreamRevision.NoStream) {
				Options.NoStream = new Types.Empty();
			} else if (expectedRevision == AnyStreamRevision.StreamExists) {
				Options.StreamExists = new Types.Empty();
			}

			return this;
		}
	}
}
