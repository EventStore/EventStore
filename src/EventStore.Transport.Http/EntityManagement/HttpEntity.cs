using System;
using System.Collections.Specialized;
using System.Net;
using System.Security.Principal;
using EventStore.Common.Utils;
using EventStore.Transport.Http.Codecs;

namespace EventStore.Transport.Http.EntityManagement
{
    public class HttpEntity
    {
        public readonly Uri RequestedUrl;

        public readonly HttpListenerRequest Request;
        internal readonly HttpListenerResponse Response;
        public readonly IPrincipal User;

        public HttpEntity(HttpListenerRequest request, HttpListenerResponse response, IPrincipal user)
        {
            Ensure.NotNull(request, "request");
            Ensure.NotNull(response, "response");

            RequestedUrl = BuildRequestedUrl(request.Url, request.Headers);
            Request = request;
            Response = response;
            User = user;
        }

        public static Uri BuildRequestedUrl(Uri requestUrl, NameValueCollection requestHeaders)
        {
            var uriBuilder = new UriBuilder(requestUrl);

            var forwardedPortHeaderValue = requestHeaders[ProxyHeaders.XForwardedPort];

            if (!string.IsNullOrEmpty(forwardedPortHeaderValue))
            {
                int requestPort;
                if (Int32.TryParse(forwardedPortHeaderValue, out requestPort))
                {
                    uriBuilder.Port = requestPort;
                }
            }

            var forwardedProtoHeaderValue = requestHeaders[ProxyHeaders.XForwardedProto];
            if (!string.IsNullOrEmpty(forwardedProtoHeaderValue))
            {
                uriBuilder.Scheme = forwardedProtoHeaderValue;
            }

            return uriBuilder.Uri;
        }

        private HttpEntity(IPrincipal user)
        {
            RequestedUrl = null;

            Request = null;
            Response = null;
            User = user;
        }

        private HttpEntity(HttpEntity httpEntity, IPrincipal user)
        {
            RequestedUrl = httpEntity.RequestedUrl;

            Request = httpEntity.Request;
            Response = httpEntity.Response;
            User = user;
            
        }

        public HttpEntityManager CreateManager(
            ICodec requestCodec, ICodec responseCodec, string[] allowedMethods, Action<HttpEntity> onRequestSatisfied)
        {
            return new HttpEntityManager(this, allowedMethods, onRequestSatisfied, requestCodec, responseCodec);
        }

        public HttpEntityManager CreateManager()
        {
            return new HttpEntityManager(this, Empty.StringArray, entity => { }, Codec.NoCodec, Codec.NoCodec);
        }

        public HttpEntity SetUser(IPrincipal user)
        {
            return new HttpEntity(this, user);
        }

        public static HttpEntity Test(IPrincipal user)
        {
            return new HttpEntity(user);
        }
    }
}