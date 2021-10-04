using System.Linq;
using System.Threading.Tasks;
using EventStore.Client;
using EventStore.Client.ClientCapabilities;
using Grpc.AspNetCore.Server;
using Grpc.Core;
using Microsoft.AspNetCore.Routing;

namespace EventStore.Core.Services.Transport.Grpc {
	internal partial class ClientCapabilities
		: EventStore.Client.ClientCapabilities.ClientCapabilities.ClientCapabilitiesBase {
		public const int ApiVersion = 1;
		private readonly EndpointDataSource _endpointDataSource;

		public ClientCapabilities(EndpointDataSource endpointDataSource) {
			_endpointDataSource = endpointDataSource;
		}

		public override Task<SupportedMethods> GetSupportedMethods(Empty request, ServerCallContext context) {
			var supportedEndpoints = _endpointDataSource.Endpoints
				.Select(x => x.Metadata.FirstOrDefault(m => m is GrpcMethodMetadata))
				.OfType<GrpcMethodMetadata>()
				.Where(x => x.Method.ServiceName.Contains("client"))
				.Select(x => new SupportedMethod {
					MethodName = x.Method.Name,
					ServiceName = x.Method.ServiceName
				});

			var result = new SupportedMethods {
				EventStoreServerVersion = EventStore.Common.Utils.VersionInfo.Version
			};
			result.Methods.AddRange(supportedEndpoints.Distinct());
			return Task.FromResult(result);
		}
	}
}
