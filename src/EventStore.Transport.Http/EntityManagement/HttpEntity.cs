using System;
using System.Collections.Specialized;
using System.Net;
using System.Security.Principal;
using EventStore.Common.Utils;
using EventStore.Transport.Http.Codecs;
using System.Linq;

namespace EventStore.Transport.Http.EntityManagement
{
    public class HttpEntity
    {
        private readonly bool _logHttpRequests;
        public readonly Uri RequestedUrl;
        public readonly string RequestedUrlBase;

        public readonly HttpListenerRequest Request;
        internal readonly HttpListenerResponse Response;
        public readonly IPrincipal User;

        public HttpEntity(HttpListenerRequest request, HttpListenerResponse response, IPrincipal user, bool logHttpRequests, IPAddress advertiseAsAddress, int advertiseAsPort)
        {
            Ensure.NotNull(request, "request");
            Ensure.NotNull(response, "response");

            _logHttpRequests = logHttpRequests;
            RequestedUrl = BuildRequestedUrl(request.Url, request.Headers, advertiseAsAddress, advertiseAsPort);
            if (request.Url != null && request.Url.Scheme != null && request.Url.Host != null) {
              RequestedUrlBase = request.Url.Scheme + "://" + request.Url.Host + ":" + request.Url.Port;
            } else {
              RequestedUrlBase = "http://127.0.0.1:2113";
            }

            Request = request;
            Response = response;
            User = user;
        }

        public static Uri BuildRequestedUrl(Uri requestUrl, NameValueCollection requestHeaders, IPAddress advertiseAsAddress, int advertiseAsPort)
        {
            var uriBuilder = new UriBuilder(requestUrl);

            if(advertiseAsAddress != null)
            {
                uriBuilder.Host = advertiseAsAddress.ToString();
            }
            if(advertiseAsPort > 0)
            {
                uriBuilder.Port = advertiseAsPort;
            }

            //if a reverse proxy is being used, the headers X-Forwarded-Proto, X-Forwarded-Host, X-Forwarded-Port can be set to correctly build up the URL base for redirects
            //However, this is no longer necessary since URLs will be rewritten as root-relative URLs before sending the response
            var forwardedHostHeaderValue = requestHeaders[ProxyHeaders.XForwardedHost];
            var forwardedProtoHeaderValue = requestHeaders[ProxyHeaders.XForwardedProto];
            var forwardedPortHeaderValue = requestHeaders[ProxyHeaders.XForwardedPort];

            if (!string.IsNullOrEmpty(forwardedHostHeaderValue))
            {
                String parsedHost = null;
                String parsedProto = null;
                int parsedPort = -1;

                //set host to X-Forwarded-Host, and assign the port if available
                var host = forwardedHostHeaderValue.Split(new []{","}, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
                if(!string.IsNullOrEmpty(host))
                {
                    var parts = host.Split(new []{":"}, StringSplitOptions.RemoveEmptyEntries);
                    parsedHost = parts.First();
                    int port;
                    if(parts.Count() > 1 && int.TryParse(parts[1], out port)) {
                        parsedPort = port;
                    }
                }

                if (parsedPort==-1 && !string.IsNullOrEmpty(forwardedPortHeaderValue))
                {
                    //set port to X-Forwarded-Port
                    int requestPort;
                    if (Int32.TryParse(forwardedPortHeaderValue, out requestPort))
                    {
                        parsedPort = requestPort;
                    }
                }

                if (!string.IsNullOrEmpty(forwardedProtoHeaderValue))
                {
                    //set scheme to X-Forwarded-Proto
                    parsedProto = forwardedProtoHeaderValue;
                }

                if (parsedHost != null && parsedPort != -1 && parsedProto != null)
                {
                    uriBuilder.Host = parsedHost;
                    uriBuilder.Port = parsedPort;
                    uriBuilder.Scheme = parsedProto;
                }
            }

            //if ES is mapped under a subfolder on a reverse proxy, X-Forwarded-Prefix MUST be set to rebuild the correct URL
            var forwardedPrefixHeaderValue = requestHeaders[ProxyHeaders.XForwardedPrefix];
            if (!string.IsNullOrEmpty(forwardedPrefixHeaderValue))
            {
                //set path to X-Forwarded-Prefix
                uriBuilder.Path = forwardedPrefixHeaderValue + uriBuilder.Path;
            }

            return uriBuilder.Uri;
        }

        private HttpEntity(IPrincipal user, bool logHttpRequests)
        {
            RequestedUrl = null;
            RequestedUrlBase = null;

            Request = null;
            Response = null;
            User = user;
            _logHttpRequests = logHttpRequests;
        }

        private HttpEntity(HttpEntity httpEntity, IPrincipal user, bool logHttpRequests)
        {
            RequestedUrl = httpEntity.RequestedUrl;
            RequestedUrlBase = httpEntity.RequestedUrlBase;

            Request = httpEntity.Request;
            Response = httpEntity.Response;
            User = user;
            _logHttpRequests = logHttpRequests;
        }

        public HttpEntityManager CreateManager(
            ICodec requestCodec, ICodec responseCodec, string[] allowedMethods, Action<HttpEntity> onRequestSatisfied)
        {
            return new HttpEntityManager(this, allowedMethods, onRequestSatisfied, requestCodec, responseCodec, _logHttpRequests);
        }

        public HttpEntityManager CreateManager()
        {
            return new HttpEntityManager(this, Empty.StringArray, entity => { }, Codec.NoCodec, Codec.NoCodec, _logHttpRequests);
        }

        public HttpEntity SetUser(IPrincipal user)
        {
            return new HttpEntity(this, user, _logHttpRequests);
        }

        public static HttpEntity Test(IPrincipal user)
        {
            return new HttpEntity(user, false);
        }
    }
}
