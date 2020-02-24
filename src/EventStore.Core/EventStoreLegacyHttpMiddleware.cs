using System;
using System.Linq;
using System.Threading.Tasks;
using EventStore.Core.Services.Transport.Http;
using EventStore.Transport.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace EventStore.Core {
	public static class EventStoreLegacyHttpMiddleware {
		public static IApplicationBuilder UseLegacyHttp(
			this IApplicationBuilder app,
			params IHttpService[] httpServices) {
			if (app == null) throw new ArgumentNullException(nameof(app));
			if (httpServices == null) throw new ArgumentNullException(nameof(httpServices));

			var actions = httpServices
				.SelectMany(x => x.Actions.Select(action =>
					(ConvertToRoute(action.UriTemplate), action.HttpMethod, action.RequiredAuthorizationLevel)))
				.ToArray();
			return actions
				.Concat(actions
					.Select(_ => _.Item1)
					.Select(route => (route, HttpMethod.Options, AuthorizationLevel.None)))
				.Distinct()
				.Aggregate(app.UseRouting(), RegisterRoute);

			IApplicationBuilder RegisterRoute(
				IApplicationBuilder builder,
				(string route, string method, AuthorizationLevel authorizationLevel) action) => builder
				.UseEndpoints(routeBuilder => routeBuilder
					.MapMethods(
						action.route,
						action.method == HttpMethod.Get
							? new[] {HttpMethod.Get, HttpMethod.Head}
							: new[] {action.method},
						HandleRequest)
					.WithMetadata(new OptionalAuthorizationLevel(action.authorizationLevel)));

			static string ConvertToRoute(string uriTemplate) {
				var route = uriTemplate.Split('?').First();
				return System.Net.WebUtility.UrlDecode(route.EndsWith("/") ? route[..^1] : route);
			}

			Task HandleRequest(HttpContext context) => httpServices
				.FirstOrDefault(service => service
					.EndPoints.Any(ipEndPoint =>
						ipEndPoint.Port == context.Request.Host.Port))?
				.AppFunc?
				.Invoke(context) ?? Task.CompletedTask;
		}
	}
}
