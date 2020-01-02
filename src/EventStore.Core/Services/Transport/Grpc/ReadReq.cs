using System;

// ReSharper disable CheckNamespace
namespace EventStore.Grpc.Streams {
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
					}

					public partial class AllOptions {
						internal Grpc.Position ToPosition() => AllOptionCase switch {
							AllOptionOneofCase.End => Grpc.Position.End,
							AllOptionOneofCase.Start => Grpc.Position.Start,
							AllOptionOneofCase.Position => new Grpc.Position(Position.CommitPosition,
								Position.PreparePosition),
							_ => throw new InvalidOperationException()
						};
					}
				}
			}
		}
	}
}
