using System;

// ReSharper disable CheckNamespace
namespace EventStore.Grpc.Streams {
// ReSharper restore CheckNamespace

	partial class ReadReq {
		partial class Types {
			partial class Options {
				partial class Types {
					public partial class StreamOptions {
						internal StreamRevision ToStreamRevision() => RevisionOptionsCase switch {
							RevisionOptionsOneofCase.End => StreamRevision.End,
							RevisionOptionsOneofCase.Start => StreamRevision.Start,
							RevisionOptionsOneofCase.Revision => new StreamRevision(Revision),
							_ => throw new InvalidOperationException()
						};
					}

					public partial class AllOptions {
						internal Grpc.Position ToPosition() => AllOptionsCase switch {
							AllOptionsOneofCase.End => Grpc.Position.End,
							AllOptionsOneofCase.Start => Grpc.Position.Start,
							AllOptionsOneofCase.Position => new Grpc.Position(Position.CommitPosition,
								Position.PreparePosition),
							_ => throw new InvalidOperationException()
						};
					}
				}
			}
		}
	}
}
