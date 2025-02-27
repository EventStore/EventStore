// Copyright (c) Kurrent, Inc and/or licensed to Kurrent, Inc under one or more agreements.
// Kurrent, Inc licenses this file to you under the Kurrent License v1 (see LICENSE.md).

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
						AllOptionOneofCase.Position => new Core.Services.Transport.Common.Position(Position.CommitPosition,
							Position.PreparePosition),
						_ => throw new InvalidOperationException()
					};

					internal Core.Services.Transport.Common.Position? ToSubscriptionPosition() => AllOptionCase switch {
						AllOptionOneofCase.End => Core.Services.Transport.Common.Position.End,
						AllOptionOneofCase.Start => null,
						AllOptionOneofCase.Position => new Core.Services.Transport.Common.Position(Position.CommitPosition,
							Position.PreparePosition),
						_ => throw new InvalidOperationException()
					};
				}
			}
		}
	}
}
