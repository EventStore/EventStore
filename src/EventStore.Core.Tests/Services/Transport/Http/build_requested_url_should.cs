using System;
using System.Collections.Specialized;
using EventStore.Transport.Http.EntityManagement;
using NUnit.Framework;
using System.Net;

namespace EventStore.Core.Tests.Services.Transport.Http
{
    [TestFixture]
    class build_requested_url_should
    {
        private Uri inputUri = new Uri("http://www.example.com:1234/path/?key=value#anchor");

        [Test]
        public void with_no_advertise_as_or_headers_uri_is_unchanged()
        {
            var requestedUri =
                HttpEntity.BuildRequestedUrl(inputUri,
                                             new NameValueCollection(), null);

            Assert.AreEqual(inputUri, requestedUri);
        }

        [Test]
        public void with_external_http_endpoint_set_external_http_is_used()
        {
            var requestedUri =
                HttpEntity.BuildRequestedUrl(inputUri,
                                             new NameValueCollection(), new IPEndPoint(IPAddress.Parse("192.168.1.13"), 2117));

            Assert.AreEqual(new Uri("http://192.168.1.13:2117/path/?key=value#anchor"), requestedUri);
        }

        [Test]
        public void with_port_forward_header_only_port_is_changed()
        {
            var headers = new NameValueCollection { { "X-Forwarded-Port", "4321" } };
            var requestedUri =
                HttpEntity.BuildRequestedUrl(inputUri, headers, null);

            Assert.AreEqual(new Uri("http://www.example.com:4321/path/?key=value#anchor"), requestedUri);
        }

        [Test]
        public void non_integer_port_forward_header_is_ignored()
        {
            var headers = new NameValueCollection { { "X-Forwarded-Port", "abc" } };
            var requestedUri =
                HttpEntity.BuildRequestedUrl(inputUri, headers, null);

            Assert.AreEqual(inputUri, requestedUri);
        }

        [Test]
        public void with_proto_forward_header_only_scheme_is_changed()
        {
            var headers = new NameValueCollection { { "X-Forwarded-Proto", "https" } };
            var requestedUri =
                HttpEntity.BuildRequestedUrl(inputUri, headers, null);

            Assert.AreEqual(new Uri("https://www.example.com:1234/path/?key=value#anchor"), requestedUri);
        }
    }
}
