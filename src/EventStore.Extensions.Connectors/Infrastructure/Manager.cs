// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using EventStore.Core;
using EventStore.Core.Bus;
using EventStore.Core.Services.Transport.Enumerators;
using Kurrent.Surge;
using Kurrent.Toolkit;
using ResolvedEvent = EventStore.Core.Data.ResolvedEvent;
using StreamMetadata = Kurrent.Surge.StreamMetadata;
using StreamRevision = Kurrent.Surge.StreamRevision;

namespace EventStore.Connectors.Infrastructure;

public class Manager : IManager {
    public Manager(IPublisher publisher) => Client = publisher;

    IPublisher Client { get; }

    public async ValueTask<bool> StreamExists(StreamId stream, CancellationToken cancellationToken) {
        try {
            var read = Client.ReadStreamBackwards(stream: stream,
                    startRevision: Core.Services.Transport.Common.StreamRevision.End,
                    maxCount: 1,
                    cancellationToken: cancellationToken)
                .FirstOrDefaultAsync(cancellationToken: cancellationToken);

            return await read.Then(x => x != ResolvedEvent.EmptyEvent).ConfigureAwait(false);
        } catch (Exception) {
            return false;
        }
    }

    public async ValueTask<DeleteStreamResult> DeleteStream(StreamId stream, CancellationToken cancellationToken) {
        try {
            var result = await Client.SoftDeleteStream(stream, expectedRevision: (int)StreamState.Exists, cancellationToken);
            return LogPosition.From(result.Position.CommitPosition);
        } catch (ReadResponseException.WrongExpectedRevision) {
            return new StreamNotFoundError(stream);
        } catch (ReadResponseException.StreamNotFound) {
            return new StreamNotFoundError(stream);
        } catch (ReadResponseException.StreamDeleted) {
            return new StreamNotFoundError(stream);
        }
    }

    public async ValueTask<DeleteStreamResult> DeleteStream(StreamId stream, StreamRevision expectedStreamRevision, CancellationToken cancellationToken) {
        try {
            var result = await Client
                .DeleteStream(stream, expectedStreamRevision, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            return LogPosition.From(result.Position.CommitPosition);
        } catch (ReadResponseException.WrongExpectedRevision ex) {
            return new ExpectedStreamRevisionError(stream, expectedStreamRevision, StreamRevision.From(ex.ActualStreamRevision.ToInt64()));
        } catch (ReadResponseException.StreamNotFound) {
            return new StreamNotFoundError(stream);
        } catch (ReadResponseException.StreamDeleted) {
            return new StreamNotFoundError(stream);
        }
    }

    public async ValueTask<DeleteStreamResult> DeleteStream(StreamId stream, LogPosition expectedLogPosition, CancellationToken cancellationToken) {
        var result = await GetStreamInfo(expectedLogPosition, cancellationToken).ConfigureAwait(false);

        return await result.Match(
            info => DeleteStream(stream, info.Revision, cancellationToken),
            _ => new(new StreamNotFoundError(stream))
        );
    }

    async ValueTask<StreamMetadata> IManager.ConfigureStream(
        StreamId stream, Func<StreamMetadata, StreamMetadata> configure, CancellationToken cancellationToken
    ) {
        var (metadata, revision) = await GetStreamMetadataInternal(stream, cancellationToken).ConfigureAwait(false);

        var newMetadata = configure(metadata);

        if (newMetadata != metadata)
            _ = await SetStreamMetadata(stream, newMetadata, revision, cancellationToken).ConfigureAwait(false);

        return newMetadata;
    }

    async ValueTask<(StreamMetadata Metadata, StreamRevision Revision)> SetStreamMetadata(
        StreamId stream, StreamMetadata metadata, StreamRevision expectedRevision, CancellationToken cancellationToken = default
    ) {
        var meta = new EventStore.Core.Data.StreamMetadata(
            maxCount: metadata.MaxCount is not null ? (int)metadata.MaxCount.Value : null, // so in the DB its a long but in the client its an int?!?
            maxAge: metadata.MaxAge,
            truncateBefore: metadata.TruncateBefore, // this should be a log position apparently
            cacheControl: metadata.CacheControl,
            acl: metadata.Acl is not null
                ? new Core.Data.StreamAcl(readRoles: metadata.Acl.ReadRoles,
                    writeRoles: metadata.Acl.WriteRoles,
                    deleteRoles: metadata.Acl.DeleteRoles,
                    metaReadRoles: metadata.Acl.MetaReadRoles,
                    metaWriteRoles: metadata.Acl.MetaWriteRoles)
                : null);

        if (expectedRevision == StreamRevision.Unset) {
            var result = await Client.SetStreamMetadata(stream,
                metadata: meta,
                expectedRevision: -1,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            return (metadata, StreamRevision.From(result.Revision));
        } else {
            var metaRevision = EventStore.Client.StreamRevision.FromInt64(expectedRevision);
            var result       = await Client.SetStreamMetadata(stream, meta, metaRevision.ToInt64(), cancellationToken: cancellationToken).ConfigureAwait(false);
            return (metadata, StreamRevision.From(result.Revision));
        }
    }

    async ValueTask<(StreamMetadata Metadata, StreamRevision MetadataRevision)> GetStreamMetadataInternal(
        StreamId stream, CancellationToken cancellationToken = default
    ) {
        var result = await Client.GetStreamMetadata(stream, cancellationToken: cancellationToken).ConfigureAwait(false);

        var meta = new StreamMetadata {
            MaxCount       = result.Metadata.MaxCount,
            MaxAge         = result.Metadata.MaxAge,
            TruncateBefore = result.Metadata.TruncateBefore, // this should be a log position apparently // review
            CacheControl   = result.Metadata.CacheControl,
            Acl = result.Metadata.Acl is not null
                ? new StreamMetadata.StreamAcl {
                    ReadRoles      = result.Metadata.Acl.ReadRoles,
                    WriteRoles     = result.Metadata.Acl.WriteRoles,
                    DeleteRoles    = result.Metadata.Acl.DeleteRoles,
                    MetaReadRoles  = result.Metadata.Acl.MetaReadRoles,
                    MetaWriteRoles = result.Metadata.Acl.MetaWriteRoles
                }
                : null
        };

        if (result.Revision is -2) // stream not found
            return (StreamMetadata.Empty, StreamRevision.Unset);

        var metaRevision = result.Revision is -1 // unset
            ? StreamRevision.Unset
            : StreamRevision.From(result.Revision);

        return (meta, metaRevision);
    }

    public ValueTask<StreamMetadata> GetStreamMetadata(StreamId stream, CancellationToken cancellationToken) =>
        GetStreamMetadataInternal(stream, cancellationToken).Then(x => x.Metadata);

    public async ValueTask<GetStreamInfoResult> GetStreamInfo(LogPosition position, CancellationToken cancellationToken = default) {
        var kdbPosition = position == LogPosition.Earliest
            ? Core.Services.Transport.Common.Position.Start
            : new EventStore.Core.Services.Transport.Common.Position(position.CommitPosition.GetValueOrDefault(),
                position.PreparePosition.GetValueOrDefault());

        ResolvedEvent? re = await Client
            .ReadForwards(kdbPosition, maxCount: 1, cancellationToken: cancellationToken)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return (
            StreamId.From(re.Value.OriginalEvent.EventStreamId),
            StreamRevision.From(re.Value.OriginalEvent.EventNumber)
        );
    }
}
