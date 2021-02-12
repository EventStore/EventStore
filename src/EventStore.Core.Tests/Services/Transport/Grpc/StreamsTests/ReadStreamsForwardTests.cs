using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EventStore.Client.Streams;
using EventStore.ClientAPI;
using EventStore.Core.Services.Transport.Grpc;
using Google.Protobuf;
using Grpc.Core;
using NUnit.Framework;
using GrpcMetadata = EventStore.Core.Services.Transport.Grpc.Constants.Metadata;

namespace EventStore.Core.Tests.Services.Transport.Grpc.StreamsTests {
	[TestFixture]
	public class ReadStreamsForwardTests {
		[TestFixture(typeof(LogFormat.V2), typeof(string))]
		[TestFixture(typeof(LogFormat.V3), typeof(uint))]
		public class when_reading_forward_from_stream_that_has_been_truncated<TLogFormat, TStreamId>
			: GrpcSpecification<TLogFormat, TStreamId> {
			private readonly string _streamName = Guid.NewGuid().ToString();
			private readonly List<ReadResp> _responses = new();
			private const ulong MaxCount = 10;
			private const int EventCount = 100;

			protected override async Task Given() {
				await AppendToStreamBatch(new BatchAppendReq {
					Options = new() {
						Any = new(),
						StreamIdentifier = new() {
							StreamName = ByteString.CopyFromUtf8(_streamName)
						}
					},
					IsFinal = true,
					ProposedMessages = { CreateEvents(EventCount) },
					CorrelationId = Uuid.NewUuid().ToDto()
				});

				using var call = StreamsClient.Append(GetCallOptions(AdminCredentials));
				await call.RequestStream.WriteAsync(new() {
					Options = new() {
						StreamIdentifier = new(){StreamName = ByteString.CopyFromUtf8($"$${_streamName}")},
						Any = new()
					}
				});

				await call.RequestStream.WriteAsync(new() {
					ProposedMessage = new() {
						Id = Uuid.NewUuid().ToDto(),
						Metadata = {
							[GrpcMetadata.Type] = "$metadata",
							[GrpcMetadata.ContentType] = GrpcMetadata.ContentTypes.ApplicationJson
						},
						Data = ByteString.CopyFrom(StreamMetadata.Build().SetTruncateBefore(81).Build()
							.AsJsonBytes())
					}
				});

				await call.RequestStream.CompleteAsync();

				await call.ResponseAsync;
			}

			protected override async Task When() {
				using var call = StreamsClient.Read(new() {
					Options = new () {
						Count = MaxCount,
						Stream = new() {
							StreamIdentifier = new() {
								StreamName = ByteString.CopyFromUtf8(_streamName)
							},
							Start = new()
						},
						UuidOption = new() { Structured = new() },
						ReadDirection = ReadReq.Types.Options.Types.ReadDirection.Forwards,
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
				Assert.AreEqual(MaxCount, _responses.Count(x => x.Event is not null));
			}

			[Test]
			public void should_start_from_the_truncation_position() {
				Assert.AreEqual(81, _responses.First(x => x.Event is not null).Event.Event.StreamRevision);
				Assert.AreEqual(90, _responses.Last(x => x.Event is not null).Event.Event.StreamRevision);
			}

			[Test]
			public void should_indicate_last_position_of_stream() {
				CollectionAssert.AreEqual(Enumerable.Range(0, EventCount)
					.Skip(81)
					.Take(10)
					.Select(streamPosition => new {
						streamPosition = Convert.ToUInt64(streamPosition),
						lastStreamPosition = EventCount - 1
					}), _responses.Where(x => x.ContentCase == ReadResp.ContentOneofCase.Event)
					.Select(x => new {
						streamPosition = x.Event.Event.StreamRevision,
						lastStreamPosition = EventCount - 1
					}));
			}
		}

		[TestFixture(typeof(LogFormat.V2), typeof(string))]
		[TestFixture(typeof(LogFormat.V3), typeof(uint))]
		public class when_reading_forward_from_the_start_of_the_stream<TLogFormat, TStreamId>
			: GrpcSpecification<TLogFormat, TStreamId> {
			private readonly string _streamName = Guid.NewGuid().ToString();
			private readonly List<ReadResp> _responses = new();
			private const ulong MaxCount = 50;
			private const int EventCount = 100;

			protected override async Task Given() {
				await AppendToStreamBatch(new BatchAppendReq {
					Options = new() {
						Any = new(),
						StreamIdentifier = new() {
							StreamName = ByteString.CopyFromUtf8(_streamName)
						}
					},
					IsFinal = true,
					ProposedMessages = { CreateEvents(EventCount) },
					CorrelationId = Uuid.NewUuid().ToDto()
				});
			}

			protected override async Task When() {
				using var call = StreamsClient.Read(new() {
					Options = new ReadReq.Types.Options {
						Count = MaxCount,
						Stream = new() {
							StreamIdentifier = new() {
								StreamName = ByteString.CopyFromUtf8(_streamName)
							},
							Start = new()
						},
						UuidOption = new() { Structured = new() },
						ReadDirection = ReadReq.Types.Options.Types.ReadDirection.Forwards,
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
				Assert.AreEqual(MaxCount, _responses.Count(x => x.Event is not null));
			}

			[Test]
			public void should_read_the_correct_events() {
				Assert.AreEqual(50, _responses.Count(x => x.Event != null));
				Assert.AreEqual(49, _responses.Last(x => x.Event is not null).Event.Event.StreamRevision);
			}

			[Test]
			public void should_indicate_last_position_of_stream() {
				var streamPosition = _responses.Last(x => x.StreamPosition != null);
				Assert.AreEqual(99, streamPosition.StreamPosition.LastStreamPosition);
				Assert.AreEqual(49, streamPosition.StreamPosition.NextStreamPosition);
			}

		}

		[TestFixture(typeof(LogFormat.V2), typeof(string))]
		[TestFixture(typeof(LogFormat.V3), typeof(uint))]
		public class when_reading_forward_from_stream_with_no_events_after_position<TLogFormat, TStreamId>
			: GrpcSpecification<TLogFormat, TStreamId> {
			private readonly string _streamName = Guid.NewGuid().ToString();
			private readonly List<ReadResp> _responses = new();
			private const ulong MaxCount = 50;

			protected override async Task Given() {
				await AppendToStreamBatch(new BatchAppendReq {
					Options = new() {
						Any = new(),
						StreamIdentifier = new() {
							StreamName = ByteString.CopyFromUtf8(_streamName)
						}
					},
					IsFinal = true,
					ProposedMessages = { CreateEvents(10) },
					CorrelationId = Uuid.NewUuid().ToDto()
				});
			}

			protected override async Task When() {
				using var call = StreamsClient.Read(new() {
					Options = new ReadReq.Types.Options {
						Count = MaxCount,
						Stream = new() {
							StreamIdentifier = new() { StreamName = ByteString.CopyFromUtf8(_streamName) },
							Revision = 11
						},
						UuidOption = new() { Structured = new() },
						ReadDirection = ReadReq.Types.Options.Types.ReadDirection.Forwards,
						NoFilter = new()
					}
				});
				_responses.AddRange(await  call.ResponseStream.ReadAllAsync().ToArrayAsync());
			}

			[Test]
			public void should_not_receive_events() {
				Assert.IsEmpty(_responses.Where(x => x.Event is not null));
			}
		}
	}
}
