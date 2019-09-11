using System;
using EventStore.ClientAPI.SystemData;

namespace EventStore.ClientAPI.Transport.Http
{
	public interface IHttpClient {
		void Get(string url, UserCredentials userCredentials,
			Action<HttpResponse> onSuccess, Action<Exception> onException,
			string hostHeader = "");

		void Post(string url, string body, string contentType, UserCredentials userCredentials,
			Action<HttpResponse> onSuccess, Action<Exception> onException);

		void Delete(string url, UserCredentials userCredentials,
			Action<HttpResponse> onSuccess, Action<Exception> onException);

		void Put(string url, string body, string contentType, UserCredentials userCredentials,
			Action<HttpResponse> onSuccess, Action<Exception> onException);
	}
}
