using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using EventStore.Core.Bus;
using EventStore.Core.Messages;
using EventStore.Core.Tests.Helpers;
using EventStore.Plugins.Subsystems;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.VNode;

[TestFixture]
public class http_pipeline_should : SpecificationWithDirectory {
	private const string SubsystemUnprotectedEndpoint = "/my-subsystem/unprotected";
	private const string SubsystemProtectedEndpoint = "/my-subsystem/protected";
	private const string ControllerProtectedEndpoint = "/my-controller/protected";
	private const string ControllerUnprotectedEndpoint = "/my-controller/unprotected";

	[Test]
	public async Task allow_subsystems_to_protect_their_endpoints() {
		var tcs = new TaskCompletionSource();

		await using var node = new MiniNode<LogFormat.V2,string>(PathName, subsystems: [ new FakeProtectedSubSystem() ]);
		node.Node.MainBus.Subscribe(new AdHocHandler<SystemMessage.SystemReady>( t => {
			tcs.TrySetResult();
		}));

		await node.Start();

		// subsystem endpoint protected manually by code
		var result = await node.HttpClient.GetAsync(SubsystemProtectedEndpoint);
		Assert.AreEqual(HttpStatusCode.Unauthorized, result.StatusCode);

		result = await SendAuthenticatedGetAsync(SubsystemProtectedEndpoint);
		Assert.AreEqual(HttpStatusCode.OK, result.StatusCode);

		// subsystem unprotected endpoint
		result = await node.HttpClient.GetAsync(SubsystemUnprotectedEndpoint);
		Assert.AreEqual(HttpStatusCode.OK, result.StatusCode);

		// controller protected by Authorize attribute
		result = await node.HttpClient.GetAsync(ControllerProtectedEndpoint);
		Assert.AreEqual(HttpStatusCode.Unauthorized, result.StatusCode);

		result = await SendAuthenticatedGetAsync(ControllerProtectedEndpoint);
		Assert.AreEqual(HttpStatusCode.OK, result.StatusCode);

		// controller unprotected endpoint
		result = await node.HttpClient.GetAsync(ControllerUnprotectedEndpoint);
		Assert.AreEqual(HttpStatusCode.OK, result.StatusCode);


		Task<HttpResponseMessage> SendAuthenticatedGetAsync(string endpoint) => node
			.HttpClient.SendAsync(new HttpRequestMessage() {
				Method = HttpMethod.Get,
				RequestUri = new Uri(node.HttpClient.BaseAddress!, endpoint),
				Headers = {
					Authorization = new AuthenticationHeaderValue("Basic",
						Convert.ToBase64String(Encoding.ASCII.GetBytes("admin:changeit")))
				}
		});
	}

	private class FakeProtectedSubSystem : ISubsystem {
		public string Name => nameof(FakeProtectedSubSystem);
		public IApplicationBuilder Configure(IApplicationBuilder builder) => builder
			.UseEndpoints(ep => {
				ep.MapControllers();
				ep.MapGet(SubsystemProtectedEndpoint, context => {

					if (context.User.IsInRole("$ops") || context.User.IsInRole("$admins")) {
						context.Response.StatusCode = (int)HttpStatusCode.OK;
					} else {
						context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
					}

					return Task.CompletedTask;
				});
				ep.MapGet(SubsystemUnprotectedEndpoint, context => {

					context.Response.StatusCode = (int)HttpStatusCode.OK;
					return Task.CompletedTask;
				});
			});

		public IServiceCollection ConfigureServices(IServiceCollection services, IConfiguration _) => services
			.AddControllers()
			.AddApplicationPart(typeof(FakeController).Assembly)
			.Services;

		public Task Start() => Task.CompletedTask;

		public Task Stop() => Task.CompletedTask;
	}
}

[ApiController]
[Route("/my-controller")]
public class FakeController : ControllerBase {
	[HttpGet]
	[Authorize(Roles = "$admins")]
	[Route("protected")]
	public string Hello() => "world";

	[HttpGet]
	[Route("unprotected")]
	public string Open() => "source";
}
