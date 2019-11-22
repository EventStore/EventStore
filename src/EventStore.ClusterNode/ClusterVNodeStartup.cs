using System;
using EventStore.Core;
using EventStore.Core.Services.Transport.Grpc;
using EventStore.Projections.Core.Services.Grpc;
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
		private readonly ClusterVNode _node;

		public ClusterVNodeStartup(ClusterVNode node) {
			if (node == null) throw new ArgumentNullException(nameof(node));
			_node = node;
		}

		public IServiceProvider ConfigureServices(IServiceCollection services) => _node.ConfigureServices(services)
			.BuildServiceProvider();

		public void Configure(IApplicationBuilder app) => _node.Configure(app);
	}
}
