using System.Net;

namespace EventStore.Transport.Http
{
    public class HttpResponse
    {
        public string CharacterSet { get; private set; }

        public string ContentEncoding { get; private set; }
        public long ContentLength { get; private set; }
        public string ContentType { get; private set; }

        //public CookieCollection Cookies { get; private set; }
        public WebHeaderCollection Headers { get; private set; }

        //public bool IsFromCache { get; private set; }
        //public bool IsMutuallyAuthenticated { get; private set; }//TODO TR: not implemented in mono
        //public DateTime LastModified { get; private set; }

        public string Method { get; private set; }
        //public Version ProtocolVersion { get; private set; }

        //public Uri ResponseUri { get; private set; }
        //public string Server { get; private set; }

        public int HttpStatusCode { get; private set; }
        public string StatusDescription { get; private set; }

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