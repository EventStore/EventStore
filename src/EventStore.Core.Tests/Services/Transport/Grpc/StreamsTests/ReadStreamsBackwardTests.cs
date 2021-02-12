using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EventStore.Client.Streams;
using EventStore.Core.Services.Transport.Grpc;
using Google.Protobuf;
using Grpc.Core;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.Transport.Grpc.StreamsTests {
	[TestFixture]
	public class ReadStreamsBackwardTests {
		[TestFixture(typeof(LogFormat.V2), typeof(string))]
		[TestFixture(typeof(LogFormat.V3), typeof(uint))]
		public class when_reading_backward_from_past_the_end_of_the_stream<TLogFormat, TStreamId>
			: GrpcSpecification<TLogFormat, TStreamId> {
			private readonly string _streamName = Guid.NewGuid().ToString();
			private readonly List<ReadResp> _responses = new();
			private const ulong _maxCount = 20;
			private const int EventCount = 30;

			protected override async Task Given() {
				await AppendToStreamBatch(new BatchAppendReq {
					Options = new() {
						Any = new(),
						StreamIdentifier = new() { StreamName = ByteString.CopyFromUtf8(_streamName) }
					},
					IsFinal = true,
					ProposedMessages = { CreateEvents(EventCount) },
					CorrelationId = Uuid.NewUuid().ToDto()
				});
			}

			protected override async Task When() {
				using var call = StreamsClient.Read(new() {
					Options = new() {
						Count = _maxCount,
						Stream = new() {
							StreamIdentifier = new() { StreamName = ByteString.CopyFromUtf8(_streamName) },
							End = new()
						},
						ReadDirection = ReadReq.Types.Options.Types.ReadDirection.Backwards,
						UuidOption = new() { Structured = new() },
						NoFilter = new()
					}
				});
				_responses.AddRange(await call.ResponseStream.ReadAllAsync().ToArrayAsync());
			}

			[Test]
			public void should_not_receive_null_events() {
				Assert.False(_responses.Where(x => x.StreamPosition is null)
					.Any(x => x.Event is null));
			}

			[Test]
			public void should_read_a_number_of_events_equal_to_the_max_count() {
				Assert.AreEqual(20, _responses.Count(x => x.Event is not null));
			}

			[Test]
			public void should_read_the_correct_events() {
				Assert.AreEqual(29, _responses.First(x => x.Event is not null).Event.Event.StreamRevision);
				Assert.AreEqual(10, _responses.Last(x => x.Event is not null).Event.Event.StreamRevision);
			}

			[Test]
			public void should_indicate_last_position_of_stream() {
				CollectionAssert.AreEqual(Enumerable.Range(0, EventCount)
					.Reverse()
					.Take(20)
					.Select(streamPosition => new {
						streamPosition = Convert.ToUInt64(streamPosition),
						lastStreamPosition = EventCount - 1
					}), _responses.Where(x => x.Event is not null)
					.Select(x => new {
						streamPosition = x.Event.Event.StreamRevision,
						lastStreamPosition = EventCount - 1
					}));
			}
		}

		[TestFixture(typeof(LogFormat.V2), typeof(string))]
		[TestFixture(typeof(LogFormat.V3), typeof(uint))]
		public class when_reading_backward_from_the_end_of_the_stream<TLogFormat, TStreamId>
			: GrpcSpecification<TLogFormat, TStreamId> {
			private readonly string _streamName = Guid.NewGuid().ToString();
			private readonly List<ReadResp> _responses = new();
			private const ulong _maxCount = 20;
			private const int EventCount = 30;

			protected override async Task Given() {
				await AppendToStreamBatch(new BatchAppendReq {
					Options = new() {
						Any = new(),
						StreamIdentifier = new() { StreamName = ByteString.CopyFromUtf8(_streamName) }
					},
					IsFinal = true,
					ProposedMessages = { CreateEvents(EventCount) },
					CorrelationId = Uuid.NewUuid().ToDto()
				});
			}

			protected override async Task When() {
				using var call = StreamsClient.Read(new() {
					Options = new() {
						Count = _maxCount,
						Stream = new() {
							StreamIdentifier = new() { StreamName = ByteString.CopyFromUtf8(_streamName) },
							End = new()
						},
						ReadDirection = ReadReq.Types.Options.Types.ReadDirection.Backwards,
						UuidOption = new() { Structured = new() },
						NoFilter = new()
					}
				});
				_responses.AddRange(await call.ResponseStream.ReadAllAsync().ToArrayAsync());
			}

			[Test]
			public void should_not_receive_null_events() {
				Assert.False(_responses.Where(x => x.StreamPosition is null)
					.Any(x => x.Event is null));
			}

			[Test]
			public void should_read_a_number_of_events_equal_to_the_max_count() {
				Assert.AreEqual(20, _responses.Count(x => x.Event is not null));
			}

			[Test]
			public void should_read_the_correct_events() {
				Assert.AreEqual(29, _responses.First(x => x.Event is not null).Event.Event.StreamRevision);
				Assert.AreEqual(10, _responses.Last(x => x.Event is not null).Event.Event.StreamRevision);
			}

			[Test]
			public void should_indicate_last_position_of_stream() {
				CollectionAssert.AreEqual(Enumerable.Range(0, EventCount)
					.Reverse()
					.Take(20)
					.Select(streamPosition => new {
						streamPosition = Convert.ToUInt64(streamPosition),
						lastStreamPosition = EventCount - 1
					}), _responses.Where(x => x.Event is not null)
					.Select(x => new {
						streamPosition = x.Event.Event.StreamRevision,
						lastStreamPosition = EventCount - 1
					}));
			}
		}

		[TestFixture(typeof(LogFormat.V2), typeof(string))]
		[TestFixture(typeof(LogFormat.V3), typeof(uint))]
		public class when_reading_backward_from_the_start_of_the_stream<TLogFormat, TStreamId>
			: GrpcSpecification<TLogFormat, TStreamId> {
			private readonly string _streamName = Guid.NewGuid().ToString();
			private readonly List<ReadResp> _responses = new();
			private const ulong _maxCount = 20;
			private const int EventCount = 10;

			protected override async Task Given() {
				await AppendToStreamBatch(new BatchAppendReq {
					Options = new() {
						Any = new(),
						StreamIdentifier = new() { StreamName = ByteString.CopyFromUtf8(_streamName) },
					},
					CorrelationId = Uuid.NewUuid().ToDto(),
					IsFinal = true,
					ProposedMessages = { CreateEvents(EventCount) }
				});
			}

			protected override async Task When() {
				using var call = StreamsClient.Read(new ReadReq {
					Options = new() {
						Count = _maxCount,
						Stream = new() {
							StreamIdentifier = new() { StreamName = ByteString.CopyFromUtf8(_streamName) },
							Start = new()
						},
						ReadDirection = ReadReq.Types.Options.Types.ReadDirection.Backwards,
						UuidOption = new() { Structured = new() },
						NoFilter = new()
					}
				});
				_responses.AddRange(await call.ResponseStream.ReadAllAsync().ToArrayAsync());
			}

			[Test]
			public void should_receive_the_first_event() {
				Assert.AreEqual(1, _responses.Count(x => x.Event is not null));
				Assert.AreEqual(0, _responses.First(x=>x.Event is not null).Event.Event.StreamRevision);
			}

			[Test]
			public void should_indicate_last_position_of_stream() {
				Assert.AreEqual(EventCount - 1, _responses.Last(x => x.StreamPosition is not null)
					.StreamPosition.LastStreamPosition);
			}
		}
	}
}
