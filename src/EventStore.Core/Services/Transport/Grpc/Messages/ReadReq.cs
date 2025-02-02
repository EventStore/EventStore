// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using EventStore.Core.Services.Transport.Common;

// ReSharper disable CheckNamespace
namespace EventStore.Client.Streams;

// ReSharper restore CheckNamespace
partial class ReadReq {
	partial class Types {
		partial class Options {
			partial class Types {
				partial class StreamOptions {
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

				partial class AllOptions {
					internal Core.Services.Transport.Common.Position ToPosition() => AllOptionCase switch {
						AllOptionOneofCase.End => Core.Services.Transport.Common.Position.End,
						AllOptionOneofCase.Start => Core.Services.Transport.Common.Position.Start,
						AllOptionOneofCase.Position => new(Position.CommitPosition, Position.PreparePosition),
						_ => throw new InvalidOperationException()
					};

					internal Core.Services.Transport.Common.Position? ToSubscriptionPosition() => AllOptionCase switch {
						AllOptionOneofCase.End => Core.Services.Transport.Common.Position.End,
						AllOptionOneofCase.Start => null,
						AllOptionOneofCase.Position => new(Position.CommitPosition, Position.PreparePosition),
						_ => throw new InvalidOperationException()
					};
				}
			}
		}
	}
}
