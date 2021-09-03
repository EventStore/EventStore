using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using EventStore.Client;
using EventStore.Client.ClientCapabilities;
using EventStore.Core.Tests.Integration;
using Google.Protobuf.Reflection;
using Grpc.Net.Client;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.Transport.Grpc.ClientCapabilitiesTests {
	public class ClientCapabilitiesTest {
		[TestFixture(typeof(LogFormat.V2), typeof(string))]
		[TestFixture(typeof(LogFormat.V3), typeof(uint))]
		public class
			when_getting_supported_methods<TLogFormat, TStreamId> : specification_with_cluster<TLogFormat, TStreamId> {

			private List<SupportedMethod> _supportedEndPoints = new ();
			private List<SupportedMethod> _expectedEndPoints = new ();
			private string _serverVersion;

			protected override async Task Given() {
				_expectedEndPoints.AddRange(GetEndPoints(Client.Streams.Streams.Descriptor));
				_expectedEndPoints.AddRange(GetEndPoints(Client.PersistentSubscriptions.PersistentSubscriptions.Descriptor));
				_expectedEndPoints.AddRange(GetEndPoints(Client.Operations.Operations.Descriptor));
				_expectedEndPoints.AddRange(GetEndPoints(Client.Users.Users.Descriptor));
				_expectedEndPoints.AddRange(GetEndPoints(Client.Gossip.Gossip.Descriptor));
				_expectedEndPoints.AddRange(GetEndPoints(Client.Monitoring.Monitoring.Descriptor));
				_expectedEndPoints.AddRange(GetEndPoints(ClientCapabilities.Descriptor));

				var node = GetLeader();
				await Task.WhenAll(node.AdminUserCreated, node.Started);

				using var channel = GrpcChannel.ForAddress(new Uri($"https://{node.HttpEndPoint}"),
					new GrpcChannelOptions {
						HttpClient = new HttpClient(new SocketsHttpHandler {
							SslOptions = {
								RemoteCertificateValidationCallback = delegate { return true; }
							}
						}, true)
					});
				var client = new ClientCapabilities.ClientCapabilitiesClient(channel);

				var resp = await client.GetSupportedMethodsAsync(new Empty());
				_supportedEndPoints = resp.Methods.ToList();
				_serverVersion = resp.EventStoreServerVersion;

			}
			private SupportedMethod[] GetEndPoints(ServiceDescriptor desc) =>
				desc.Methods.Select(x => new SupportedMethod {MethodName = x.Name, ServiceName = x.Service.FullName}).ToArray();

			[Test]
			public void should_receive_expected_endpoints() {
				CollectionAssert.AreEquivalent(_expectedEndPoints, _supportedEndPoints);
			}

			[Test]
			public void should_receive_the_correct_eventstore_version() {
				Assert.AreEqual(EventStore.Common.Utils.VersionInfo.Version, _serverVersion);
			}
		}
	}
}

