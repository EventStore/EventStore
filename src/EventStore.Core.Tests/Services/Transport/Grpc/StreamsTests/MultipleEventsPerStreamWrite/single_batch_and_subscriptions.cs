using System;
using System.Linq;
using System.Threading;
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
	public class single_batch_and_subscriptions<TLogFormat, TStreamId> : GrpcSpecification<TLogFormat, TStreamId> {
		private const int NumEvents = 10;
		private const uint MaxSearchWindow = 3;

		protected override Task Given() => Task.CompletedTask;
		protected override Task When() => Task.CompletedTask;

		private async Task AppendNewEvents(string stream, int count = NumEvents, string[] eventTypes = null) {
			await AppendToStreamBatch(new BatchAppendReq {
				Options = new() {
					Any = new(),
					StreamIdentifier = new() {StreamName = ByteString.CopyFromUtf8(stream)}
				},
				IsFinal = true,
				ProposedMessages = { CreateEvents(count, eventTypes) },
				CorrelationId = Uuid.NewUuid().ToDto()
			});
		}

		[Test]
		public async Task receives_correct_events_when_subscribed_to_all_from_beginning() {
			var stream = GenerateRandomString();
			await AppendNewEvents(stream);

			using var call = StreamsClient.Read(new ReadReq {
				Options = new() {
					Count = ulong.MaxValue,
					All = new() {
						Start = new()
					},
					ReadDirection = ReadDirection.Forwards,
					UuidOption = new() {Structured = new()},
					NoFilter = new(),
					Subscription = new()
				}
			}, GetCallOptions(AdminCredentials));

			var cde = new CountdownEvent(NumEvents * 2);
			_ = Task.Factory.StartNew(async () => {
				var expectedEventNumber = 0;
				while (await call.ResponseStream.MoveNext()) {
					var response = call.ResponseStream.Current;
					if (response.Event == null) continue;
					var @event = response.Event.Event;
					if (@event.StreamIdentifier != stream) continue;
					Assert.AreEqual(expectedEventNumber, @event.StreamRevision);
					expectedEventNumber++;
					cde.Signal();
					if (cde.IsSet) break;
				}
			});
			await Task.Delay(500); //TODO: find a better way to wait for subscription to start
			await AppendNewEvents(stream);
			if (!cde.Wait(TimeSpan.FromSeconds(5))) {
				Assert.Fail("Timeout has expired");
			}
		}

		[Test]
		public async Task receives_correct_events_when_subscribed_to_all_from_end() {
			var stream = GenerateRandomString();
			await AppendNewEvents(stream);
			using var call = StreamsClient.Read(new ReadReq {
				Options = new() {
					Count = ulong.MaxValue,
					All = new() {
						End = new()
					},
					ReadDirection = ReadDirection.Forwards,
					UuidOption = new() {Structured = new()},
					NoFilter = new(),
					Subscription = new()
				}
			}, GetCallOptions(AdminCredentials));

			var cde = new CountdownEvent(NumEvents);
			_ = Task.Factory.StartNew(async () => {
				var expectedEventNumber = NumEvents;
				while (await call.ResponseStream.MoveNext()) {
					var response = call.ResponseStream.Current;
					if (response.Event == null) continue;
					var @event = response.Event.Event;
					if (@event.StreamIdentifier != stream) continue;
					Assert.AreEqual(expectedEventNumber, @event.StreamRevision);
					expectedEventNumber++;
					cde.Signal();
					if (cde.IsSet) break;
				}
			});
			await Task.Delay(500); //TODO: find a better way to wait for subscription to start
			await AppendNewEvents(stream);
			if (!cde.Wait(TimeSpan.FromSeconds(5))) {
				Assert.Fail("Timeout has expired");
			}
		}

		[Test]
		public async Task receives_correct_events_when_subscribed_to_all_filtered_by_stream_prefix_from_beginning() {
			var stream = GenerateRandomString();
			await AppendNewEvents("dummy_"+stream);
			await AppendNewEvents(stream);
			using var call = StreamsClient.Read(new ReadReq {
				Options = new() {
					Count = ulong.MaxValue,
					All = new() {
						Start = new()
					},
					ReadDirection = ReadDirection.Forwards,
					UuidOption = new() {Structured = new()},
					Subscription = new(),
					Filter = new() {
						Max = MaxSearchWindow,
						StreamIdentifier = new () {
							Prefix = { stream }
						},
						CheckpointIntervalMultiplier = 1
					}
				}
			}, GetCallOptions(AdminCredentials));

			var cde = new CountdownEvent(NumEvents * 2);
			_ = Task.Factory.StartNew(async () => {
				var expectedEventNumber = 0;
				while (await call.ResponseStream.MoveNext()) {
					var response = call.ResponseStream.Current;
					if (response.Event == null) continue;
					var @event = response.Event.Event;
					Assert.AreEqual(stream, @event.StreamIdentifier.StreamName.ToStringUtf8());
					Assert.AreEqual(expectedEventNumber, @event.StreamRevision);
					expectedEventNumber++;
					cde.Signal();
					if (cde.IsSet) break;
				}
			});
			await Task.Delay(500); //TODO: find a better way to wait for subscription to start
			await AppendNewEvents("dummy_"+stream);
			await AppendNewEvents(stream);
			if (!cde.Wait(TimeSpan.FromSeconds(5))) {
				Assert.Fail("Timeout has expired");
			}
		}

		[Test]
		public async Task receives_correct_events_when_subscribed_to_all_filtered_by_stream_prefix_from_end() {
			var stream = GenerateRandomString();
			await AppendNewEvents("dummy_"+stream);
			await AppendNewEvents(stream);

			using var call = StreamsClient.Read(new ReadReq {
				Options = new() {
					Count = ulong.MaxValue,
					All = new() {
						End = new()
					},
					ReadDirection = ReadDirection.Forwards,
					UuidOption = new() {Structured = new()},
					Subscription = new(),
					Filter = new() {
						Max = MaxSearchWindow,
						StreamIdentifier = new () {
							Prefix = { stream }
						},
						CheckpointIntervalMultiplier = 1
					}
				}
			}, GetCallOptions(AdminCredentials));

			var cde = new CountdownEvent(NumEvents);
			_ = Task.Factory.StartNew(async () => {
				var expectedEventNumber = NumEvents;
				while (await call.ResponseStream.MoveNext()) {
					var response = call.ResponseStream.Current;
					if (response.Event == null) continue;
					var @event = response.Event.Event;
					Assert.AreEqual(stream, @event.StreamIdentifier.StreamName.ToStringUtf8());
					Assert.AreEqual(expectedEventNumber, @event.StreamRevision);
					expectedEventNumber++;
					cde.Signal();
					if (cde.IsSet) break;
				}
			});
			await Task.Delay(500); //TODO: find a better way to wait for subscription to start
			await AppendNewEvents("dummy_"+stream);
			await AppendNewEvents(stream);
			if (!cde.Wait(TimeSpan.FromSeconds(5))) {
				Assert.Fail("Timeout has expired");
			}
		}

		[Test]
		public async Task receives_correct_events_when_subscribed_to_all_filtered_by_event_type_prefix_from_beginning() {
			var stream = GenerateRandomString();
			var eventTypes = new string[NumEvents];
			for (int i = 0; i < NumEvents; i++) {
				eventTypes[i] = stream + (i % 2 == 0 ? "_even" : "_odd");
			}
			await AppendNewEvents("dummy_"+stream, NumEvents, eventTypes);
			await AppendNewEvents(stream, NumEvents, eventTypes);
			using var call = StreamsClient.Read(new ReadReq {
				Options = new() {
					Count = ulong.MaxValue,
					All = new() {
						Start = new()
					},
					ReadDirection = ReadDirection.Forwards,
					UuidOption = new() {Structured = new()},
					Subscription = new(),
					Filter = new() {
						Max = MaxSearchWindow,
						EventType = new() {
							Prefix = { stream + "_even" }
						},
						CheckpointIntervalMultiplier = 1
					}
				}
			}, GetCallOptions(AdminCredentials));

			var cde = new CountdownEvent(NumEvents * 2);
			_ = Task.Factory.StartNew(async () => {
				var expectedEventNumber = 0;
				var expectedEventNumberDummy = 0;
				while (await call.ResponseStream.MoveNext()) {
					var response = call.ResponseStream.Current;
					if (response.Event == null) continue;
					var @event = response.Event.Event;
					Assert.True(@event.StreamIdentifier == stream
					            || @event.StreamIdentifier == "dummy_" + stream);
					if (@event.StreamIdentifier== stream) {
						Assert.AreEqual(expectedEventNumber, @event.StreamRevision);
						expectedEventNumber+=2;
					} else if(@event.StreamIdentifier== "dummy_" + stream){
						Assert.AreEqual(expectedEventNumberDummy, @event.StreamRevision);
						expectedEventNumberDummy+=2;
					}

					cde.Signal();
					if (cde.IsSet) break;
				}
			});
			await Task.Delay(500); //TODO: find a better way to wait for subscription to start
			await AppendNewEvents("dummy_"+stream, NumEvents, eventTypes);
			await AppendNewEvents(stream, NumEvents, eventTypes);
			if (!cde.Wait(TimeSpan.FromSeconds(5))) {
				Assert.Fail("Timeout has expired");
			}
		}

		[Test]
		public async Task receives_correct_events_when_subscribed_to_all_filtered_by_event_type_prefix_from_end() {
			var stream = GenerateRandomString();
			var eventTypes = new string[NumEvents];
			for (int i = 0; i < NumEvents; i++) {
				eventTypes[i] = stream + (i % 2 == 0 ? "_even" : "_odd");
			}
			await AppendNewEvents("dummy_"+stream, NumEvents, eventTypes);
			await AppendNewEvents(stream, NumEvents, eventTypes);
			using var call = StreamsClient.Read(new ReadReq {
				Options = new() {
					Count = ulong.MaxValue,
					All = new() {
						End = new()
					},
					ReadDirection = ReadDirection.Forwards,
					UuidOption = new() {Structured = new()},
					Subscription = new(),
					Filter = new() {
						Max = MaxSearchWindow,
						EventType = new() {
							Prefix = { stream + "_odd" }
						},
						CheckpointIntervalMultiplier = 1
					}
				}
			}, GetCallOptions(AdminCredentials));

			var cde = new CountdownEvent(NumEvents);
			_ = Task.Factory.StartNew(async () => {
				var expectedEventNumber = NumEvents + 1;
				var expectedEventNumberDummy = NumEvents + 1;
				while (await call.ResponseStream.MoveNext()) {
					var response = call.ResponseStream.Current;
					if (response.Event == null) continue;
					var @event = response.Event.Event;
					Assert.True(@event.StreamIdentifier == stream
					            || @event.StreamIdentifier == "dummy_" + stream);
					if (@event.StreamIdentifier== stream) {
						Assert.AreEqual(expectedEventNumber, @event.StreamRevision);
						expectedEventNumber+=2;
					} else if(@event.StreamIdentifier== "dummy_" + stream){
						Assert.AreEqual(expectedEventNumberDummy, @event.StreamRevision);
						expectedEventNumberDummy+=2;
					}

					cde.Signal();
					if (cde.IsSet) break;
				}
			});
			await Task.Delay(500); //TODO: find a better way to wait for subscription to start
			await AppendNewEvents("dummy_"+stream, NumEvents, eventTypes);
			await AppendNewEvents(stream, NumEvents, eventTypes);
			if (!cde.Wait(TimeSpan.FromSeconds(5))) {
				Assert.Fail("Timeout has expired");
			}
		}

		[Test]
		public async Task receives_correct_events_when_subscribed_to_all_filtered_by_event_type_prefix_from_middle() {
			var stream = GenerateRandomString();
			var eventTypes = new string[NumEvents];
			for (int i = 0; i < NumEvents; i++) {
				eventTypes[i] = stream + (i % 2 == 0 ? "_even" : "_odd");
			}
			await AppendNewEvents("dummy_"+stream, NumEvents, eventTypes);
			await AppendNewEvents(stream, NumEvents, eventTypes);

			//since subscription start positions are exclusive, we need to set pos=NumEvents/2-1 to get NumEvents/2 as first event
			var pos = await GetEventPosition("dummy_" + stream, NumEvents / 2 - 1);
			using var call = StreamsClient.Read(new ReadReq {
				Options = new() {
					Count = ulong.MaxValue,
					All = new() {
						Position = new() {
							CommitPosition = pos.preCommit,
							PreparePosition = pos.prePrepare
						}
					},
					ReadDirection = ReadDirection.Forwards,
					UuidOption = new() {Structured = new()},
					Subscription = new(),
					Filter = new() {
						Max = MaxSearchWindow,
						EventType = new() {
							Prefix = { stream + "_odd" }
						},
						CheckpointIntervalMultiplier = 1
					}
				}
			}, GetCallOptions(AdminCredentials));

			var cde = new CountdownEvent(NumEvents * 2 - NumEvents / 2 / 2);
			_ = Task.Factory.StartNew(async () => {
				var expectedEventNumber = 1;
				var expectedEventNumberDummy = NumEvents / 2;
				while (await call.ResponseStream.MoveNext()) {
					var response = call.ResponseStream.Current;
					if (response.Event == null) continue;
					var @event = response.Event.Event;
					Assert.True(@event.StreamIdentifier == stream
					            || @event.StreamIdentifier == "dummy_" + stream);
					if (@event.StreamIdentifier== stream) {
						Assert.AreEqual(expectedEventNumber, @event.StreamRevision);
						expectedEventNumber+=2;
					} else if(@event.StreamIdentifier== "dummy_" + stream){
						Assert.AreEqual(expectedEventNumberDummy, @event.StreamRevision);
						expectedEventNumberDummy+=2;
					}

					cde.Signal();
					if (cde.IsSet) break;
				}
			});
			await Task.Delay(500); //TODO: find a better way to wait for subscription to start
			await AppendNewEvents("dummy_"+stream, NumEvents, eventTypes);
			await AppendNewEvents(stream, NumEvents, eventTypes);
			if (!cde.Wait(TimeSpan.FromSeconds(5))) {
				Assert.Fail("Timeout has expired");
			}
		}

		private static string GenerateCharset() {
			var charset = "";
			for (var c = 'a'; c <= 'z'; c++) {
				charset += c;
			}
			for (var c = 'A'; c <= 'Z'; c++) {
				charset += c;
			}
			for (var c = '0'; c <= '9'; c++) {
				charset += c;
			}
			charset += "!@#$%^&*()-_";
			return charset;
		}
		private static string GenerateRandomString() {
			var charset = GenerateCharset();
			var random = new Random();
			string s = "";
			for (var j = 0; j < 10; j++) {
				s += charset[random.Next() % charset.Length];
			}
			return s;
		}

		private async Task<(ulong preCommit,ulong prePrepare, ulong postCommit, ulong postPrepare)> GetEventPosition(string stream, ulong eventNumber) {
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
				.Where(x => x.Event.Event.StreamIdentifier == stream)
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
