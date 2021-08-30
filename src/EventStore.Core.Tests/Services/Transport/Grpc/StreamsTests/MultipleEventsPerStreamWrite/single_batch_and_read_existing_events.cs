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
	[TestFixture(typeof(LogFormat.V2), typeof(string))]
	[TestFixture(typeof(LogFormat.V3), typeof(uint))]
	public class single_batch_and_read_existing_events<TLogFormat, TStreamId> : GrpcSpecification<TLogFormat, TStreamId> {
		private const string Stream = "stream";
		private const int NumExistingEvents = 10;
		private readonly BatchAppendReq.Types.ProposedMessage[] _existingEvents;
		private readonly UUID[] _ids;

		public single_batch_and_read_existing_events() {
			_existingEvents = CreateEvents(NumExistingEvents).ToArray();
			_ids = _existingEvents.Select(x => x.Id).ToArray();
		}
		protected override Task Given() => AppendExistingEvents();
		protected override Task When() => Task.CompletedTask;
		private async Task AppendExistingEvents() {
			await AppendToStreamBatch(new BatchAppendReq {
				Options = new() {
					Any = new(),
					StreamIdentifier = new() {StreamName = ByteString.CopyFromUtf8(Stream)}
				},
				IsFinal = true,
				ProposedMessages = { _existingEvents },
				CorrelationId = Uuid.NewUuid().ToDto()
			});

		}
		[Test]
		public async Task can_read_events_from_stream_forwards() {
			using var call = StreamsClient.Read(new ReadReq {
				Options = new() {
					Count = ulong.MaxValue,
					Stream = new() {
						StreamIdentifier = new() { StreamName = ByteString.CopyFromUtf8(Stream) },
						Start = new Empty()
					},
					ReadDirection = ReadDirection.Forwards,
					UuidOption = new() { Structured = new() },
					NoFilter = new()
				}
			});
			var response = call.ResponseStream.ReadAllAsync();
			var events = await response
				.Where(x => x.Event is not null)
				.Select(x => x.Event.Event)
				.ToArrayAsync();

			Assert.AreEqual(NumExistingEvents, events.Length);
			for (int i = 0; i < NumExistingEvents; i++) {
				Assert.AreEqual(events[i].StreamIdentifier.StreamName, Stream);
				Assert.AreEqual(events[i].StreamRevision, i);
				Assert.AreEqual(events[i].Id, _ids[i]);
			}
		}

		[Test]
		public async Task can_read_events_from_stream_backwards() {
			using var call = StreamsClient.Read(new ReadReq {
				Options = new() {
					Count = ulong.MaxValue,
					Stream = new() {
						StreamIdentifier = new() { StreamName = ByteString.CopyFromUtf8(Stream) },
						End = new Empty()
					},
					ReadDirection = ReadDirection.Backwards,
					UuidOption = new() { Structured = new() },
					NoFilter = new()
				}
			});
			var response = call.ResponseStream.ReadAllAsync();
			var events = await response
				.Where(x => x.Event is not null)
				.Select(x => x.Event.Event)
				.Reverse()
				.ToArrayAsync();

			Assert.AreEqual(NumExistingEvents, events.Length);
			for (int i = 0; i < NumExistingEvents; i++) {
				Assert.AreEqual(events[i].StreamIdentifier.StreamName, Stream);
				Assert.AreEqual(events[i].StreamRevision, i);
				Assert.AreEqual(events[i].Id, _ids[i]);
			}
		}

		[Test]
		public async Task can_read_events_from_all_forwards() {
			using var call = StreamsClient.Read(new ReadReq {
				Options = new() {
					Count = ulong.MaxValue,
					All = new() {
						Start = new ()
					},
					ReadDirection = ReadDirection.Forwards,
					UuidOption = new() { Structured = new() },
					NoFilter = new()
				}
			}, GetCallOptions(AdminCredentials));
			var response = call.ResponseStream.ReadAllAsync();
			var events = await response
				.Where(x => x.Event is not null)
				.Where(x => x.Event.Event.StreamIdentifier == Stream)
				.Select(x => x.Event.Event)
				.ToArrayAsync();

			Assert.AreEqual(NumExistingEvents, events.Length);
			for (int i = 0; i < NumExistingEvents; i++) {
				Assert.AreEqual(events[i].StreamIdentifier.StreamName, Stream);
				Assert.AreEqual(events[i].StreamRevision, i);
				Assert.AreEqual(events[i].Id, _ids[i]);
				if (i > 0) {
					Assert.Greater(events[i].CommitPosition, events[i-1].CommitPosition);
					Assert.Greater(events[i].PreparePosition, events[i-1].PreparePosition);
				}
			}
		}

		[Test]
		public async Task can_read_events_from_all_backwards() {
			using var call = StreamsClient.Read(new ReadReq {
				Options = new() {
					Count = ulong.MaxValue,
					All = new() {
						End = new ()
					},
					ReadDirection = ReadDirection.Backwards,
					UuidOption = new() { Structured = new() },
					NoFilter = new()
				}
			}, GetCallOptions(AdminCredentials));
			var response = call.ResponseStream.ReadAllAsync();
			var events = await response
				.Where(x => x.Event is not null)
				.Where(x => x.Event.Event.StreamIdentifier == Stream)
				.Select(x => x.Event.Event)
				.Reverse()
				.ToArrayAsync();

			Assert.AreEqual(NumExistingEvents, events.Length);
			for (int i = 0; i < NumExistingEvents; i++) {
				Assert.AreEqual(events[i].StreamIdentifier.StreamName, Stream);
				Assert.AreEqual(events[i].StreamRevision, i);
				Assert.AreEqual(events[i].Id, _ids[i]);
				if (i > 0) {
					Assert.Greater(events[i].CommitPosition, events[i-1].CommitPosition);
					Assert.Greater(events[i].PreparePosition, events[i-1].PreparePosition);
				}
			}
		}

		[Test]
		public async Task can_read_events_from_stream_forwards_in_the_middle() {
			using var call = StreamsClient.Read(new ReadReq {
				Options = new() {
					Count = ulong.MaxValue,
					Stream = new() {
						StreamIdentifier = new() { StreamName = ByteString.CopyFromUtf8(Stream) },
						Revision = NumExistingEvents / 2
					},
					ReadDirection = ReadDirection.Forwards,
					UuidOption = new() { Structured = new() },
					NoFilter = new()
				}
			});
			var response = call.ResponseStream.ReadAllAsync();
			var events = await response
				.Where(x => x.Event is not null)
				.Select(x => x.Event.Event)
				.ToArrayAsync();

			Assert.AreEqual(NumExistingEvents / 2, events.Length);
			for (int i = 0; i < NumExistingEvents / 2; i++) {
				Assert.AreEqual(events[i].StreamIdentifier.StreamName, Stream);
				Assert.AreEqual(events[i].StreamRevision, NumExistingEvents / 2 + i);
				Assert.AreEqual(events[i].Id, _ids[NumExistingEvents / 2 + i]);
			}
		}

		[Test]
		public async Task can_read_events_from_stream_backwards_in_the_middle() {
			using var call = StreamsClient.Read(new ReadReq {
				Options = new() {
					Count = ulong.MaxValue,
					Stream = new() {
						StreamIdentifier = new() { StreamName = ByteString.CopyFromUtf8(Stream) },
						Revision = NumExistingEvents / 2
					},
					ReadDirection = ReadDirection.Backwards,
					UuidOption = new() { Structured = new() },
					NoFilter = new()
				}
			});
			var response = call.ResponseStream.ReadAllAsync();
			var events = await response
				.Where(x => x.Event is not null)
				.Select(x => x.Event.Event)
				.Reverse()
				.ToArrayAsync();

			Assert.AreEqual(NumExistingEvents / 2 + 1, events.Length);
			for (int i = 0; i < NumExistingEvents / 2 + 1; i++) {
				Assert.AreEqual(events[i].StreamIdentifier.StreamName, Stream);
				Assert.AreEqual(events[i].StreamRevision,i);
				Assert.AreEqual(events[i].Id, _ids[i]);
			}
		}

		[Test]
		public async Task can_read_one_event_from_stream_forwards_in_the_middle() {
			using var call = StreamsClient.Read(new ReadReq {
				Options = new() {
					Count = 1,
					Stream = new() {
						StreamIdentifier = new() { StreamName = ByteString.CopyFromUtf8(Stream) },
						Revision = NumExistingEvents / 2
					},
					ReadDirection = ReadDirection.Forwards,
					UuidOption = new() { Structured = new() },
					NoFilter = new()
				}
			});
			var response = call.ResponseStream.ReadAllAsync();
			var events = await response
				.Where(x => x.Event is not null)
				.Select(x => x.Event.Event)
				.ToArrayAsync();

			Assert.AreEqual(1, events.Length);
			Assert.AreEqual(events[0].StreamIdentifier.StreamName, Stream);
			Assert.AreEqual(events[0].StreamRevision, NumExistingEvents / 2);
			Assert.AreEqual(events[0].Id, _ids[NumExistingEvents / 2]);
		}

		[Test]
		public async Task can_read_one_event_from_stream_backwards_in_the_middle() {
			using var call = StreamsClient.Read(new ReadReq {
				Options = new() {
					Count = 1,
					Stream = new() {
						StreamIdentifier = new() { StreamName = ByteString.CopyFromUtf8(Stream) },
						Revision = NumExistingEvents / 2
					},
					ReadDirection = ReadDirection.Backwards,
					UuidOption = new() { Structured = new() },
					NoFilter = new()
				}
			});
			var response = call.ResponseStream.ReadAllAsync();
			var events = await response
				.Where(x => x.Event is not null)
				.Select(x => x.Event.Event)
				.ToArrayAsync();

			Assert.AreEqual(1, events.Length);
			Assert.AreEqual(events[0].StreamIdentifier.StreamName, Stream);
			Assert.AreEqual(events[0].StreamRevision, NumExistingEvents / 2);
			Assert.AreEqual(events[0].Id, _ids[NumExistingEvents / 2]);
		}

		[Test]
		public async Task can_read_events_from_all_forwards_in_the_middle() {
			var pos = await GetEventPosition(NumExistingEvents / 2);
			using var call = StreamsClient.Read(new ReadReq {
				Options = new() {
					Count = ulong.MaxValue,
					All = new() {
						Start = new (),
						Position = new() {
							CommitPosition = pos.preCommit,
							PreparePosition = pos.prePrepare
						}
					},
					ReadDirection = ReadDirection.Forwards,
					UuidOption = new() { Structured = new() },
					NoFilter = new()
				}
			}, GetCallOptions(AdminCredentials));
			var response = call.ResponseStream.ReadAllAsync();
			var events = await response
				.Where(x => x.Event is not null)
				.Where(x => x.Event.Event.StreamIdentifier == Stream)
				.Select(x => x.Event.Event)
				.ToArrayAsync();

			Assert.AreEqual(NumExistingEvents / 2, events.Length);
			for (int i = 0; i < NumExistingEvents / 2; i++) {
				Assert.AreEqual(events[i].StreamIdentifier.StreamName, Stream);
				Assert.AreEqual(events[i].StreamRevision, NumExistingEvents / 2 + i);
				Assert.AreEqual(events[i].Id, _ids[NumExistingEvents / 2 + i]);
				if (i > 0) {
					Assert.Greater(events[i].CommitPosition, events[i-1].CommitPosition);
					Assert.Greater(events[i].PreparePosition, events[i-1].PreparePosition);
				}
			}
		}

		[Test]
		public async Task can_read_events_from_all_backwards_in_the_middle() {
			var pos = await GetEventPosition(NumExistingEvents / 2);
			using var call = StreamsClient.Read(new ReadReq {
				Options = new() {
					Count = ulong.MaxValue,
					All = new() {
						Start = new (),
						Position = new() {
							CommitPosition = pos.postCommit,
							PreparePosition = pos.postPrepare
						}
					},
					ReadDirection = ReadDirection.Backwards,
					UuidOption = new() { Structured = new() },
					NoFilter = new()
				}
			}, GetCallOptions(AdminCredentials));
			var response = call.ResponseStream.ReadAllAsync();
			var events = await response
				.Where(x => x.Event is not null)
				.Where(x => x.Event.Event.StreamIdentifier == Stream)
				.Select(x => x.Event.Event)
				.Reverse()
				.ToArrayAsync();

			Assert.AreEqual(NumExistingEvents / 2 + 1, events.Length);
			for (int i = 0; i < NumExistingEvents / 2 + 1; i++) {
				Assert.AreEqual(events[i].StreamIdentifier.StreamName, Stream);
				Assert.AreEqual(events[i].StreamRevision, i);
				Assert.AreEqual(events[i].Id, _ids[i]);
				if (i > 0) {
					Assert.Greater(events[i].CommitPosition, events[i-1].CommitPosition);
					Assert.Greater(events[i].PreparePosition, events[i-1].PreparePosition);
				}
			}
		}

		[Test]
		public async Task can_read_one_event_from_all_forwards_in_the_middle() {
			var pos = await GetEventPosition(NumExistingEvents / 2);
			using var call = StreamsClient.Read(new ReadReq {
				Options = new() {
					Count = 1,
					All = new() {
						Start = new (),
						Position = new() {
							CommitPosition = pos.preCommit,
							PreparePosition = pos.prePrepare
						}
					},
					ReadDirection = ReadDirection.Forwards,
					UuidOption = new() { Structured = new() },
					NoFilter = new()
				}
			}, GetCallOptions(AdminCredentials));
			var response = call.ResponseStream.ReadAllAsync();
			var events = await response
				.Where(x => x.Event is not null)
				.Where(x => x.Event.Event.StreamIdentifier == Stream)
				.Select(x => x.Event.Event)
				.ToArrayAsync();

			Assert.AreEqual(1, events.Length);
			Assert.AreEqual(events[0].StreamIdentifier.StreamName, Stream);
			Assert.AreEqual(events[0].StreamRevision, NumExistingEvents / 2);
			Assert.AreEqual(events[0].Id, _ids[NumExistingEvents / 2]);
		}

		[Test]
		public async Task can_read_one_event_from_all_backwards_in_the_middle() {
			var pos = await GetEventPosition(NumExistingEvents / 2);
			using var call = StreamsClient.Read(new ReadReq {
				Options = new() {
					Count = 1,
					All = new() {
						Start = new (),
						Position = new() {
							CommitPosition = pos.postCommit,
							PreparePosition = pos.postPrepare
						}
					},
					ReadDirection = ReadDirection.Backwards,
					UuidOption = new() { Structured = new() },
					NoFilter = new()
				}
			}, GetCallOptions(AdminCredentials));
			var response = call.ResponseStream.ReadAllAsync();
			var events = await response
				.Where(x => x.Event is not null)
				.Where(x => x.Event.Event.StreamIdentifier == Stream)
				.Select(x => x.Event.Event)
				.Reverse()
				.ToArrayAsync();

			Assert.AreEqual(1, events.Length);
			Assert.AreEqual(events[0].StreamIdentifier.StreamName, Stream);
			Assert.AreEqual(events[0].StreamRevision, NumExistingEvents / 2);
			Assert.AreEqual(events[0].Id, _ids[NumExistingEvents / 2]);
		}

		private async Task<(ulong preCommit,ulong prePrepare, ulong postCommit, ulong postPrepare)> GetEventPosition(ulong eventNumber) {
			Assert.Less(eventNumber, NumExistingEvents - 1); //post commit/prepare won't be available if it's the last event
			using var call = StreamsClient.Read(new ReadReq {
				Options = new() {
					Count = ulong.MaxValue,
					All = new () {
						Start = new ()
					},
					ReadDirection = ReadDirection.Forwards,
					UuidOption = new() { Structured = new() },
					NoFilter = new()
				}
			}, GetCallOptions(AdminCredentials));

			var response = call.ResponseStream.ReadAllAsync();
			var events = await response
				.Where(x => x.Event is not null)
				.Where(x => x.Event.Event.StreamIdentifier == Stream)
				.Where(x => x.Event.Event.StreamRevision == eventNumber || x.Event.Event.StreamRevision == eventNumber + 1)
				.Select(x => x.Event.Event)
				.ToArrayAsync();

			Assert.AreEqual(2, events.Length);

			return (
				events[0].CommitPosition,
				events[0].PreparePosition,
				events[1].CommitPosition,
				events[1].PreparePosition);
		}
	}
}
