using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using EventStore.Client.Shared;
using EventStore.Client.Streams;
using Google.Protobuf;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Net.Http.Headers;

#nullable enable

namespace EventStore.TestClient.Commands.Grpc {
	internal class WriteFloodProcessor : ICmdProcessor {
		private static readonly UTF8Encoding UTF8NoBom = new UTF8Encoding(false);

		public string Usage {
			get { return "WRFLGRPC [<clients> <requests> [<streams-cnt> [<size>] [<batchsize>]]]"; }
		}

		public string Keyword {
			get { return "WRFLGRPC"; }
		}

		public bool Execute(CommandProcessorContext context, string[] args) {
			int clientsCnt = 1;
			long requestsCnt = 5000;
			int streamsCnt = 1000;
			int size = 256;
			int batchSize = 1;
			if (args.Length > 0)
			{
			    if (args.Length < 2 || args.Length > 5)
			        return false;
			
			    try
			    {
			        clientsCnt = int.Parse(args[0]);
			        requestsCnt = long.Parse(args[1]);
			        if (args.Length >= 3)
			            streamsCnt = int.Parse(args[2]);
			        if (args.Length >= 4)
			            size = int.Parse(args[3]);
			        if (args.Length >= 5)
			            batchSize = int.Parse(args[4]);
			    }
			    catch
			    {
			        return false;
			    }
			}
			
			var monitor = new RequestMonitor();
			try {
				var task = WriteFlood(context, clientsCnt, requestsCnt, streamsCnt, size, batchSize, monitor);
				task.Wait();
			} catch (Exception ex) {
				context.Fail(ex);
			}

			return true;
		}

		private async Task WriteFlood(CommandProcessorContext context, int clientsCnt, long requestsCnt, int streamsCnt,
			int size, int batchSize, RequestMonitor monitor) {
			context.IsAsync();

			long succ = 0;
			long last = 0;
			long fail = 0;
			long prepTimeout = 0;
			long commitTimeout = 0;
			long forwardTimeout = 0;
			long wrongExpVersion = 0;
			long streamDeleted = 0;
			long all = 0;
			long interval = 100000;
			long currentInterval = 0;

			var streams = Enumerable.Range(0, streamsCnt).Select(x => Guid.NewGuid().ToString()).ToArray();
			var start = new TaskCompletionSource();
			var sw2 = new Stopwatch();
			var capacity = 2000 / clientsCnt;
			var clientTasks = new List<Task>();
			for (int i = 0; i < clientsCnt; i++) {
				var count = requestsCnt / clientsCnt + ((i == clientsCnt - 1) ? requestsCnt % clientsCnt : 0);

				var client = new SimpleClient();
				clientTasks.Add(RunClient(client, count));
			}

			async Task RunClient(SimpleClient client, long count) {
				var rnd = new Random();
				List<Task> pending = new List<Task>(capacity);
				await start.Task;
				for (int j = 0; j < count; ++j) {

					var events = new EventData[batchSize];
					for (int q = 0; q < batchSize; q++) {
						events[q] = new EventData(Uuid.FromGuid(Guid.NewGuid()),
							"TakeSomeSpaceEvent",
							UTF8NoBom.GetBytes(
								"{ \"DATA\" : \"" + new string('*', size) + "\"}"),
							UTF8NoBom.GetBytes(
								"{ \"METADATA\" : \"" + new string('$', 100) + "\"}"), contentType: Constants.Metadata.ContentTypes.ApplicationOctetStream);
					}

					var corrid = Guid.NewGuid();
					monitor.StartOperation(corrid);

					pending.Add(client.AppendRequestStreamed(streams[rnd.Next(streamsCnt)], events)
						.ContinueWith(t => {
							if (t.IsCompletedSuccessfully) Interlocked.Increment(ref succ);
							else {
								if (Interlocked.Increment(ref fail) % 1000 == 0)
									Console.Write('#');
							}
							var localAll = Interlocked.Add(ref all, batchSize);
							if (localAll - currentInterval > interval) {
								var localInterval = Interlocked.Exchange(ref currentInterval, localAll);
								var elapsed = sw2.Elapsed;
								sw2.Restart();
								context.Log.Information(
									"\nDONE TOTAL {writes} WRITES IN {elapsed} ({rate:0.0}/s) [S:{success}, F:{failures} (WEV:{wrongExpectedVersion}, P:{prepareTimeout}, C:{commitTimeout}, F:{forwardTimeout}, D:{streamDeleted})].",
									localAll, elapsed, 1000.0 * (localAll - localInterval) / elapsed.TotalMilliseconds,
									succ, fail,
									wrongExpVersion, prepTimeout, commitTimeout, forwardTimeout, streamDeleted);
							}

							monitor.EndOperation(corrid);
						}));
					if (pending.Count == capacity) {
						await Task.WhenAny(pending).ConfigureAwait(false);

						while (pending.Count > 0 && Task.WhenAny(pending).IsCompleted) {
							pending.RemoveAll(x => x.IsCompleted);
							if (succ - last > 1000) {
								Console.Write(".");
								last = succ;
							}
						}
					}
				}

				if (pending.Count > 0) await Task.WhenAll(pending);
			}

			var sw = Stopwatch.StartNew();
			sw2.Start();
			start.SetResult();
			await Task.WhenAll(clientTasks);
			sw.Stop();

			context.Log.Information(
				"Completed. Successes: {success}, failures: {failures} (WRONG VERSION: {wrongExpectedVersion}, P: {prepareTimeout}, C: {commitTimeout}, F: {forwardTimeout}, D: {streamDeleted})",
				succ, fail,
				wrongExpVersion, prepTimeout, commitTimeout, forwardTimeout, streamDeleted);

			var reqPerSec = (all + 0.0) / sw.ElapsedMilliseconds * 1000;
			context.Log.Information("{requests} requests completed in {elapsed}ms ({rate:0.00} reqs per sec).", all,
				sw.ElapsedMilliseconds, reqPerSec);

			PerfUtils.LogData(
				Keyword,
				PerfUtils.Row(PerfUtils.Col("clientsCnt", clientsCnt),
					PerfUtils.Col("requestsCnt", requestsCnt),
					PerfUtils.Col("ElapsedMilliseconds", sw.ElapsedMilliseconds)),
				PerfUtils.Row(PerfUtils.Col("successes", succ), PerfUtils.Col("failures", fail)));

			var failuresRate = (int)(100 * fail / (fail + succ));
			PerfUtils.LogTeamCityGraphData(string.Format("{0}-{1}-{2}-reqPerSec", Keyword, clientsCnt, requestsCnt),
				(int)reqPerSec);
			PerfUtils.LogTeamCityGraphData(
				string.Format("{0}-{1}-{2}-failureSuccessRate", Keyword, clientsCnt, requestsCnt), failuresRate);
			PerfUtils.LogTeamCityGraphData(
				string.Format("{0}-c{1}-r{2}-st{3}-s{4}-reqPerSec", Keyword, clientsCnt, requestsCnt, streamsCnt, size),
				(int)reqPerSec);
			PerfUtils.LogTeamCityGraphData(
				string.Format("{0}-c{1}-r{2}-st{3}-s{4}-failureSuccessRate", Keyword, clientsCnt, requestsCnt,
					streamsCnt, size), failuresRate);
			monitor.GetMeasurementDetails();
			if (Interlocked.Read(ref succ) != requestsCnt)
				context.Fail(reason: "There were errors or not all requests completed.");
			else
				context.Success();
		}
	}

	
    class SimpleClient : IDisposable
    {
        private readonly ConcurrentDictionary<Uuid, TaskCompletionSource> _pendingRequests;
        private readonly ChannelWriter<AppendReq2> _outbound;
        private Streams.StreamsClient _client;


        public SimpleClient()
        {
            var handler = new SocketsHttpHandler
            {
                PooledConnectionIdleTimeout = Timeout.InfiniteTimeSpan,
                KeepAlivePingDelay = TimeSpan.FromSeconds(60),
                KeepAlivePingTimeout = TimeSpan.FromSeconds(30),
                EnableMultipleHttp2Connections = true
            };
            _pendingRequests = new ConcurrentDictionary<Uuid, TaskCompletionSource>();
            var channel = GrpcChannel.ForAddress("http://localhost:2113", new GrpcChannelOptions
            {
                HttpHandler = handler
            });
            _client = new Streams.StreamsClient(channel);
            var queue = Channel.CreateBounded<AppendReq2>(10000);
            var append = _client.Append2();
            _outbound = queue.Writer;
            RunOutbound(queue.Reader, append.RequestStream);
            RunInbound(append.ResponseStream);
        }

        private async void RunInbound(IAsyncStreamReader<AppendResp2> responseStream)
        {
            try
            {
                await foreach (var r in responseStream.ReadAllAsync())
                {
                    if (_pendingRequests.TryRemove(Uuid.FromDto(r.Correlation), out var completion))
                    {
						if(r.ResultCase == AppendResp2.ResultOneofCase.Success)
                            completion.TrySetResult();
						else if (r.ResultCase == AppendResp2.ResultOneofCase.WrongExpectedVersion)
                        {
                            completion.TrySetException(new Exception("Wrong expected version"));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                foreach (var kvp in _pendingRequests)
                {
                    kvp.Value.TrySetException(ex);
                }
            }
        }

        private static readonly Empty _empty = new Empty();
        public async Task AppendRequestStreamed(string stream, IEnumerable<EventData> events)
        {
            var cr = Uuid.NewUuid().ToDto();
            var cts = new TaskCompletionSource();
            _pendingRequests.TryAdd(Uuid.FromDto(cr), cts);
            var appendReq = new AppendReq2() {Correlation = cr};
            
            var batch = new AppendReq2.Types.Batch
            {
                Options = new AppendReq2.Types.Options()
                {
                    Any = _empty,
                    StreamIdentifier = new StreamIdentifier() {StreamName = ByteString.CopyFromUtf8(stream)}
                }
            };
            appendReq.Batch = batch;
            foreach (var e in events)
            {
                batch.ProposedMessages.Add(
                    new AppendReq2.Types.ProposedMessage()
                    {
                        Id = e.EventId.ToDto(),
                        Metadata = {{Constants.Metadata.Type, e.Type}, {Constants.Metadata.ContentType, e.ContentType}},
                        Data = ByteString.CopyFrom(e.Data.Span),
                        CustomMetadata = ByteString.CopyFrom(e.Metadata.Span)
                    });
                if (batch.CalculateSize() > 3 * 1024 * 1024)
                {
                    await _outbound.WriteAsync(appendReq);
                    batch = new AppendReq2.Types.Batch();
                    appendReq = new AppendReq2() {Correlation = cr, Batch = batch};
                }
            }

            batch.IsFinal = true;
            
            await _outbound.WriteAsync(appendReq);
            await cts.Task;

        }

        async void RunOutbound(ChannelReader<AppendReq2> channelReader,
            IClientStreamWriter<AppendReq2> requestStream)
        {
            try
            {
                await foreach (var r in channelReader.ReadAllAsync())
                {
                    await requestStream.WriteAsync(r);
                }

                await requestStream.CompleteAsync();
            }
            catch (Exception ex)
            {
                foreach (var kvp in _pendingRequests)
                {
                    kvp.Value.TrySetException(ex);
                }
            }
        }

        public void Dispose()
        {
            _outbound.TryComplete();
        }
    }

    public sealed class EventData {
		/// <summary>
		/// The raw bytes of the event data.
		/// </summary>
		public readonly ReadOnlyMemory<byte> Data;

		/// <summary>
		/// The <see cref="Uuid"/> of the event, used as part of the idempotent write check.
		/// </summary>
		public readonly Uuid EventId;

		/// <summary>
		/// The raw bytes of the event metadata.
		/// </summary>
		public readonly ReadOnlyMemory<byte> Metadata;

		/// <summary>
		/// The name of the event type. It is strongly recommended that these
		/// use lowerCamelCase if projections are to be used.
		/// </summary>
		public readonly string Type;

		/// <summary>
		/// The Content-Type of the <see cref="Data"/>. Valid values are 'application/json' and 'application/octet-stream'.
		/// </summary>
		public readonly string ContentType;

		/// <summary>
		/// Constructs a new <see cref="EventData"/>.
		/// </summary>
		/// <param name="eventId">The <see cref="Uuid"/> of the event, used as part of the idempotent write check.</param>
		/// <param name="type">The name of the event type. It is strongly recommended that these use lowerCamelCase if projections are to be used.</param>
		/// <param name="data">The raw bytes of the event data.</param>
		/// <param name="metadata">The raw bytes of the event metadata.</param>
		/// <param name="contentType">The Content-Type of the <see cref="Data"/>. Valid values are 'application/json' and 'application/octet-stream'.</param>
		/// <exception cref="ArgumentOutOfRangeException"></exception>
		public EventData(Uuid eventId, string type, ReadOnlyMemory<byte> data, ReadOnlyMemory<byte>? metadata = null,
			string contentType = Constants.Metadata.ContentTypes.ApplicationJson) {
			if (eventId == Uuid.Empty) {
				throw new ArgumentOutOfRangeException(nameof(eventId));
			}

			MediaTypeHeaderValue.Parse(contentType);

			if (contentType != Constants.Metadata.ContentTypes.ApplicationJson &&
			    contentType != Constants.Metadata.ContentTypes.ApplicationOctetStream) {
				throw new ArgumentOutOfRangeException(nameof(contentType), contentType,
					$"Only {Constants.Metadata.ContentTypes.ApplicationJson} or {Constants.Metadata.ContentTypes.ApplicationOctetStream} are acceptable values.");
			}

			EventId = eventId;
			Type = type;
			Data = data;
			Metadata = metadata ?? Array.Empty<byte>();
			ContentType = contentType;
		}
	}

    /// <summary>
	/// An RFC-4122 compliant v4 UUID.
	/// </summary>
	public readonly struct Uuid : IEquatable<Uuid> {
		/// <summary>
		/// Represents the empty (00000000-0000-0000-0000-000000000000) <see cref="Uuid"/>.
		/// </summary>
		/// <remarks>
		/// This reorders the bits in System.Guid to improve interop with other languages. See: https://stackoverflow.com/a/16722909
		/// </remarks>
		public static readonly Uuid Empty = new Uuid(Guid.Empty);

		private readonly long _lsb;
		private readonly long _msb;

		/// <summary>
		/// Creates a new, randomized <see cref="Uuid"/>.
		/// </summary>
		/// <returns><see cref="Uuid"/></returns>
		public static Uuid NewUuid() => new Uuid(Guid.NewGuid());

		/// <summary>
		/// Converts a <see cref="Guid"/> to a <see cref="Uuid"/>.
		/// </summary>
		/// <param name="value"></param>
		/// <returns><see cref="Uuid"/></returns>
		public static Uuid FromGuid(Guid value) => new Uuid(value);

		/// <summary>
		/// Parses a <see cref="string"/> into a <see cref="Uuid"/>.
		/// </summary>
		/// <param name="value"></param>
		/// <returns><see cref="Uuid"/></returns>
		public static Uuid Parse(string value) => new Uuid(value);

		/// <summary>
		/// Creates a <see cref="Uuid"/> from a pair of <see cref="long"/>.
		/// </summary>
		/// <param name="msb">The <see cref="long"/> representing the most significant bits.</param>
		/// <param name="lsb">The <see cref="long"/> representing the least significant bits.</param>
		/// <returns></returns>
		public static Uuid FromInt64(long msb, long lsb) => new Uuid(msb, lsb);

		/// <summary>
		/// Creates a <see cref="Uuid"/> from the gRPC wire format.
		/// </summary>
		/// <param name="dto"></param>
		/// <returns><see cref="Uuid"/></returns>
		public static Uuid FromDto(UUID dto) =>
			dto == null
				? throw new ArgumentNullException(nameof(dto))
				: dto.ValueCase switch {
					UUID.ValueOneofCase.String => new Uuid(dto.String),
					UUID.ValueOneofCase.Structured => new Uuid(dto.Structured.MostSignificantBits,
						dto.Structured.LeastSignificantBits),
					_ => throw new ArgumentException($"Invalid argument: {dto.ValueCase}", nameof(dto))
				};

		private Uuid(Guid value) {
			if (!BitConverter.IsLittleEndian) {
				throw new NotSupportedException();
			}

#if NETFRAMEWORK
			var data = value.ToByteArray();

			Array.Reverse(data, 0, 8);
			Array.Reverse(data, 0, 2);
			Array.Reverse(data, 2, 2);
			Array.Reverse(data, 4, 4);
			Array.Reverse(data, 8, 8);

			_msb = BitConverter.ToInt64(data, 0);
			_lsb = BitConverter.ToInt64(data, 8);
#else
			Span<byte> data = stackalloc byte[16];

			if (!value.TryWriteBytes(data)) {
				throw new InvalidOperationException();
			}

			data.Slice(0, 8).Reverse();
			data.Slice(0, 2).Reverse();
			data.Slice(2, 2).Reverse();
			data.Slice(4, 4).Reverse();
			data.Slice(8).Reverse();

			_msb = BitConverter.ToInt64(data);
			_lsb = BitConverter.ToInt64(data.Slice(8));
#endif
		}

		private Uuid(string value) : this(value == null
			? throw new ArgumentNullException(nameof(value))
			: Guid.Parse(value)) {
		}

		private Uuid(long msb, long lsb) {
			_msb = msb;
			_lsb = lsb;
		}

		/// <summary>
		/// Converts the <see cref="Uuid"/> to the gRPC wire format.
		/// </summary>
		/// <returns><see cref="UUID"/></returns>
		public UUID ToDto() =>
			new UUID {
				Structured = new UUID.Types.Structured {
					LeastSignificantBits = _lsb,
					MostSignificantBits = _msb
				}
			};


		/// <inheritdoc />
		public bool Equals(Uuid other) => _lsb == other._lsb && _msb == other._msb;

		/// <inheritdoc />
		public override bool Equals(object? obj) => obj is Uuid other && Equals(other);

		/// <inheritdoc />
		public override int GetHashCode() => HashCode.Combine(_lsb,_msb);

		/// <summary>
		/// Compares left and right for equality.
		/// </summary>
		/// <param name="left">A <see cref="Uuid"/></param>
		/// <param name="right">A <see cref="Uuid"/></param>
		/// <returns>True if left is equal to right.</returns>
		public static bool operator ==(Uuid left, Uuid right) => left.Equals(right);

		/// <summary>
		/// Compares left and right for inequality.
		/// </summary>
		/// <param name="left">A <see cref="Uuid"/></param>
		/// <param name="right">A <see cref="Uuid"/></param>
		/// <returns>True if left is not equal to right.</returns>
		public static bool operator !=(Uuid left, Uuid right) => !left.Equals(right);

		/// <inheritdoc />
		public override string ToString() => ToGuid().ToString();

		/// <summary>
		/// Converts the <see cref="Uuid"/> to a <see cref="string"/> based on the supplied format.
		/// </summary>
		/// <param name="format"></param>
		/// <returns><see cref="string"/></returns>
		public string ToString(string format) => ToGuid().ToString(format);

		/// <summary>
		/// Converts the <see cref="Uuid"/> to a <see cref="Guid"/>.
		/// </summary>
		/// <returns><see cref="Guid"/></returns>
		public Guid ToGuid() {
			if (!BitConverter.IsLittleEndian) {
				throw new NotSupportedException();
			}
#if NETFRAMEWORK
			var msb = BitConverter.GetBytes(_msb);
			Array.Reverse(msb, 0, 8);
			Array.Reverse(msb, 0, 4);
			Array.Reverse(msb, 4, 2);
			Array.Reverse(msb, 6, 2);

			var lsb = BitConverter.GetBytes(_lsb);
			Array.Reverse(lsb);

			var data = new byte[16];
			Array.Copy(msb, data, 8);
			Array.Copy(lsb, 0, data, 8, 8);
			return new Guid(data);
#else
			Span<byte> data = stackalloc byte[16];
			if (!BitConverter.TryWriteBytes(data, _msb) ||
			    !BitConverter.TryWriteBytes(data.Slice(8), _lsb)) {
				throw new InvalidOperationException();
			}

			data.Slice(0, 8).Reverse();
			data.Slice(0, 4).Reverse();
			data.Slice(4, 2).Reverse();
			data.Slice(6, 2).Reverse();
			data.Slice(8).Reverse();

			return new Guid(data);

#endif
		}
	}

	internal static class Constants {
		public static class Exceptions {
			public const string ExceptionKey = "exception";

			public const string AccessDenied = "access-denied";
			public const string InvalidTransaction = "invalid-transaction";
			public const string StreamDeleted = "stream-deleted";
			public const string WrongExpectedVersion = "wrong-expected-version";
			public const string StreamNotFound = "stream-not-found";
			public const string MaximumAppendSizeExceeded = "maximum-append-size-exceeded";
			public const string MissingRequiredMetadataProperty = "missing-required-metadata-property";
			public const string NotLeader = "not-leader";

			public const string PersistentSubscriptionFailed = "persistent-subscription-failed";
			public const string PersistentSubscriptionDoesNotExist = "persistent-subscription-does-not-exist";
			public const string PersistentSubscriptionExists = "persistent-subscription-exists";
			public const string MaximumSubscribersReached = "maximum-subscribers-reached";
			public const string PersistentSubscriptionDropped = "persistent-subscription-dropped";

			public const string UserNotFound = "user-not-found";
			public const string UserConflict = "user-conflict";

			public const string ScavengeNotFound = "scavenge-not-found";

			public const string ExpectedVersion = "expected-version";
			public const string ActualVersion = "actual-version";
			public const string StreamName = "stream-name";
			public const string GroupName = "group-name";
			public const string Reason = "reason";
			public const string MaximumAppendSize = "maximum-append-size";
			public const string RequiredMetadataProperties = "required-metadata-properties";
			public const string ScavengeId = "scavenge-id";
			public const string LeaderEndpointHost = "leader-endpoint-host";
			public const string LeaderEndpointPort = "leader-endpoint-port";

			public const string LoginName = "login-name";
		}

		public static class Metadata {
			public const string Type = "type";
			public const string Created = "created";
			public const string ContentType = "content-type";
			public static readonly string[] RequiredMetadata = {Type, ContentType};

			public static class ContentTypes {
				public const string ApplicationJson = "application/json";
				public const string ApplicationOctetStream = "application/octet-stream";
			}
		}

		public static class Headers {
			public const string Authorization = "authorization";
			public const string BasicScheme = "Basic";
			public const string BearerScheme = "Bearer";

			public const string ConnectionName = "connection-name";
			public const string RequiresLeader = "requires-leader";
		}
	}
}
