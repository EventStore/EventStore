using System.Linq;
using System.Threading.Tasks;
using EventStore.Client;
using EventStore.Client.ServerFeatures;
using Grpc.AspNetCore.Server;
using Grpc.Core;
using Microsoft.AspNetCore.Routing;

namespace EventStore.Core.Services.Transport.Grpc {
	internal partial class ServerFeatures
		: EventStore.Client.ServerFeatures.ServerFeatures.ServerFeaturesBase {
		public const int ApiVersion = 1;
		private readonly EndpointDataSource _endpointDataSource;

		public ServerFeatures(EndpointDataSource endpointDataSource) {
			_endpointDataSource = endpointDataSource;
		}

		public override Task<SupportedMethods> GetSupportedMethods(Empty request, ServerCallContext context) {
			var supportedEndpoints = _endpointDataSource.Endpoints
				.Select(x => x.Metadata.FirstOrDefault(m => m is GrpcMethodMetadata))
				.OfType<GrpcMethodMetadata>()
				.Where(x => x.Method.ServiceName.Contains("client"))
				.Select(x => {
					var method = new SupportedMethod {
						MethodName = x.Method.Name.ToLower(),
						ServiceName = x.Method.ServiceName.ToLower(),
					};
					if (x.Method.ServiceName.Contains("PersistentSubscriptions")) {
						method.Features.AddRange(new[] {"stream", "all"});
					} else if (x.Method.ServiceName.Contains("Streams") && x.Method.Name.Contains("Read")) {
						method.Features.AddRange(new[] {"position", "events"});
					}
					return method;
				});

			var versionParts = EventStore.Common.Utils.VersionInfo.Version.Split('.');
			var result = new SupportedMethods {
				EventStoreServerVersion = string.Join('.', versionParts.Take(3))
			};
			result.Methods.AddRange(supportedEndpoints.Distinct());
			return Task.FromResult(result);
		}
	}
}
