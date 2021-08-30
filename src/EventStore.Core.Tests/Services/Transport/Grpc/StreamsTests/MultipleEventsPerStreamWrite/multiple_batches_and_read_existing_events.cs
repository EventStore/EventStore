using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EventStore.Client;
using EventStore.Client.Streams;
using EventStore.Core.Services.Transport.Grpc;
using Google.Protobuf;
using Grpc.Core;
using NUnit.Framework;
using ReadDirection = EventStore.Client.Streams.ReadReq.Types.Options.Types.ReadDirection;

// these tests are particularly important for Log V3 which supports having multiple events per stream write record

namespace EventStore.Core.Tests.Services.Transport.Grpc.StreamsTests.MultipleEventsPerStreamWrite {
	[TestFixture(typeof(LogFormat.V2), typeof(string), 1)]
	[TestFixture(typeof(LogFormat.V2), typeof(string), NumEvents / 5)]
	[TestFixture(typeof(LogFormat.V2), typeof(string), NumEvents)]
	[TestFixture(typeof(LogFormat.V3), typeof(uint), 1)]
	[TestFixture(typeof(LogFormat.V3), typeof(uint), NumEvents / 5)]
	[TestFixture(typeof(LogFormat.V3), typeof(uint), NumEvents)]
	public class multiple_batches_and_read_existing_events<TLogFormat, TStreamId> : GrpcSpecification<TLogFormat, TStreamId> {
		private const string Stream = "stream";
		private const int NumEvents = 42;
		private readonly ReadDirection[] _readDirections = {ReadDirection.Forwards, ReadDirection.Backwards};
		private readonly ulong[] _maxCounts = {1U, 2U, 7U, 12U, 25U, (ulong)NumEvents - 1, NumEvents, (ulong)NumEvents + 1, ulong.MaxValue};
		private readonly Random _random = new();
		private readonly int _numBatches;
		private readonly (ulong commit, ulong prepare)[] _prePos = new (ulong commit, ulong prepare)[NumEvents];
		private readonly (ulong commit, ulong prepare)[] _postPos = new (ulong commit, ulong prepare)[NumEvents];
		private readonly BatchAppendReq.Types.ProposedMessage[] _events;
		private readonly int[] _batchSizes;

		public multiple_batches_and_read_existing_events(int numBatches) {
			Assert.Positive(numBatches);
			_numBatches = numBatches;
			_events = CreateEvents(NumEvents).ToArray();
			_batchSizes = GenerateRandomBatchSizes(NumEvents, _numBatches);
		}

		protected override Task Given() => Task.CompletedTask;
		protected override async Task When() {
			await AppendBatches();
			await PopulateEventPositions();
		}

		private async Task AppendBatches() {
			Assert.LessOrEqual(_numBatches, NumEvents);
			var sum = 0;
			foreach (var batchSize in _batchSizes) {
				await AppendToStreamBatch(new BatchAppendReq {
					Options = new() {
						Any = new(),
						StreamIdentifier = new() {StreamName = ByteString.CopyFromUtf8(Stream)}
					},
					IsFinal = true,
					ProposedMessages = { _events.Skip(sum).Take(batchSize) },
					CorrelationId = Uuid.NewUuid().ToDto()
				});
				sum += batchSize;

				//flip a coin and, if heads, randomly introduce some dummy events between batches
				if (_random.Next(2) == 0 || sum == NumEvents /*always append some dummy events at the end to be able to get the post position of events*/) {
					var numDummyEvents = _random.Next(10) + 1;
					for (int i = 0; i < numDummyEvents; i++) {
						await AppendToStreamBatch(new BatchAppendReq {
							Options = new() {
								Any = new(),
								StreamIdentifier = new() {StreamName = ByteString.CopyFromUtf8("dummy")}
							},
							IsFinal = true,
							ProposedMessages = { CreateEvents(1) },
							CorrelationId = Uuid.NewUuid().ToDto()
						});
					}
				}
			}
			Assert.AreEqual(NumEvents, sum);
		}

		private int[] GenerateRandomBatchSizes(int numItems, int numBatches) {
			var remPos = new List<int>();
			for (int i = 1; i < numItems; i++) {
				remPos.Add(i);
			}

			var result = new List<int>();
			for (int i = 0; i < numBatches - 1; i++) {
				var selection = remPos[_random.Next(remPos.Count)];
				result.Add(selection);
				remPos.Remove(selection);
			}
			result.Sort();
			result.Add(numItems);
			var batchSizes = new int[numBatches];
			var currentBatch = 0;
			for (int i = 0; i < numBatches; i++) {
				batchSizes[i] = result[i] - currentBatch;
				currentBatch = result[i];
			}

			return batchSizes;
		}

		[Test]
		public async Task can_read_events_from_stream() {
			for (int fromEventNumber = 0; fromEventNumber < NumEvents; fromEventNumber++) {
				foreach (var readDirection in _readDirections) {
					foreach (var maxCount in _maxCounts) {
						await ReadEventsFromStream(fromEventNumber, readDirection, maxCount);
					}
				}
			}
		}

		[Test]
		public async Task can_read_events_from_all() {
			for (int fromEventNumber = 0; fromEventNumber < NumEvents; fromEventNumber++) {
				foreach (var readDirection in _readDirections) {
					foreach (var maxCount in _maxCounts) {
						await ReadEventsFromAll(fromEventNumber, readDirection, maxCount);
					}
				}
			}
		}

		private async Task ReadEventsFromStream(int fromEventNumber,ReadDirection readDirection, ulong maxCount) {
			using var call = StreamsClient.Read(new ReadReq {
				Options = new() {
					Count = maxCount,
					Stream = new() {
						StreamIdentifier = new() { StreamName = ByteString.CopyFromUtf8(Stream) },
						Revision = (ulong) fromEventNumber
					},
					ReadDirection = readDirection,
					UuidOption = new() { Structured = new() },
					NoFilter = new()
				}
			});
			var responses = await call.ResponseStream.ReadAllAsync().Where(x => x.Event is not null).ToArrayAsync();

			int start = fromEventNumber;
			int end;
			if (readDirection == ReadDirection.Forwards) {
				end = maxCount == ulong.MaxValue ? NumEvents - 1 : Math.Min(fromEventNumber + (int)maxCount - 1, NumEvents - 1);
			} else {
				end = maxCount == ulong.MaxValue ? 0: Math.Max(fromEventNumber - (int)maxCount + 1, 0);
			}

			if (end < start) {
				int tmp = start;
				start = end;
				end = tmp;
				responses = responses.Reverse().ToArray();
			}

			var testCase = $"Test case: read from stream, batchSizes: [{string.Join(",", _batchSizes)}], fromEventNumber: {fromEventNumber}, readDirection: {(readDirection)}, maxCount: {maxCount}";

			Assert.AreEqual(end-start+1, responses.Length, testCase);
			for (int i = start; i <= end; i++) {
				Assert.AreEqual(_events[i].Id, responses[i-start].Event.Event.Id, testCase);
			}

			if (maxCount == ulong.MaxValue) {
				Assert.True(start == 0 || end == NumEvents - 1);
			} else {
				Assert.LessOrEqual(responses.Length, (int)maxCount, testCase);
			}
		}

		private async Task ReadEventsFromAll(int fromEventNumber, ReadDirection readDirection, ulong maxCount) {
			var pos = (readDirection == ReadDirection.Forwards) ? _prePos[fromEventNumber] : _postPos[fromEventNumber];
			using var call = StreamsClient.Read(new ReadReq {
				Options = new() {
					Count = maxCount,
					All = new () {
						Position = new() {
							CommitPosition = pos.commit,
							PreparePosition = pos.prepare
						}
					},
					ReadDirection = readDirection,
					UuidOption = new() { Structured = new() },
					NoFilter = new Empty()
				}
			},  GetCallOptions(AdminCredentials));

			var responses = await call.ResponseStream.ReadAllAsync().Where(x => x.Event is not null).ToArrayAsync();
			var testCase = $"Test case: read from $all, batchSizes: [{string.Join(",", _batchSizes)}], fromEventNumber: {fromEventNumber}, readDirection: {readDirection}, maxCount: {maxCount}";

			var eventIndex = fromEventNumber;
			for (int i = 0; i < responses.Length; i++) {
				if (responses[i].Event.Event.StreamIdentifier != Stream)
					continue;
				Assert.AreEqual(_events[eventIndex].Id, responses[i].Event.Event.Id, testCase);
				if (readDirection == ReadDirection.Forwards) eventIndex++;
				else eventIndex--;
			}

			if (maxCount == ulong.MaxValue) {
				Assert.True(eventIndex < 0 || eventIndex > NumEvents - 1, testCase);
			} else {
				Assert.LessOrEqual(responses.Length, (int)maxCount);
				if (readDirection == ReadDirection.Forwards && fromEventNumber + (int)maxCount - 1 < NumEvents) {
					Assert.AreEqual((int)maxCount, responses.Length, testCase);
				}
				if (readDirection == ReadDirection.Backwards && fromEventNumber - (int)maxCount + 1 >= 0) {
					Assert.AreEqual((int)maxCount, responses.Length, testCase);
				}
			}
		}

		private async Task PopulateEventPositions() {
			using var call = StreamsClient.Read(new ReadReq {
				Options = new() {
					Count = ulong.MaxValue,
					All = new () {
						Start = new Empty()
					},
					ReadDirection = ReadDirection.Forwards,
					UuidOption = new() { Structured = new() },
					NoFilter = new()
				}
			},  GetCallOptions(AdminCredentials));
			var responses = await call.ResponseStream.ReadAllAsync().Where(x => x.Event is not null).ToArrayAsync();
			var eventIndex = 0;
			for (int i=0;i<responses.Length;i++) {
				var curEvent = responses[i].Event.Event;
				if (curEvent.StreamIdentifier != Stream) continue;
				var nextEvent = responses[i+1].Event.Event; //there's always a next event since we append dummy events at the end
				Assert.AreEqual(_events[eventIndex].Id,curEvent.Id);
				_prePos[eventIndex] = (curEvent.CommitPosition, curEvent.PreparePosition);
				_postPos[eventIndex] = (nextEvent.CommitPosition, nextEvent.PreparePosition);
				eventIndex++;
			}
			Assert.AreEqual(NumEvents, eventIndex);
		}
	}
}
