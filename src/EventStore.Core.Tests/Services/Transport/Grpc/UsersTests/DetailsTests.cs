using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EventStore.Client;
using EventStore.Client.Streams;
using EventStore.Client.Users;
using EventStore.Core.Services.Transport.Grpc;
using EventStore.Core.Tests.Services.Transport.Grpc.StreamsTests;
using Google.Protobuf;
using Grpc.Core.Utils;
using NUnit.Framework;
using GrpcMetadata = EventStore.Core.Services.Transport.Grpc.Constants.Metadata;

namespace EventStore.Core.Tests.Services.Transport.Grpc.UsersTests {
	[TestFixture]
	public class DetailsTests {
		[TestFixture(typeof(LogFormat.V2), typeof(string))]
		public class UserDetailsWithCorruptedUser<TLogFormat, TStreamId>
			: GrpcSpecification<TLogFormat, TStreamId> {
			private const string User1 = nameof(User1);
			private const string User2 = nameof(User2);
			private string[] Users => new [] { User1, User2 };

			private List<DetailsResp> _users;
			private Exception _caughtException;

			protected override Task Given() {
				foreach (var user in Users) {
					UsersClient.Create(new CreateReq() {
						Options = new CreateReq.Types.Options() {
							LoginName = user,
							FullName = $"Full name of {user}",
						}
					}, GetCallOptions(AdminCredentials));
				}
				
				return Task.CompletedTask;
			}

			protected override async Task When() {
				var append = StreamsClient.Append(GetCallOptions(AdminCredentials));
				await append.RequestStream.WriteAsync(new AppendReq() {
					Options = new AppendReq.Types.Options() {
						StreamIdentifier = new() {
							StreamName = ByteString.CopyFromUtf8($"$user-{User1}")
						},
						Any = new Empty()
					}
				});

				// empty json
				await append.RequestStream.WriteAsync(new AppendReq() {
					ProposedMessage = new AppendReq.Types.ProposedMessage {
						Id = new UUID {
							String = Uuid.FromGuid(Guid.NewGuid()).ToString()
						},
						CustomMetadata = ByteString.Empty,
						Data = ByteString.CopyFromUtf8("{}"),
						Metadata = {
							{GrpcMetadata.Type, "$UserUpdated"},
							{GrpcMetadata.ContentType, GrpcMetadata.ContentTypes.ApplicationOctetStream}
						}
					}
				});

				
				// invalid json
				await append.RequestStream.WriteAsync(new AppendReq() {
					ProposedMessage = new AppendReq.Types.ProposedMessage {
						Id = new UUID {
							String = Uuid.FromGuid(Guid.NewGuid()).ToString()
						},
						CustomMetadata = ByteString.Empty,
						Data = ByteString.CopyFromUtf8(""),
						Metadata = {
							{GrpcMetadata.Type, "$UserUpdated"},
							{GrpcMetadata.ContentType, GrpcMetadata.ContentTypes.ApplicationOctetStream}
						}
					}
				});

				await append.RequestStream.CompleteAsync();
				await append.ResponseAsync;
				
				try {
					var response = UsersClient.Details(
						new DetailsReq() {},
						GetCallOptions(AdminCredentials));
					
					_users = await response.ResponseStream.ToListAsync();
				} catch (Exception ex) {
					_caughtException = ex;
				}
			}
			
			[Test]
			public void no_exception_is_thrown() {
				Assert.Null(_caughtException);
			}
			
			[Test]
			public void returns_users() {
				Assert.NotNull(_users);
				CollectionAssert
					.IsSubsetOf(Users, _users.Select(u => u.UserDetails.LoginName));
			}
		}
	}
}
