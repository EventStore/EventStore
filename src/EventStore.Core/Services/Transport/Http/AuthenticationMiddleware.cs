﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EventStore.Core.Services.Transport.Http.Authentication;
using EventStore.Plugins.Authentication;
using EventStore.Transport.Http;
using Grpc.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Newtonsoft.Json;

namespace EventStore.Core.Services.Transport.Http {
	public class AuthenticationMiddleware : IMiddleware {
		private readonly IAuthenticationProvider _authenticationProvider;
		private readonly IReadOnlyList<IHttpAuthenticationProvider> _httpAuthenticationProviders;

		public AuthenticationMiddleware(IReadOnlyList<IHttpAuthenticationProvider> httpAuthenticationProviders, IAuthenticationProvider authenticationProvider) {
			_httpAuthenticationProviders = httpAuthenticationProviders;
			_authenticationProvider = authenticationProvider;
		}

		public async Task InvokeAsync(HttpContext context, RequestDelegate next) {
			try {
				if (context.IsGrpc()) {
					await HandleAsGrpcAsync(context, next).ConfigureAwait(false);
				} else {
					await HandleAsHttpAsync(context, next).ConfigureAwait(false);
				}
			} catch (Exception ex) when (ex is OperationCanceledException or TaskCanceledException) {
				// ignore request aborted
			}
		}

		private async Task HandleAsHttpAsync(HttpContext context, RequestDelegate next) {
			if (!TrySelectProvider(context, out var authenticationRequest)) {
				await AddHttp1ChallengeHeaders(context).ConfigureAwait(false);
				return;
			}

			var (status, principal) = await authenticationRequest.AuthenticateAsync().ConfigureAwait(false);

			switch (status) {
				case HttpAuthenticationRequestStatus.Authenticated:
					context.User = principal;
					await next(context).ConfigureAwait(false);
					break;
				case HttpAuthenticationRequestStatus.Error:
					context.Response.StatusCode = HttpStatusCode.InternalServerError;
					break;
				case HttpAuthenticationRequestStatus.NotReady:
					context.Response.StatusCode = HttpStatusCode.ServiceUnavailable;
					context.Response.Headers.Append("Retry-After", "5");
					break;
				case HttpAuthenticationRequestStatus.Unauthenticated:
				default:
					await AddHttp1ChallengeHeaders(context).ConfigureAwait(false);
					break;
			}
		}

		private async Task HandleAsGrpcAsync(HttpContext context, RequestDelegate next) {
			var (status, principal) = TrySelectProvider(context, out var authenticationRequest)
				? await authenticationRequest.AuthenticateAsync().ConfigureAwait(false)
				: (HttpAuthenticationRequestStatus.Unauthenticated, default);

			GrpcProtocolHelpers.AddProtocolHeaders(context.Response);
			var trailersDestination = GrpcProtocolHelpers.GetTrailersDestination(context.Response);

			Status grpcStatus;
			switch (status) {
				case HttpAuthenticationRequestStatus.Authenticated:
					context.User = principal;
					await next(context).ConfigureAwait(false);
					return;
				case HttpAuthenticationRequestStatus.Error:
					grpcStatus = new Status(StatusCode.Unknown, "Internal server error");
					break;
				case HttpAuthenticationRequestStatus.NotReady:
					// negative RetryPushbackHeader asks the grpc client not to retry the request.
					// the user is then explicit about what retries they want and aware of the delays incurred
					trailersDestination.Append(GrpcProtocolHelpers.RetryPushbackHeader, "-1");
					grpcStatus = new Status(StatusCode.Unavailable, "Server not ready");
					break;
				case HttpAuthenticationRequestStatus.Unauthenticated:
				default:
					grpcStatus = new Status(StatusCode.Unauthenticated, "Unauthenticated");
					break;
			}

			GrpcProtocolHelpers.SetStatus(trailersDestination, grpcStatus);

			// Immediately send remaining response content and trailers
			// If feature is null then reset/abort will still end request, but response won't have trailers
			var completionFeature = context.Features.Get<IHttpResponseBodyFeature>();
			if (completionFeature != null) {
				await completionFeature.CompleteAsync().ConfigureAwait(false);
			}
		}

		private bool TrySelectProvider(HttpContext context, out HttpAuthenticationRequest authenticationRequest) {
			for (int i = 0; i < _httpAuthenticationProviders.Count; i++) {
				if (_httpAuthenticationProviders[i].Authenticate(context, out authenticationRequest)) {
					return true;
				}
			}

			authenticationRequest = default;
			return false;
		}

		private async Task AddHttp1ChallengeHeaders(HttpContext context) {
			context.Response.StatusCode = HttpStatusCode.Unauthorized;
			var authSchemes = _authenticationProvider.GetSupportedAuthenticationSchemes();
			if (authSchemes != null && authSchemes.Any()) {
				//add "X-" in front to prevent any default browser behaviour e.g Basic Auth popups
				context.Response.Headers.Append("WWW-Authenticate", $"X-{authSchemes.First()} realm=\"ESDB\"");
				var properties = _authenticationProvider.GetPublicProperties();
				if (properties != null && properties.Any()) {
					await context.Response.WriteAsync(JsonConvert.SerializeObject(properties))
						.ConfigureAwait(false);
				}
			}
		}
	}
}
