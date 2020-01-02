using System;

// ReSharper disable CheckNamespace
namespace EventStore.Client.Streams {
// ReSharper restore CheckNamespace

	partial class ReadReq {
		partial class Types {
			partial class Options {
				partial class Types {
					public partial class StreamOptions {
						internal StreamRevision ToStreamRevision() => RevisionOptionCase switch {
							RevisionOptionOneofCase.End => StreamRevision.End,
							RevisionOptionOneofCase.Start => StreamRevision.Start,
							RevisionOptionOneofCase.Revision => new StreamRevision(Revision),
							_ => throw new InvalidOperationException()
						};

						internal StreamRevision? ToSubscriptionStreamRevision() => RevisionOptionCase switch {
							RevisionOptionOneofCase.End => StreamRevision.End,
							RevisionOptionOneofCase.Start => null,
							RevisionOptionOneofCase.Revision => new StreamRevision(Revision),
							_ => throw new InvalidOperationException()
						};
					}

					public partial class AllOptions {
						internal Client.Position ToPosition() => AllOptionCase switch {
							AllOptionOneofCase.End => Client.Position.End,
							AllOptionOneofCase.Start => Client.Position.Start,
							AllOptionOneofCase.Position => new Client.Position(Position.CommitPosition,
								Position.PreparePosition),
							_ => throw new InvalidOperationException()
						};

						internal Client.Position? ToSubscriptionPosition() => AllOptionCase switch {
							AllOptionOneofCase.End => Client.Position.End,
							AllOptionOneofCase.Start => null,
							AllOptionOneofCase.Position => new Client.Position(Position.CommitPosition,
								Position.PreparePosition),
							_ => throw new InvalidOperationException()
						};
					}
				}
			}
		}
	}
}
