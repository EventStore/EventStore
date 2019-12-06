namespace EventStore.Grpc.Streams {
	partial class AppendReq {
		public AppendReq WithAnyStreamRevision(AnyStreamRevision expectedRevision) {
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
