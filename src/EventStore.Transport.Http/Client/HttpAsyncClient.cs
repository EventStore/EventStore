using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using EventStore.Common.Utils;
using System.Net.Http;
using System.Threading.Tasks;
using System.Net.Http.Headers;

namespace EventStore.Transport.Http.Client {
	public class HttpAsyncClient : IHttpClient {
		private readonly HttpClient _client;

		static HttpAsyncClient() {
			ServicePointManager.MaxServicePointIdleTime = 10000;
			ServicePointManager.DefaultConnectionLimit = 500;
		}

		public HttpAsyncClient(TimeSpan timeout) {
			_client = new HttpClient();
			_client.Timeout = timeout;
		}

		public void Get(string url, Action<HttpResponse> onSuccess, Action<Exception> onException) {
			Get(url, null, onSuccess, onException);
		}

		public void Get(string url, IEnumerable<KeyValuePair<string, string>> headers, Action<HttpResponse> onSuccess,
			Action<Exception> onException) {
			Ensure.NotNull(url, "url");
			Ensure.NotNull(onSuccess, "onSuccess");
			Ensure.NotNull(onException, "onException");

			Receive(HttpMethod.Get, url, headers, onSuccess, onException);
		}

		public void Post(string url, string body, string contentType, Action<HttpResponse> onSuccess,
			Action<Exception> onException) {
			Post(url, body, contentType, null, onSuccess, onException);
		}

		public void Post(string url, string body, string contentType, IEnumerable<KeyValuePair<string, string>> headers,
			Action<HttpResponse> onSuccess, Action<Exception> onException) {
			Ensure.NotNull(url, "url");
			Ensure.NotNull(body, "body");
			Ensure.NotNull(contentType, "contentType");
			Ensure.NotNull(onSuccess, "onSuccess");
			Ensure.NotNull(onException, "onException");

			Send(HttpMethod.Post, url, body, contentType, headers, onSuccess, onException);
		}

		public void Delete(string url, Action<HttpResponse> onSuccess, Action<Exception> onException) {
			Ensure.NotNull(url, "url");
			Ensure.NotNull(onSuccess, "onSuccess");
			Ensure.NotNull(onException, "onException");

			Receive(HttpMethod.Delete, url, null, onSuccess, onException);
		}

		public void Put(string url, string body, string contentType, Action<HttpResponse> onSuccess,
			Action<Exception> onException) {
			Ensure.NotNull(url, "url");
			Ensure.NotNull(body, "body");
			Ensure.NotNull(contentType, "contentType");
			Ensure.NotNull(onSuccess, "onSuccess");
			Ensure.NotNull(onException, "onException");

			Send(HttpMethod.Put, url, body, contentType, null, onSuccess, onException);
		}

		public void Dispose() {
			_client.Dispose();
		}

		private void Receive(string method, string url, IEnumerable<KeyValuePair<string, string>> headers,
			Action<HttpResponse> onSuccess, Action<Exception> onException) {
			var request = new HttpRequestMessage();
			request.Method = new System.Net.Http.HttpMethod(method);
			request.RequestUri = new Uri(url);

			if (headers != null) {
				foreach (var header in headers) {
					request.Headers.Add(header.Key, header.Value);
				}
			}

			var state = new ClientOperationState(request, onSuccess, onException);
			_client.SendAsync(request).ContinueWith(RequestSent(state));
		}

		private void Send(string method, string url, string body, string contentType,
			IEnumerable<KeyValuePair<string, string>> headers,
			Action<HttpResponse> onSuccess, Action<Exception> onException) {
			var request = new HttpRequestMessage();
			request.Method = new System.Net.Http.HttpMethod(method);
			request.RequestUri = new Uri(url);

			var bodyBytes = Helper.UTF8NoBom.GetBytes(body);
			var stream = new MemoryStream(bodyBytes);
			var content = new StreamContent(stream);
			content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
			content.Headers.ContentLength = bodyBytes.Length;

			request.Content = content;

			if (headers != null) {
				foreach (var header in headers) {
					request.Headers.Add(header.Key, header.Value);
				}
			}

			var state = new ClientOperationState(request, onSuccess, onException);
			_client.SendAsync(request).ContinueWith(RequestSent(state));
		}

		private Action<Task<HttpResponseMessage>> RequestSent(ClientOperationState state) {
			return task => {
				try {
					var responseMsg = task.Result;
					state.Response = new HttpResponse(responseMsg);
					responseMsg.Content.ReadAsStringAsync()
						.ContinueWith(ResponseRead(state));
				} catch (Exception ex) {
					state.Dispose();
					state.OnError(ex);
				}
			};
		}

		private Action<Task<string>> ResponseRead(ClientOperationState state) {
			return task => {
				try {
					state.Response.Body = task.Result;
					state.Dispose();
					state.OnSuccess(state.Response);
				} catch (Exception ex) {
					state.Dispose();
					state.OnError(ex);
				}
			};
		}
	}
}
