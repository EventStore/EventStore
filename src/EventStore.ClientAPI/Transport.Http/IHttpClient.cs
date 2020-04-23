using System;
using EventStore.ClientAPI.SystemData;

namespace EventStore.ClientAPI.Transport.Http {
	/// <summary>
	/// An IHttpClient.
	/// </summary>
	public interface IHttpClient {
		/// <summary>
		/// Invokes a GET request.
		/// </summary>
		/// <param name="url"></param>
		/// <param name="userCredentials"></param>
		/// <param name="onSuccess"></param>
		/// <param name="onException"></param>
		/// <param name="hostHeader"></param>
		void Get(string url, UserCredentials userCredentials,
			Action<HttpResponse> onSuccess, Action<Exception> onException,
			string hostHeader = "");

		/// <summary>
		/// Invokes a POST request.
		/// </summary>
		/// <param name="url"></param>
		/// <param name="body"></param>
		/// <param name="contentType"></param>
		/// <param name="userCredentials"></param>
		/// <param name="onSuccess"></param>
		/// <param name="onException"></param>
		void Post(string url, string body, string contentType, UserCredentials userCredentials,
			Action<HttpResponse> onSuccess, Action<Exception> onException);

		/// <summary>
		/// Invokes a DELETE request.
		/// </summary>
		/// <param name="url"></param>
		/// <param name="userCredentials"></param>
		/// <param name="onSuccess"></param>
		/// <param name="onException"></param>
		void Delete(string url, UserCredentials userCredentials,
			Action<HttpResponse> onSuccess, Action<Exception> onException);

		/// <summary>
		/// Invokes a PUT request.
		/// </summary>
		/// <param name="url"></param>
		/// <param name="body"></param>
		/// <param name="contentType"></param>
		/// <param name="userCredentials"></param>
		/// <param name="onSuccess"></param>
		/// <param name="onException"></param>
		void Put(string url, string body, string contentType, UserCredentials userCredentials,
			Action<HttpResponse> onSuccess, Action<Exception> onException);
	}
}
