using System;
using System.Collections.Specialized;
using EventStore.Transport.Http.EntityManagement;
using NUnit.Framework;
using System.Net;

namespace EventStore.Core.Tests.Services.Transport.Http {
	[TestFixture]
	class build_requested_url_should {
		private Uri inputUri = new Uri("http://www.example.com:1234/path/?key=value#anchor");

		[Test]
		public void with_no_advertise_as_or_headers_uri_is_unchanged() {
			var requestedUri =
				HttpEntity.BuildRequestedUrl(inputUri,
					new NameValueCollection(), null, 0);

			Assert.AreEqual(inputUri, requestedUri);
		}

		[Test]
		public void with_advertise_http_port_set_only_port_is_changed() {
			var requestedUri =
				HttpEntity.BuildRequestedUrl(inputUri,
					new NameValueCollection(), null, 2116);

			Assert.AreEqual(new Uri("http://www.example.com:2116/path/?key=value#anchor"), requestedUri);
		}

		[Test]
		public void with_advertise_ip_set_only_host_is_changed() {
			var requestedUri =
				HttpEntity.BuildRequestedUrl(inputUri,
					new NameValueCollection(), IPAddress.Parse("192.168.1.13"), 0);

			Assert.AreEqual(new Uri("http://192.168.1.13:1234/path/?key=value#anchor"), requestedUri);
		}

		[Test]
		public void with_advertise_ip_and_http_port_set_both_host_and_port_is_changed() {
			var requestedUri =
				HttpEntity.BuildRequestedUrl(inputUri,
					new NameValueCollection(), IPAddress.Parse("192.168.1.13"), 2116);

			Assert.AreEqual(new Uri("http://192.168.1.13:2116/path/?key=value#anchor"), requestedUri);
		}

		[Test]
		public void with_port_forward_header_only_port_is_changed() {
			var headers = new NameValueCollection {{"X-Forwarded-Port", "4321"}};
			var requestedUri =
				HttpEntity.BuildRequestedUrl(inputUri, headers, null, 0);

			Assert.AreEqual(new Uri("http://www.example.com:4321/path/?key=value#anchor"), requestedUri);
		}

		[Test]
		public void non_integer_port_forward_header_is_ignored() {
			var headers = new NameValueCollection {{"X-Forwarded-Port", "abc"}};
			var requestedUri =
				HttpEntity.BuildRequestedUrl(inputUri, headers, null, 0);

			Assert.AreEqual(inputUri, requestedUri);
		}

		[Test]
		public void with_proto_forward_header_only_scheme_is_changed() {
			var headers = new NameValueCollection {{"X-Forwarded-Proto", "https"}};
			var requestedUri =
				HttpEntity.BuildRequestedUrl(inputUri, headers, null, 0);

			Assert.AreEqual(new Uri("https://www.example.com:1234/path/?key=value#anchor"), requestedUri);
		}

		[Test]
		public void with_proto_forward_host_only_host_is_changed() {
			string host = "www.my-host.com";
			var headers = new NameValueCollection {{"X-Forwarded-Host", host}};
			var requestedUri =
				HttpEntity.BuildRequestedUrl(inputUri, headers, null, 0);
			Assert.AreEqual(new Uri("http://www.my-host.com:1234/path/?key=value#anchor"), requestedUri);
		}

		[Test, Category("prefix")]
		public void with_forward_prefix_adds_prefix_when_already_prefix() {
			string prefix = "prefix";
			var headers = new NameValueCollection {{"X-Forwarded-Prefix", prefix}};
			var requestedUri =
				HttpEntity.BuildRequestedUrl(inputUri, headers, null, 0);
			Assert.AreEqual(new Uri("http://www.example.com:1234/prefix/path/?key=value#anchor"), requestedUri);
		}


		[Test, Category("prefix")]
		public void with_forward_prefix_adds_prefix_when_not_prefix() {
			var start = new Uri("http://www.my-host.com:1234/?key=value#anchor");
			string prefix = "prefix";
			var headers = new NameValueCollection {{"X-Forwarded-Prefix", prefix}};
			var requestedUri =
				HttpEntity.BuildRequestedUrl(start, headers, null, 0);
			Assert.AreEqual(new Uri("http://www.my-host.com:1234/prefix/?key=value#anchor"), requestedUri);
		}

		[Test]
		public void with_proto_forward_host_and_advertised_ip_forwarded_host_is_used() {
			string host = "www.my-host.com";
			var headers = new NameValueCollection {{"X-Forwarded-Host", host}};
			var requestedUri =
				HttpEntity.BuildRequestedUrl(inputUri, headers, IPAddress.Parse("192.168.10.13"), 0);
			Assert.AreEqual(new Uri("http://www.my-host.com:1234/path/?key=value#anchor"), requestedUri);
		}

		[Test]
		public void with_proto_forward_host_containing_comma_delimited_list_first_forwarded_host_is_used() {
			string host = "www.my-first-host.com, www.my-second-host.com";
			var headers = new NameValueCollection {{"X-Forwarded-Host", host}};
			var requestedUri =
				HttpEntity.BuildRequestedUrl(inputUri, headers, null, 0);
			Assert.AreEqual(new Uri("http://www.my-first-host.com:1234/path/?key=value#anchor"), requestedUri);
		}

		[Test]
		public void with_proto_forward_host_containing_hosts_with_ports_host_and_port_is_used() {
			string host = "192.168.10.13:2231";
			var headers = new NameValueCollection {{"X-Forwarded-Host", host}};
			var requestedUri =
				HttpEntity.BuildRequestedUrl(inputUri, headers, null, 0);
			Assert.AreEqual(new Uri("http://192.168.10.13:2231/path/?key=value#anchor"), requestedUri);
		}
	}
}
