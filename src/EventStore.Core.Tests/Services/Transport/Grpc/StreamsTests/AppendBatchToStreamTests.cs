using System;
using Google.Protobuf.WellKnownTypes;
using System.Threading.Tasks;
using EventStore.Client.Streams;
using EventStore.Core.Services.Transport.Grpc;
using Google.Protobuf;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.Transport.Grpc.StreamsTests {
	[TestFixture]
	public class AppendBatchToStreamTests {
		[TestFixture(typeof(LogFormat.V2), typeof(string))]
		[TestFixture(typeof(LogFormat.V3), typeof(uint))]
		public class single_batch<TLogFormat, TStreamId> : GrpcSpecification<TLogFormat, TStreamId> {
			private BatchAppendResp _response;
			protected override Task Given() => Task.CompletedTask;

			protected override async Task When() {
				_response = await AppendToStreamBatch(new BatchAppendReq {
					Options = new() {
						Any = new(),
						StreamIdentifier = new() { StreamName = ByteString.CopyFromUtf8("stream") }
					},
					IsFinal = true,
					ProposedMessages = { CreateEvents(1) },
					CorrelationId = Uuid.NewUuid().ToDto()
				});
			}

			[Test]
			public void is_success() {
				Assert.AreEqual(_response.ResultCase, BatchAppendResp.ResultOneofCase.Success);
			}
		}

		[TestFixture(typeof(LogFormat.V2), typeof(string))]
		[TestFixture(typeof(LogFormat.V3), typeof(uint))]
		public class multiple_batches<TLogFormat, TStreamId> : GrpcSpecification<TLogFormat, TStreamId> {
			private BatchAppendResp _response;
			protected override Task Given() => Task.CompletedTask;

			protected override async Task When() {
				var correlationId = Uuid.NewUuid();
				_response = await AppendToStreamBatch(new BatchAppendReq {
					Options = new() {
						Any = new(),
						StreamIdentifier = new() { StreamName = ByteString.CopyFromUtf8("stream") }
					},
					IsFinal = false,
					ProposedMessages = { CreateEvents(1) },
					CorrelationId = correlationId.ToDto(),
				}, new() {
					IsFinal = true,
					ProposedMessages = { CreateEvents(1) },
					CorrelationId = correlationId.ToDto()
				});
			}

			[Test]
			public void is_success() {
				Assert.AreEqual(_response.ResultCase, BatchAppendResp.ResultOneofCase.Success);
			}
		}

		[TestFixture(typeof(LogFormat.V2), typeof(string))]
		[TestFixture(typeof(LogFormat.V3), typeof(uint))]
		public class exceeded_deadline<TLogFormat, TStreamId> : GrpcSpecification<TLogFormat, TStreamId> {
			private BatchAppendResp _response;
			protected override Task Given() => Task.CompletedTask;

			protected override async Task When() {
				_response = await AppendToStreamBatch(new BatchAppendReq {
					Options = new() {
						Any = new(),
						StreamIdentifier = new() { StreamName = ByteString.CopyFromUtf8("stream") },
						Deadline = Timestamp.FromDateTime(DateTime.UtcNow.AddSeconds(-1))
					},
					IsFinal = true,
					ProposedMessages = { CreateEvents(1) },
					CorrelationId = Uuid.NewUuid().ToDto()
				});
			}

			[Test]
			public void is_error() {
				Assert.AreEqual(_response.ResultCase, BatchAppendResp.ResultOneofCase.Error);
			}
		}
	}
}
