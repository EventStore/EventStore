using System;

namespace EventStore.Transport.Http.Client
{
    public interface IHttpClient
    {
        void Get(string url, TimeSpan timeout, Action<HttpResponse> onSuccess, Action<Exception> onException);
        void Post(string url, string request, string contentType, TimeSpan timeout, Action<HttpResponse> onSuccess, Action<Exception> onException);

        void Delete(string url, TimeSpan timeout, Action<HttpResponse> onSuccess, Action<Exception> onException);
        void Put(string url, string request, string contentType, TimeSpan timeout, Action<HttpResponse> onSuccess, Action<Exception> onException);
    }
}
