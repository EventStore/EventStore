using System.Net;

namespace EventStore.ClientAPI.Transport.Http
{
    internal class HttpResponse
    {
        public readonly string CharacterSet;

        public readonly string ContentEncoding;
        public readonly long ContentLength;
        public readonly string ContentType;

        //public readonly CookieCollection Cookies;
        public readonly WebHeaderCollection Headers;

        //public readonly bool IsFromCache;
        //public readonly bool IsMutuallyAuthenticated;//TODO TR: not implemented in mono
        //public readonly DateTime LastModified;

        public readonly string Method;
        //public readonly Version ProtocolVersion;

        //public readonly Uri ResponseUri;
        //public readonly string Server;

        public readonly int HttpStatusCode;
        public readonly string StatusDescription;

        public string Body { get; internal set; }

        public HttpResponse(HttpWebResponse httpWebResponse)
        {
            CharacterSet = httpWebResponse.CharacterSet;

            ContentEncoding = httpWebResponse.ContentEncoding;
            ContentLength = httpWebResponse.ContentLength;
            ContentType = httpWebResponse.ContentType;

            //Cookies = httpWebResponse.Cookies;
            Headers = httpWebResponse.Headers;

            //IsFromCache = httpWebResponse.IsFromCache;
            //IsMutuallyAuthenticated = httpWebResponse.IsMutuallyAuthenticated;

            //LastModified = httpWebResponse.LastModified;

            Method = httpWebResponse.Method;
            //ProtocolVersion = httpWebResponse.ProtocolVersion;

            //ResponseUri = httpWebResponse.ResponseUri;
            //Server = httpWebResponse.Server;

            HttpStatusCode = (int)httpWebResponse.StatusCode;
            StatusDescription = httpWebResponse.StatusDescription;
        }
    }
}