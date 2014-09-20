using System;
using System.Collections.Specialized;
using EventStore.Transport.Http.EntityManagement;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.Transport.Http
{
    [TestFixture]
    class proxy_headers
    {
        [Test]
        public void with_no_headers_uri_is_unchanged()
        {
            var inputUri = new Uri("http://www.example.com:1234/path/?key=value#anchor");
            var requestedUri =
                HttpEntity.BuildRequestedUrl(inputUri,
                                             new NameValueCollection());

            Assert.AreEqual(inputUri, requestedUri);
        }

        [Test]
        public void with_port_forward_header_only_port_is_changed()
        {
            var inputUri = new Uri("http://www.example.com:1234/path/?key=value#anchor");
            var headers = new NameValueCollection { { "X-Forwarded-Port", "4321" } };
            var requestedUri =
                HttpEntity.BuildRequestedUrl(inputUri, headers);

            Assert.AreEqual(new Uri("http://www.example.com:4321/path/?key=value#anchor"), requestedUri);
        }

        [Test]
        public void non_integer_port_forward_header_is_ignored()
        {
            var inputUri = new Uri("http://www.example.com:1234/path/?key=value#anchor");
            var headers = new NameValueCollection { { "X-Forwarded-Port", "abc" } };
            var requestedUri =
                HttpEntity.BuildRequestedUrl(inputUri, headers);

            Assert.AreEqual(inputUri, requestedUri);
        }

        [Test]
        public void with_proto_forward_header_only_scheme_is_changed()
        {
            var inputUri = new Uri("http://www.example.com:1234/path/?key=value#anchor");
            var headers = new NameValueCollection { { "X-Forwarded-Proto", "https" } };
            var requestedUri =
                HttpEntity.BuildRequestedUrl(inputUri, headers);

            Assert.AreEqual(new Uri("https://www.example.com:1234/path/?key=value#anchor"), requestedUri);
        }
    }
}
