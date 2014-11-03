namespace EventStore.Transport.Http
{
    public static class ProxyHeaders
    {
        public const string XForwardedPort = "X-Forwarded-Port";
        public const string XForwardedProto = "X-Forwarded-Proto";
    }

    public static class ProxyHeaderValues
    {
        public const string XForwardedProtoHttp = "http";
        public const string XForwardedProtoHttps = "https";
    }
}