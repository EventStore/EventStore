using System;
using EventStore.Core;
using EventStore.Core.Services.Transport.Grpc;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;
using MidFunc = System.Func<
	Microsoft.AspNetCore.Http.HttpContext,
	System.Func<System.Threading.Tasks.Task>,
	System.Threading.Tasks.Task>;

namespace EventStore.ClusterNode {
	public class ClusterVNodeStartup : IStartup {
		private static readonly MediaTypeHeaderValue Grpc = new MediaTypeHeaderValue("application/grpc");
		private readonly ClusterVNode _node;

		public ClusterVNodeStartup(ClusterVNode node) {
			if (node == null) throw new ArgumentNullException(nameof(node));
			_node = node;
		}

		public IServiceProvider ConfigureServices(IServiceCollection services) => services
			.AddSingleton(_node)
			.AddRouting()
			.AddGrpc().Services
			.BuildServiceProvider();

		public void Configure(IApplicationBuilder app) =>
			app.MapWhen(
					IsGrpc,
					inner => inner.UseRouting().UseEndpoints(endpoints => endpoints.MapGrpcService<Streams>()))
				.Use(_node.ExternalHttp)
				.Use(_node.InternalHttp);

		private static bool IsGrpc(HttpContext context) =>
			context.Request.Headers.TryGetValue("content-type", out var contentType) &&
			MediaTypeHeaderValue.TryParse(new StringSegment(contentType), out var contentTypeHeader) &&
			contentTypeHeader.Type == Grpc.Type &&
			contentTypeHeader.SubTypeWithoutSuffix == Grpc.SubTypeWithoutSuffix;
	}
}
