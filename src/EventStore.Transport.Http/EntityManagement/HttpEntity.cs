using System;
using System.Collections.Specialized;
using System.Net;
using System.Security.Principal;
using EventStore.Common.Utils;
using EventStore.Transport.Http.Codecs;
using System.Linq;
using Microsoft.Extensions.Primitives;

namespace EventStore.Transport.Http.EntityManagement {
	public class HttpEntity {
		private readonly bool _logHttpRequests;
		public readonly Action OnComplete;
		public readonly Uri RequestedUrl;
		public readonly Uri ResponseUrl;

		public readonly IHttpRequest Request;
		internal readonly IHttpResponse Response;
		public readonly IPrincipal User;

		public HttpEntity(IHttpRequest request, IHttpResponse response, IPrincipal user,
			bool logHttpRequests, IPAddress advertiseAsAddress, int advertiseAsPort, Action onComplete) {
			Ensure.NotNull(request, "request");
			Ensure.NotNull(response, "response");
			Ensure.NotNull(onComplete, nameof(onComplete));

			_logHttpRequests = logHttpRequests;
			OnComplete = onComplete;
			RequestedUrl = BuildRequestedUrl(request, advertiseAsAddress, advertiseAsPort);
			ResponseUrl = BuildRequestedUrl(request, advertiseAsAddress, advertiseAsPort, true);
			Request = request;
			Response = response;
			User = user;
		}

		public static Uri BuildRequestedUrl(IHttpRequest request,
			IPAddress advertiseAsAddress, int advertiseAsPort, bool overridePath = false) {
			var uriBuilder = new UriBuilder(request.Url);
			if (overridePath) {
				uriBuilder.Path = string.Empty;
			}

			if (advertiseAsAddress != null) {
				uriBuilder.Host = advertiseAsAddress.ToString();
			}

			if (advertiseAsPort > 0) {
				uriBuilder.Port = advertiseAsPort;
			}

			var forwardedPortHeaderValue = request.GetHeaderValues(ProxyHeaders.XForwardedPort);
			if (!StringValues.IsNullOrEmpty(forwardedPortHeaderValue)) {
				if (int.TryParse(forwardedPortHeaderValue.First(), out var requestPort)) {
					uriBuilder.Port = requestPort;
				}
			}

			var forwardedProtoHeaderValue = request.GetHeaderValues(ProxyHeaders.XForwardedProto);

			if (!StringValues.IsNullOrEmpty(forwardedProtoHeaderValue)) {
				uriBuilder.Scheme = forwardedProtoHeaderValue.First();
			}

			var forwardedHostHeaderValue = request.GetHeaderValues(ProxyHeaders.XForwardedHost);

			if (!StringValues.IsNullOrEmpty(forwardedHostHeaderValue)) {
				var host = forwardedHostHeaderValue.First();
				if (!string.IsNullOrEmpty(host)) {
					var parts = host.Split(new[] { ":" }, StringSplitOptions.RemoveEmptyEntries);
					uriBuilder.Host = parts[0];
					if (parts.Length > 1 && int.TryParse(parts[1], out var port)) {
						uriBuilder.Port = port;
					}
				}
			}

			var forwardedPrefixHeaderValue = request.GetHeaderValues(ProxyHeaders.XForwardedPrefix);
			if (!string.IsNullOrEmpty(forwardedPrefixHeaderValue)) {
				uriBuilder.Path = forwardedPrefixHeaderValue + uriBuilder.Path;
			}

			return uriBuilder.Uri;
		}

		private HttpEntity(IPrincipal user, bool logHttpRequests) {
			RequestedUrl = null;
			ResponseUrl = null;

			Request = null;
			Response = null;
			User = user;
			_logHttpRequests = logHttpRequests;
			OnComplete = () => { };
		}

		private HttpEntity(HttpEntity httpEntity, IPrincipal user, bool logHttpRequests) {
			RequestedUrl = httpEntity.RequestedUrl;
			ResponseUrl = httpEntity.ResponseUrl;

			Request = httpEntity.Request;
			Response = httpEntity.Response;
			User = user;
			_logHttpRequests = logHttpRequests;
			OnComplete = httpEntity.OnComplete;
		}

		public HttpEntityManager CreateManager(
			ICodec requestCodec, ICodec responseCodec, string[] allowedMethods, Action<HttpEntity> onRequestSatisfied) {
			return new HttpEntityManager(this, allowedMethods, onRequestSatisfied, requestCodec, responseCodec,
				_logHttpRequests, OnComplete);
		}

		public HttpEntityManager CreateManager()
			=> CreateManager(Codec.NoCodec, Codec.NoCodec, Array.Empty<string>(), _ => { });

		public HttpEntity SetUser(IPrincipal user) {
			return new HttpEntity(this, user, _logHttpRequests);
		}
	}
}
