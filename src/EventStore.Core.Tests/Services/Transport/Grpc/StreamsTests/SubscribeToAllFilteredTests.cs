using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EventStore.Client.Streams;
using EventStore.Core.Services.Transport.Grpc;
using Google.Protobuf;
using Grpc.Core;
using NUnit.Framework;
using Position = EventStore.Core.Services.Transport.Grpc.Position;
using LogV3StreamId = System.UInt32;

namespace EventStore.Core.Tests.Services.Transport.Grpc.StreamsTests {
	[TestFixture]
	public class SubscribeToAllFilteredTests {
		public static IEnumerable<object[]> TestCases() {
			var checkpointIntervalMultipliers = new uint[] { 2, 4, 8 };

			var maxSearchWindows = new uint[] { 1, 32, 64 };

			var filteredEventCount = checkpointIntervalMultipliers.Max() * maxSearchWindows.Max();

			var logFormats = new[] {
				(typeof(LogFormat.V2), typeof(string)),
				(typeof(LogFormat.V3), typeof(LogV3StreamId)),
			};

			return from checkpointInterval in checkpointIntervalMultipliers
				from maxSearchWindow in maxSearchWindows
				from logFormat in logFormats
				select new object[] {
					logFormat.Item1,
					logFormat.Item2,
					checkpointInterval,
					maxSearchWindow,
					(int)filteredEventCount
				};
		}

		[TestFixtureSource(typeof(SubscribeToAllFilteredTests), nameof(TestCases))]
		public class when_subscribing_to_all_with_a_filter<TLogFormat, TStreamId>
			: GrpcSpecification<TLogFormat, TStreamId> {
			private const string StreamName = "test";

			private int CheckpointCount => _positions.Count;

			private readonly uint _maxSearchWindow;
			private readonly int _filteredEventCount;
			private readonly uint _checkpointIntervalMultiplier;
			private readonly List<Position> _positions;
			private readonly uint _checkpointInterval;

			private Position _position;
			private long _expected;

			public when_subscribing_to_all_with_a_filter(uint checkpointIntervalMultiplier, uint maxSearchWindow,
				int filteredEventCount)
				: base (new LotsOfExpiriesStrategy()) {
				_maxSearchWindow = maxSearchWindow;
				_checkpointIntervalMultiplier = checkpointIntervalMultiplier;
				_checkpointInterval = checkpointIntervalMultiplier * maxSearchWindow;
				_filteredEventCount = filteredEventCount;
				_positions = new List<Position>();
				_position = Position.End;
			}

			protected override async Task Given() {
				await AppendToStreamBatch(new BatchAppendReq {
					Options = new() {
						Any = new(),
						StreamIdentifier = new() { StreamName = ByteString.CopyFromUtf8("abcd") }
					},
					IsFinal = true,
					ProposedMessages = { CreateEvents(_filteredEventCount) },
					CorrelationId = Uuid.NewUuid().ToDto()
				});
			}

			protected override async Task When() {
				var success = (await AppendToStreamBatch(new BatchAppendReq {
					Options = new() {
						Any = new(),
						StreamIdentifier = new() { StreamName = ByteString.CopyFromUtf8(StreamName) }
					},
					IsFinal = true,
					ProposedMessages = { CreateEvents(1) },
					CorrelationId = Uuid.NewUuid().ToDto()
				})).Success;

				_position = new Position(success.Position.CommitPosition, success.Position.PreparePosition);

				using var skippedEventCall = StreamsClient.Read(new ReadReq {
					Options = new ReadReq.Types.Options {
						Count = 4096,
						All = new() { Start = new() },
						UuidOption = new() { Structured = new() },
						ReadDirection = ReadReq.Types.Options.Types.ReadDirection.Forwards,
						NoFilter = new()
					}
				}, GetCallOptions(AdminCredentials));
				var skippedEventCount = await skippedEventCall.ResponseStream.ReadAllAsync().CountAsync(response =>
					response.Event is not null &&
					new Position(response.Event.Event.CommitPosition, response.Event.Event.PreparePosition) <=
					_position &&
					response.Event.Event.StreamIdentifier.StreamName.ToStringUtf8() != StreamName);

				// skip the epochinfo - which isn't in unfiltered all reads so we haven't accounted for it above
				skippedEventCount += 1;
				_expected = skippedEventCount / _checkpointInterval;

				using var call = StreamsClient.Read(new ReadReq {
					Options = new ReadReq.Types.Options {
						Subscription = new(),
						All = new() { Start = new() },
						Filter = new() {
							Max = _maxSearchWindow,
							CheckpointIntervalMultiplier = _checkpointIntervalMultiplier,
							StreamIdentifier = new() { Prefix = { StreamName } }
						},
						UuidOption = new() { Structured = new() },
						ReadDirection = ReadReq.Types.Options.Types.ReadDirection.Forwards
					}
				}, GetCallOptions(AdminCredentials));

				var receivedTheEvent = false;
				await foreach (var response in call.ResponseStream.ReadAllAsync()) {
					if (response.ContentCase == ReadResp.ContentOneofCase.CaughtUp) {
						// we have successfully transitioned to live (and will receive no more events)
						break;
					}

					if (response.ContentCase == ReadResp.ContentOneofCase.Checkpoint) {
						if (receivedTheEvent) {
							break;
						}
						_positions.Add(new Position(response.Checkpoint.CommitPosition,
							response.Checkpoint.PreparePosition));
						continue;
					}

					if (response.ContentCase == ReadResp.ContentOneofCase.Event) {
						Assert.AreEqual(StreamName, response.Event.Event.StreamIdentifier.StreamName.ToStringUtf8());
						receivedTheEvent = true;
					}
				}
			}

			[Test]
			public void receives_the_correct_number_of_checkpoints() {
				Assert.AreEqual(_expected, CheckpointCount);
			}

			[Test]
			public void no_duplicate_checkpoints_received() {
				Assert.AreEqual(_positions.Distinct().Count(), _positions.Count);
			}
		}

		[TestFixtureSource(typeof(SubscribeToAllFilteredTests), nameof(TestCases))]
		public class when_subscribing_to_all_with_a_filter_live<TLogFormat, TStreamId>
			: GrpcSpecification<TLogFormat, TStreamId> {
			private const string StreamName = "test";

			private int CheckpointCount => _positions.Count;

			private readonly uint _maxSearchWindow;
			private readonly int _filteredEventCount;
			private readonly uint _checkpointIntervalMultiplier;
			private readonly List<Position> _positions;
			private readonly uint _checkpointInterval;

			private Position _position;

			public when_subscribing_to_all_with_a_filter_live(uint checkpointIntervalMultiplier, uint maxSearchWindow,
				int filteredEventCount) {
				_maxSearchWindow = maxSearchWindow;
				_checkpointIntervalMultiplier = checkpointIntervalMultiplier;
				_checkpointInterval = checkpointIntervalMultiplier * maxSearchWindow;
				_filteredEventCount = filteredEventCount;
				_positions = new List<Position>();
				_position = Position.End;
			}

			protected override async Task Given() {
				await AppendToStreamBatch(new BatchAppendReq {
					Options = new() {
						Any = new(),
						StreamIdentifier = new() {StreamName = ByteString.CopyFromUtf8("abcd")}
					},
					IsFinal = true,
					ProposedMessages = {CreateEvents(_filteredEventCount)},
					CorrelationId = Uuid.NewUuid().ToDto()
				});
			}

			protected override async Task When() {
				for (int i = 1; i <= 5; i++) {
					try {
						await WhenWhichMightConsumeTooSlow(i);
						return;
					} catch (RpcException ex) when(ex.Message.Contains("too slow")) {
						_positions.Clear();
						await Task.Delay(500);
					}
				}
			}

			private async Task WhenWhichMightConsumeTooSlow(int attempt) {
				var streamName = attempt + StreamName;
				using var call = StreamsClient.Read(new ReadReq {
					Options = new ReadReq.Types.Options {
						Subscription = new(),
						All = new() {End = new()},
						Filter = new() {
							Max = _maxSearchWindow,
							CheckpointIntervalMultiplier = _checkpointIntervalMultiplier,
							StreamIdentifier = new() {Prefix = {streamName}}
						},
						UuidOption = new() {Structured = new()},
						ReadDirection = ReadReq.Types.Options.Types.ReadDirection.Forwards
					}
				}, GetCallOptions(AdminCredentials));

				Assert.True(await call.ResponseStream.MoveNext());

				Assert.AreEqual(ReadResp.ContentOneofCase.Confirmation, call.ResponseStream.Current.ContentCase);

				var success = new BatchAppendResp.Types.Success();
				for (int i = 0; i <= _checkpointInterval ; i++) {
					success = (await AppendToStreamBatch(new BatchAppendReq {
						Options = new() {
							Any = new(),
							StreamIdentifier = new() {StreamName = ByteString.CopyFromUtf8(streamName)}
						},
						IsFinal = true,
						ProposedMessages = {CreateEvents(1)},
						CorrelationId = Uuid.NewUuid().ToDto()
					})).Success;
				}
				
				_position = new Position(success.Position.CommitPosition, success.Position.PreparePosition);

				while (await call.ResponseStream.MoveNext()) {
					var response = call.ResponseStream.Current;
					if (response.ContentCase == ReadResp.ContentOneofCase.Checkpoint) {
						_positions.Add(new Position(response.Checkpoint.CommitPosition,
							response.Checkpoint.PreparePosition));
						continue;
					}

					if (response.ContentCase == ReadResp.ContentOneofCase.Event) {
						Assert.AreEqual(streamName,
							response.Event.Event.StreamIdentifier.StreamName.ToStringUtf8());
						if (!(response.Event.CommitPosition < _position.CommitPosition)) {
							return;
						}
					}
				}
			}

			[Test]
			public void receives_the_correct_number_of_checkpoints() {
				Assert.AreEqual(1, CheckpointCount);
			}

			[Test]
			public void no_duplicate_checkpoints_received() {
				Assert.AreEqual(_positions.Distinct().Count(), _positions.Count);
			}

			[Test]
			public void checkpoint_is_before_last_written_event() {
				Assert.True(_positions[0] < _position);
			}
		}
	}
}
