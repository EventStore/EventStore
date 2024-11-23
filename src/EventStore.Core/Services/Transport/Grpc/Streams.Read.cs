using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EventStore.Client.Streams;
using EventStore.Core.Messages;
using EventStore.Core.Metrics;
using EventStore.Core.Services.Storage.ReaderIndex;
using Grpc.Core;
using CountOptionOneofCase = EventStore.Client.Streams.ReadReq.Types.Options.CountOptionOneofCase;
using FilterOptionOneofCase = EventStore.Client.Streams.ReadReq.Types.Options.FilterOptionOneofCase;
using ReadDirection = EventStore.Client.Streams.ReadReq.Types.Options.Types.ReadDirection;
using StreamOptionOneofCase = EventStore.Client.Streams.ReadReq.Types.Options.StreamOptionOneofCase;

namespace EventStore.Core.Services.Transport.Grpc {
	internal partial class Streams<TStreamId> {
		public override async Task Read(
			ReadReq request,
			IServerStreamWriter<ReadResp> responseStream,
			ServerCallContext context) {

			var trackDuration = request.Options.CountOptionCase != CountOptionOneofCase.Subscription;
			using var duration = trackDuration ? _readTracker.Start() : Duration.Nil;
			try {
				var options = request.Options;
				var countOptionsCase = options.CountOptionCase;
				var streamOptionsCase = options.StreamOptionCase;
				var readDirection = options.ReadDirection;
				var filterOptionsCase = options.FilterOptionCase;
				var compatibility = options.ControlOption?.Compatibility ?? 0;

				var user = context.GetHttpContext().User;
				var tfTracker = _tfTrackers.For(user);
				var requiresLeader = GetRequiresLeader(context.RequestHeaders);

				var op = streamOptionsCase switch {
					StreamOptionOneofCase.Stream => ReadOperation.WithParameter(
						Plugins.Authorization.Operations.Streams.Parameters.StreamId(
							request.Options.Stream.StreamIdentifier)),
					StreamOptionOneofCase.All => ReadOperation.WithParameter(
						Plugins.Authorization.Operations.Streams.Parameters.StreamId(SystemStreams.AllStream)),
					_ => throw RpcExceptions.InvalidArgument(streamOptionsCase)
				};

				if (!await _provider.CheckAccessAsync(user, op, context.CancellationToken).ConfigureAwait(false)) {
					throw RpcExceptions.AccessDenied();
				}

				var enumerator =
					(streamOptionsCase, countOptionsCase, readDirection, filterOptionsCase) switch {
						(StreamOptionOneofCase.Stream,
							CountOptionOneofCase.Count,
							ReadDirection.Forwards,
							FilterOptionOneofCase.NoFilter) => (IAsyncEnumerator<ReadResp>)
							new Enumerators.ReadStreamForwards(
								_publisher,
								request.Options.Stream.StreamIdentifier,
								request.Options.Stream.ToStreamRevision(),
								request.Options.Count,
								request.Options.ResolveLinks,
								user,
								requiresLeader,
								context.Deadline,
								options.UuidOption,
								compatibility,
								context.CancellationToken),
						(StreamOptionOneofCase.Stream,
							CountOptionOneofCase.Count,
							ReadDirection.Backwards,
							FilterOptionOneofCase.NoFilter) => new Enumerators.ReadStreamBackwards(
								_publisher,
								request.Options.Stream.StreamIdentifier,
								request.Options.Stream.ToStreamRevision(),
								request.Options.Count,
								request.Options.ResolveLinks,
								user,
								requiresLeader,
								context.Deadline,
								options.UuidOption,
								compatibility,
								context.CancellationToken),
						(StreamOptionOneofCase.All,
							CountOptionOneofCase.Count,
							ReadDirection.Forwards,
							FilterOptionOneofCase.NoFilter) => new Enumerators.ReadAllForwards(
								_publisher,
								request.Options.All.ToPosition(),
								request.Options.Count,
								request.Options.ResolveLinks,
								user,
								requiresLeader,
								context.Deadline,
								options.UuidOption,
								context.CancellationToken),
						(StreamOptionOneofCase.All,
							CountOptionOneofCase.Count,
							ReadDirection.Forwards,
							FilterOptionOneofCase.Filter) => new Enumerators.ReadAllForwardsFiltered(
								_publisher,
								request.Options.All.ToPosition(),
								request.Options.Count,
								request.Options.ResolveLinks,
								ConvertToEventFilter(true, request.Options.Filter),
								user,
								requiresLeader,
								_readIndex,
								request.Options.Filter.WindowCase switch {
									ReadReq.Types.Options.Types.FilterOptions.WindowOneofCase.Count => null,
									ReadReq.Types.Options.Types.FilterOptions.WindowOneofCase.Max => request.Options.Filter
										.Max,
									_ => throw RpcExceptions.InvalidArgument(request.Options.Filter.WindowCase)
								},
								context.Deadline,
								options.UuidOption,
								context.CancellationToken),
						(StreamOptionOneofCase.All,
							CountOptionOneofCase.Count,
							ReadDirection.Backwards,
							FilterOptionOneofCase.NoFilter) => new Enumerators.ReadAllBackwards(
								_publisher,
								request.Options.All.ToPosition(),
								request.Options.Count,
								request.Options.ResolveLinks,
								user,
								requiresLeader,
								context.Deadline,
								options.UuidOption,
								context.CancellationToken),
						(StreamOptionOneofCase.All,
							CountOptionOneofCase.Count,
							ReadDirection.Backwards,
							FilterOptionOneofCase.Filter) => new Enumerators.ReadAllBackwardsFiltered(
								_publisher,
								request.Options.All.ToPosition(),
								request.Options.Count,
								request.Options.ResolveLinks,
								ConvertToEventFilter(true, request.Options.Filter),
								user,
								requiresLeader,
								_readIndex,
								request.Options.Filter.WindowCase switch {
									ReadReq.Types.Options.Types.FilterOptions.WindowOneofCase.Count => null,
									ReadReq.Types.Options.Types.FilterOptions.WindowOneofCase.Max => request.Options.Filter
										.Max,
									_ => throw RpcExceptions.InvalidArgument(request.Options.Filter.WindowCase)
								},
								context.Deadline,
								options.UuidOption,
								context.CancellationToken),
						(StreamOptionOneofCase.Stream,
							CountOptionOneofCase.Subscription,
							ReadDirection.Forwards,
							FilterOptionOneofCase.NoFilter) => new Enumerators.StreamSubscription<TStreamId>(
								_publisher,
								_expiryStrategy,
								request.Options.Stream.StreamIdentifier,
								request.Options.Stream.ToSubscriptionStreamRevision(),
								request.Options.ResolveLinks,
								user,
								requiresLeader,
								options.UuidOption,
								context.CancellationToken),
						(StreamOptionOneofCase.All,
							CountOptionOneofCase.Subscription,
							ReadDirection.Forwards,
							FilterOptionOneofCase.NoFilter) => new Enumerators.AllSubscription(
								_publisher,
								_expiryStrategy,
								request.Options.All.ToSubscriptionPosition(),
								request.Options.ResolveLinks,
								user,
								requiresLeader,
								_readIndex,
								options.UuidOption,
								tfTracker,
								context.CancellationToken),
						(StreamOptionOneofCase.All,
							CountOptionOneofCase.Subscription,
							ReadDirection.Forwards,
							FilterOptionOneofCase.Filter) => new Enumerators.AllSubscriptionFiltered(
								_publisher,
								_expiryStrategy,
								request.Options.All.ToSubscriptionPosition(),
								request.Options.ResolveLinks,
								ConvertToEventFilter(true, request.Options.Filter),
								user,
								requiresLeader,
								_readIndex,
								request.Options.Filter.WindowCase switch {
									ReadReq.Types.Options.Types.FilterOptions.WindowOneofCase.Count => null,
									ReadReq.Types.Options.Types.FilterOptions.WindowOneofCase.Max => request.Options.Filter
										.Max,
									_ => throw RpcExceptions.InvalidArgument(request.Options.Filter.WindowCase)
								},
								request.Options.Filter.CheckpointIntervalMultiplier,
								options.UuidOption,
								tfTracker,
								context.CancellationToken),
						_ => throw RpcExceptions.InvalidCombination((streamOptionsCase, countOptionsCase, readDirection,
							filterOptionsCase))
					};

				await using (enumerator.ConfigureAwait(false))
				await using (context.CancellationToken.Register(() => enumerator.DisposeAsync()).ConfigureAwait(false)) {
					while (await enumerator.MoveNextAsync().ConfigureAwait(false)) {
						await responseStream.WriteAsync(enumerator.Current).ConfigureAwait(false);
					}
				}

				IEventFilter ConvertToEventFilter(bool isAllStream, ReadReq.Types.Options.Types.FilterOptions filter) =>
					filter.FilterCase switch {
						ReadReq.Types.Options.Types.FilterOptions.FilterOneofCase.EventType => (
							string.IsNullOrEmpty(filter.EventType.Regex)
								? EventFilter.EventType.Prefixes(isAllStream, filter.EventType.Prefix.ToArray())
								: EventFilter.EventType.Regex(isAllStream, filter.EventType.Regex)),
						ReadReq.Types.Options.Types.FilterOptions.FilterOneofCase.StreamIdentifier => (
							string.IsNullOrEmpty(filter.StreamIdentifier.Regex)
								? EventFilter.StreamName.Prefixes(isAllStream, filter.StreamIdentifier.Prefix.ToArray())
								: EventFilter.StreamName.Regex(isAllStream, filter.StreamIdentifier.Regex)),
						_ => throw RpcExceptions.InvalidArgument(filter)
					};
			} catch (Exception ex) {
				duration.SetException(ex);
				throw;
			}
		}
	}
}
