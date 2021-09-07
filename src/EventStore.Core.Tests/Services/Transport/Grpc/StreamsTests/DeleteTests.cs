using System;
using System.Linq;
using System.Threading.Tasks;
using EventStore.Client.Streams;
using EventStore.Core.Data;
using EventStore.Core.Services.Transport.Grpc;
using Google.Protobuf;
using Grpc.Core;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.Transport.Grpc.StreamsTests {
	[TestFixture]
	public class DeleteTests {
		private const string StreamName = nameof(DeleteTests);

		public abstract class DeleteExistingStreamSpecification<TLogFormat, TStreamId>
			: GrpcSpecification<TLogFormat, TStreamId> {
			private readonly DeleteReq.Types.Options _options;
			private Exception _caughtException;

			private DeleteExistingStreamSpecification(DeleteReq.Types.Options options) {
				_options = new(options) {
					StreamIdentifier = new() { StreamName = ByteString.CopyFromUtf8(StreamName) }
				};
			}

			protected DeleteExistingStreamSpecification(StreamRevision expectedStreamRevision) : this(
				new DeleteReq.Types.Options {
					Revision = expectedStreamRevision.ToUInt64(),
				}) {
			}

			protected DeleteExistingStreamSpecification(long expectedStreamState) : this(expectedStreamState switch {
				ExpectedVersion.Any => new DeleteReq.Types.Options { Any = new() },
				ExpectedVersion.NoStream => new DeleteReq.Types.Options { NoStream = new() },
				ExpectedVersion.StreamExists => new DeleteReq.Types.Options { StreamExists = new() },
				_ => throw new InvalidOperationException()
			}) {
			}

			[Test]
			public void no_exception_is_thrown() {
				Assert.Null(_caughtException);
			}

			[Test]
			public async Task the_stream_is_deleted() {
				using var call = StreamsClient.Read(new() {
					Options = new() {
						UuidOption = new() { Structured = new() },
						NoFilter = new(),
						ResolveLinks = false,
						ReadDirection = ReadReq.Types.Options.Types.ReadDirection.Forwards,
						Count = 1,
						Stream = new() {
							StreamIdentifier = new() { StreamName = ByteString.CopyFromUtf8(StreamName) },
							Start = new()
						}
					}
				}, GetCallOptions());

				var responses = await call.ResponseStream.ReadAllAsync().ToArrayAsync();

				Assert.AreEqual(1, responses.Length);
				Assert.AreEqual(new ReadResp {
					StreamNotFound = new() {
						StreamIdentifier = new() { StreamName = ByteString.CopyFromUtf8(StreamName) }
					}
				}, responses[0]);
			}

			protected override async Task Given() => await AppendToStreamBatch(new BatchAppendReq {
				Options = new() {
					NoStream = new(),
					StreamIdentifier = new() { StreamName = ByteString.CopyFromUtf8(StreamName) }
				},
				CorrelationId = Uuid.NewUuid().ToDto(),
				IsFinal = true,
				ProposedMessages = { CreateEvents(1) }
			});

			protected override async Task When() {
				using var call = StreamsClient.DeleteAsync(new() {
					Options = _options
				}, GetCallOptions());

				try {
					await call.ResponseAsync;
				} catch (Exception ex) {
					_caughtException = ex;
				}
			}
		}

		[TestFixture(typeof(LogFormat.V2), typeof(string))]
		[TestFixture(typeof(LogFormat.V3), typeof(uint))]
		public class DeleteExistingStreamExpectedRevision<TLogFormat, TStreamId>
			: DeleteExistingStreamSpecification<TLogFormat, TStreamId> {
			public DeleteExistingStreamExpectedRevision() : base(StreamRevision.Start) { }
		}

		[TestFixture(typeof(LogFormat.V2), typeof(string))]
		[TestFixture(typeof(LogFormat.V3), typeof(uint))]
		public class DeleteExistingStreamAny<TLogFormat, TStreamId>
			: DeleteExistingStreamSpecification<TLogFormat, TStreamId> {
			public DeleteExistingStreamAny() : base(ExpectedVersion.Any) { }
		}

		[TestFixture(typeof(LogFormat.V2), typeof(string))]
		[TestFixture(typeof(LogFormat.V3), typeof(uint))]
		public class DeleteExistingStreamExists<TLogFormat, TStreamId>
			: DeleteExistingStreamSpecification<TLogFormat, TStreamId> {
			public DeleteExistingStreamExists() : base(ExpectedVersion.StreamExists) { }
		}
	}
}
