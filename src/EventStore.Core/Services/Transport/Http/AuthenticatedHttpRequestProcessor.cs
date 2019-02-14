using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using EventStore.Common.Log;
using EventStore.Common.Utils;
using EventStore.Core.Bus;
using EventStore.Core.DataStructures;
using EventStore.Core.Messages;
using EventStore.Core.Services.Transport.Http.Messages;
using EventStore.Transport.Http;
using EventStore.Transport.Http.Codecs;
using EventStore.Transport.Http.EntityManagement;
using HttpStatusCode = EventStore.Transport.Http.HttpStatusCode;

namespace EventStore.Core.Services.Transport.Http {
	internal class AuthenticatedHttpRequestProcessor : IHandle<HttpMessage.PurgeTimedOutRequests>,
		IHandle<AuthenticatedHttpRequestMessage> {
		private static readonly ILogger Log = LogManager.GetLoggerFor<AuthenticatedHttpRequestProcessor>();

		private readonly PairingHeap<Tuple<DateTime, HttpEntityManager>> _pending =
			new PairingHeap<Tuple<DateTime, HttpEntityManager>>((x, y) => x.Item1 < y.Item1);

		private readonly bool _doNotTimeout = Application.IsDefined(Application.DoNotTimeoutRequests);

		public void Handle(HttpMessage.PurgeTimedOutRequests message) {
			PurgeTimedOutRequests();
		}

		private void PurgeTimedOutRequests() {
			if (_doNotTimeout)
				return;
			try {
				while (_pending.Count > 0) {
					var req = _pending.FindMin();
					if (req.Item1 <= DateTime.UtcNow || req.Item2.IsProcessing) {
						req = _pending.DeleteMin();
						req.Item2.ReplyStatus(HttpStatusCode.RequestTimeout,
							"Server was unable to handle request in time",
							e => Log.Debug(
								"Error occurred while closing timed out connection (HTTP service core): {e}.",
								e.Message));
					} else
						break;
				}
			} catch (Exception exc) {
				Log.ErrorException(exc, "Error purging timed out requests in HTTP request processor.");
			}
		}

		public void Handle(AuthenticatedHttpRequestMessage message) {
			ProcessRequest(message.HttpService, message.Entity);
		}

		private void ProcessRequest(HttpService httpService, HttpEntity httpEntity) {
			var request = httpEntity.Request;
			try {
				var allMatches = httpService.GetAllUriMatches(request.Url);
				if (allMatches.Count == 0) {
					NotFound(httpEntity);
					return;
				}

				var allowedMethods = GetAllowedMethods(allMatches);

				if (request.HttpMethod.Equals(HttpMethod.Options, StringComparison.OrdinalIgnoreCase)) {
					RespondWithOptions(httpEntity, allowedMethods);
					return;
				}

				var match = allMatches.LastOrDefault(
					m => m.ControllerAction.HttpMethod.Equals(request.HttpMethod, StringComparison.OrdinalIgnoreCase));
				if (match == null) {
					MethodNotAllowed(httpEntity, allowedMethods);
					return;
				}

				ICodec requestCodec = null;
				var supportedRequestCodecs = match.ControllerAction.SupportedRequestCodecs;
				if (supportedRequestCodecs != null && supportedRequestCodecs.Length > 0) {
					requestCodec = SelectRequestCodec(request.HttpMethod, request.ContentType, supportedRequestCodecs);
					if (requestCodec == null) {
						BadContentType(httpEntity, "Invalid or missing Content-Type");
						return;
					}
				}

				ICodec responseCodec = SelectResponseCodec(request.QueryString,
					request.AcceptTypes,
					match.ControllerAction.SupportedResponseCodecs,
					match.ControllerAction.DefaultResponseCodec);
				if (responseCodec == null) {
					BadCodec(httpEntity, "Requested URI is not available in requested format");
					return;
				}


				try {
					var manager =
						httpEntity.CreateManager(requestCodec, responseCodec, allowedMethods, satisfied => { });
					var reqParams = match.RequestHandler(manager, match.TemplateMatch);
					if (!reqParams.IsDone)
						_pending.Add(Tuple.Create(DateTime.UtcNow + reqParams.Timeout, manager));
				} catch (Exception exc) {
					Log.ErrorException(exc, "Error while handling HTTP request '{url}'.", request.Url);
					InternalServerError(httpEntity);
				}
			} catch (Exception exc) {
				Log.ErrorException(exc, "Unhandled exception while processing HTTP request at [{listenPrefixes}].",
					string.Join(", ", httpService.ListenPrefixes));
				InternalServerError(httpEntity);
			}

			PurgeTimedOutRequests();
		}

		private static string[] GetAllowedMethods(List<UriToActionMatch> allMatches) {
			var allowedMethods = new string[allMatches.Count + 1];
			for (int i = 0; i < allMatches.Count; ++i) {
				allowedMethods[i] = allMatches[i].ControllerAction.HttpMethod;
			}

			//add options to the list of allowed request methods
			allowedMethods[allMatches.Count] = HttpMethod.Options;
			return allowedMethods;
		}

		private void RespondWithOptions(HttpEntity httpEntity, string[] allowed) {
			var entity = httpEntity.CreateManager(Codec.NoCodec, Codec.NoCodec, allowed, _ => { });
			entity.ReplyStatus(HttpStatusCode.OK, "OK",
				e => Log.Debug("Error while closing HTTP connection (http service core): {e}.", e.Message));
		}

		private void MethodNotAllowed(HttpEntity httpEntity, string[] allowed) {
			var entity = httpEntity.CreateManager(Codec.NoCodec, Codec.NoCodec, allowed, _ => { });
			entity.ReplyStatus(HttpStatusCode.MethodNotAllowed, "Method Not Allowed",
				e => Log.Debug("Error while closing HTTP connection (HTTP service core): {e}.", e.Message));
		}

		private void NotFound(HttpEntity httpEntity) {
			var entity = httpEntity.CreateManager();
			entity.ReplyStatus(HttpStatusCode.NotFound, "Not Found",
				e => Log.Debug("Error while closing HTTP connection (HTTP service core): {e}.", e.Message));
		}

		private void InternalServerError(HttpEntity httpEntity) {
			var entity = httpEntity.CreateManager();
			entity.ReplyStatus(HttpStatusCode.InternalServerError, "Internal Server Error",
				e => Log.Debug("Error while closing HTTP connection (HTTP service core): {e}.", e.Message));
		}

		private void BadCodec(HttpEntity httpEntity, string reason) {
			var entity = httpEntity.CreateManager();
			entity.ReplyStatus(HttpStatusCode.NotAcceptable, reason,
				e => Log.Debug("Error while closing HTTP connection (HTTP service core): {e}.", e.Message));
		}

		private void BadContentType(HttpEntity httpEntity, string reason) {
			var entity = httpEntity.CreateManager();
			entity.ReplyStatus(HttpStatusCode.UnsupportedMediaType, reason,
				e => Log.Debug("Error while closing HTTP connection (HTTP service core): {e}.", e.Message));
		}

		private ICodec SelectRequestCodec(string method, string contentType, ICodec[] supportedCodecs) {
			if (string.IsNullOrEmpty(contentType))
				return supportedCodecs != null && supportedCodecs.Length > 0 ? null : Codec.NoCodec;
			switch (method.ToUpper()) {
				case HttpMethod.Post:
				case HttpMethod.Put:
				case HttpMethod.Delete:
					return supportedCodecs.SingleOrDefault(c => c.CanParse(MediaType.Parse(contentType)));

				default:
					return Codec.NoCodec;
			}
		}

		private ICodec SelectResponseCodec(NameValueCollection query, string[] acceptTypes, ICodec[] supported,
			ICodec @default) {
			var requestedFormat = GetFormatOrDefault(query);
			if (requestedFormat == null && acceptTypes.IsEmpty())
				return @default;

			if (requestedFormat != null)
				return supported.FirstOrDefault(c => c.SuitableForResponse(MediaType.Parse(requestedFormat)));

			return acceptTypes.Select(MediaType.TryParse)
				.Where(x => x != null)
				.OrderByDescending(v => v.Priority)
				.Select(type => supported.FirstOrDefault(codec => codec.SuitableForResponse(type)))
				.FirstOrDefault(corresponding => corresponding != null);
		}

		private string GetFormatOrDefault(NameValueCollection query) {
			var format = (query != null && query.Count > 0) ? query.Get("format") : null;
			if (format == null)
				return null;
			switch (format.ToLower()) {
				case "json":
					return ContentType.Json;
				case "text":
					return ContentType.PlainText;
				case "xml":
					return ContentType.Xml;
				case "atom":
					return ContentType.Atom;
				case "atomxj":
					return ContentType.AtomJson;
				case "atomsvc":
					return ContentType.AtomServiceDoc;
				case "atomsvcxj":
					return ContentType.AtomServiceDocJson;
				default:
					throw new NotSupportedException("Unknown format requested");
			}
		}
	}
}
