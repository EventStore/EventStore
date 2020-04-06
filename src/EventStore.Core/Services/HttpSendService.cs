using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Diagnostics;
using EventStore.Common.Utils;
using EventStore.Core.Bus;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Services.Histograms;
using EventStore.Core.Services.Transport.Http;
using EventStore.Transport.Http;
using EventStore.Transport.Http.EntityManagement;
using HttpStatusCode = EventStore.Transport.Http.HttpStatusCode;
using ILogger = Serilog.ILogger;

namespace EventStore.Core.Services {
	public class HttpSendService : IHttpForwarder,
		IHandle<SystemMessage.StateChangeMessage>,
		IHandle<HttpMessage.HttpSend> {
		private static readonly ILogger Log = Serilog.Log.ForContext<HttpSendService>();
		private static HttpClient _client = new HttpClient();

		private readonly Stopwatch _watch = Stopwatch.StartNew();
		private readonly HttpMessagePipe _httpPipe;
		private readonly bool _forwardRequests;
		private const string _httpSendHistogram = "http-send";
		private VNodeInfo _leaderInfo;

		public HttpSendService(HttpMessagePipe httpPipe, bool forwardRequests) {
			Ensure.NotNull(httpPipe, "httpPipe");
			_httpPipe = httpPipe;
			_forwardRequests = forwardRequests;
		}

		public void Handle(SystemMessage.StateChangeMessage message) {
			switch (message.State) {
				case VNodeState.PreReplica:
				case VNodeState.CatchingUp:
				case VNodeState.Clone:
				case VNodeState.Follower:
				case VNodeState.PreReadOnlyReplica:
				case VNodeState.ReadOnlyReplica:
					_leaderInfo = ((SystemMessage.ReplicaStateMessage)message).Leader;
					break;
				case VNodeState.Initializing:
				case VNodeState.DiscoverLeader:
				case VNodeState.Unknown:
				case VNodeState.PreLeader:
				case VNodeState.Leader:
				case VNodeState.ResigningLeader:
				case VNodeState.Manager:
				case VNodeState.ShuttingDown:
				case VNodeState.Shutdown:
				case VNodeState.ReadOnlyLeaderless:
					_leaderInfo = null;
					break;
				default:
					throw new Exception(string.Format("Unknown node state: {0}.", message.State));
			}
		}

		public void Handle(HttpMessage.HttpSend message) {
			var deniedToHandle = message.Message as HttpMessage.DeniedToHandle;
			if (deniedToHandle != null) {
				int code;
				switch (deniedToHandle.Reason) {
					case DenialReason.ServerTooBusy:
						code = HttpStatusCode.ServiceUnavailable;
						break;
					default:
						throw new ArgumentOutOfRangeException();
				}

				var start = _watch.ElapsedTicks;
				message.HttpEntityManager.ReplyStatus(
					code,
					deniedToHandle.Details,
					exc => Log.Debug("Error occurred while replying to HTTP with message {message}: {e}.",
						message.Message, exc.Message));
				HistogramService.SetValue(_httpSendHistogram,
					(long)((((double)_watch.ElapsedTicks - start) / Stopwatch.Frequency) * 1000000000));
			} else {
				var response = message.Data;
				var config = message.Configuration;
				var start = _watch.ElapsedTicks;
				if (response is byte[]) {
					message.HttpEntityManager.ReplyContent(
						response as byte[],
						config.Code,
						config.Description,
						config.ContentType,
						config.Headers,
						exc => Log.Debug("Error occurred while replying to HTTP with message {message}: {e}.",
							message.Message, exc.Message));
				} else {
					message.HttpEntityManager.ReplyTextContent(
						response as string,
						config.Code,
						config.Description,
						config.ContentType,
						config.Headers,
						exc => Log.Debug("Error occurred while replying to HTTP with message {message}: {e}.",
							message.Message, exc.Message));
				}

				HistogramService.SetValue(_httpSendHistogram,
					(long)((((double)_watch.ElapsedTicks - start) / Stopwatch.Frequency) * 1000000000));
			}
		}

		bool IHttpForwarder.ForwardRequest(HttpEntityManager manager) {
			var leaderInfo = _leaderInfo;
			if (_forwardRequests && leaderInfo != null) {
				var srcUrl = manager.RequestedUrl;
				var srcBase = new Uri($"{srcUrl.Scheme}://{srcUrl.Host}:{srcUrl.Port}/",
					UriKind.Absolute);
				var baseUri = new Uri($"{srcUrl.Scheme}://{leaderInfo.ExternalHttp}/");
				var forwardUri = new Uri(baseUri, srcBase.MakeRelativeUri(srcUrl));
				ForwardRequest(manager, forwardUri);
				return true;
			}

			return false;
		}

		private static void ForwardRequest(HttpEntityManager manager, Uri forwardUri) {
			var srcReq = manager.HttpEntity.Request;
			var request = new HttpRequestMessage();
			request.RequestUri = forwardUri;
			request.Method = new System.Net.Http.HttpMethod(srcReq.HttpMethod);

			var hasContentLength = false;
			// Copy unrestricted headers (including cookies, if any)
			foreach (var headerKey in srcReq.GetHeaderKeys()) {
				try {
					switch (headerKey.ToLower()) {
						case "accept":
							request.Headers.Accept.ParseAdd(srcReq.GetHeaderValues(headerKey).ToString());
							break;
						case "connection":
							break;
						case "content-type":
							break;
						case "content-length":
							hasContentLength = true;
							break;
						case "date":
							request.Headers.Date = DateTime.Parse(srcReq.GetHeaderValues(headerKey).ToString());
							break;
						case "expect":
							break;
						case "host":
							request.Headers.Host = $"{forwardUri.Host}:{forwardUri.Port}";
							break;
						case "if-modified-since":
							request.Headers.IfModifiedSince =
								DateTime.Parse(srcReq.GetHeaderValues(headerKey).ToString());
							break;
						case "proxy-connection":
							break;
						case "range":
							break;
						case "referer":
							request.Headers.Referrer = new Uri(srcReq.GetHeaderValues(headerKey).ToString());
							break;
						case "transfer-encoding":
							request.Headers.TransferEncoding.ParseAdd(srcReq.GetHeaderValues(headerKey).ToString());
							break;
						case "user-agent":
							request.Headers.UserAgent.ParseAdd(srcReq.GetHeaderValues(headerKey).ToString());
							break;

						default:
							request.Headers.Add(headerKey, srcReq.GetHeaderValues(headerKey).ToString());
							break;
					}
				} catch (System.FormatException) {
					request.Headers.TryAddWithoutValidation(headerKey, srcReq.GetHeaderValues(headerKey).ToString());
				}
			}

			if (!request.Headers.Contains(ProxyHeaders.XForwardedHost)) {
				request.Headers.Add(ProxyHeaders.XForwardedHost, string.Format("{0}:{1}",
					manager.RequestedUrl.Host, manager.RequestedUrl.Port));
			}

			// Copy content (if content body is allowed)
			if (!string.Equals(srcReq.HttpMethod, "GET", StringComparison.OrdinalIgnoreCase)
			    && !string.Equals(srcReq.HttpMethod, "HEAD", StringComparison.OrdinalIgnoreCase)
			    && hasContentLength) {
				var streamContent = new StreamContent(srcReq.InputStream);
				streamContent.Headers.ContentLength = srcReq.ContentLength64;
				request.Content = streamContent;

				MediaTypeHeaderValue contentType;
				if (MediaTypeHeaderValue.TryParse(srcReq.ContentType, out contentType)) {
					streamContent.Headers.ContentType = contentType;
				}
			}

			ForwardResponse(manager, request);
		}

		private static void ForwardReplyFailed(HttpEntityManager manager) {
			manager.ReplyStatus(HttpStatusCode.InternalServerError, "Error while forwarding request", _ => { });
		}

		private static void ForwardResponse(HttpEntityManager manager, HttpRequestMessage request) {
			_client.SendAsync(request)
				.ContinueWith(t => {
					HttpResponseMessage response;
					try {
						response = t.Result;
					} catch (Exception ex) {
						Log.Debug("Error in SendAsync for forwarded request for '{requestedUrl}': {e}.",
							manager.RequestedUrl, ex.InnerException.Message);
						ForwardReplyFailed(manager);
						return;
					}

					manager.ForwardReply(response,
						exc => Log.Debug("Error forwarding response for '{requestedUrl}': {e}.", manager.RequestedUrl,
							exc.Message));
				});
		}
	}
}
