using System;
using System.Threading.Tasks;
using EventStore.Core.Bus;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Services.TimerService;
using EventStore.Core.Services.Transport.Http.Messages;
using EventStore.Transport.Http;
using Microsoft.AspNetCore.Http;
using Serilog;

namespace EventStore.Core.Services.Transport.Http
{
	public class InternalDispatcherEndpoint :
		IHandle<SystemMessage.SystemInit>,
		IHandle<HttpMessage.PurgeTimedOutRequests> {

		private static readonly ILogger Log = Serilog.Log.ForContext<AuthorizationMiddleware>();
		private readonly IPublisher _output;
		private readonly MultiQueuedHandler _requestsMultiHandler;
		private static readonly TimeSpan UpdateInterval = TimeSpan.FromSeconds(1);
		private readonly Message _schedulePurge;

		public InternalDispatcherEndpoint(IPublisher output, MultiQueuedHandler requestsMultiHandler) {
			_output = output;
			_requestsMultiHandler = requestsMultiHandler;

			_schedulePurge = TimerMessage.Schedule.Create(
				UpdateInterval,
				new PublishEnvelope(output),
				new HttpMessage.PurgeTimedOutRequests());

		}

		public void Handle(SystemMessage.SystemInit _) {
			_output.Publish(_schedulePurge);
		}

		public void Handle(HttpMessage.PurgeTimedOutRequests message) {
			_requestsMultiHandler.PublishToAll(message);
			_output.Publish(_schedulePurge);
		}

		public Task InvokeAsync(HttpContext context) {
			
			if (InternalHttpHelper.TryGetInternalContext(context, out var manager, out var match, out var tcs)) {
				_requestsMultiHandler.Publish(new AuthenticatedHttpRequestMessage(manager, match));
				return tcs.Task;
			}
			Log.Error("Failed to get internal http components for request {requestId}", context.TraceIdentifier);
			context.Response.StatusCode = HttpStatusCode.InternalServerError;
			return Task.CompletedTask;
		}
	}
}
