using System;

namespace EventStore.Transport.Http.Client {
	public interface IHttpClient {
		void Get(string url, Action<HttpResponse> onSuccess, Action<Exception> onException);

		void Post(string url, string request, string contentType, Action<HttpResponse> onSuccess,
			Action<Exception> onException);

		void Delete(string url, Action<HttpResponse> onSuccess, Action<Exception> onException);

		void Put(string url, string request, string contentType, Action<HttpResponse> onSuccess,
			Action<Exception> onException);
	}
}
