using System;
using System.IO;
using System.Net;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using EventStore.Core.LogAbstraction;
using EventStore.Core.Tests.Services.Transport.Tcp;
using NUnit.Framework;

namespace EventStore.Core.Tests.Common.ClusterNodeOptionsTests.when_building {
	[Category("LongRunning")]
	[TestFixture(typeof(LogFormat.V2), typeof(string))]
	[TestFixture(typeof(LogFormat.V3), typeof(uint))]
	public class with_ssl_enabled_and_using_a_security_certificate_from_file<TLogFormat, TStreamId> : SingleNodeScenario<TLogFormat, TStreamId> {
		private readonly IPEndPoint _internalSecTcp = new(IPAddress.Parse("127.0.1.15"), 1114);
		private readonly IPEndPoint _externalSecTcp = new(IPAddress.Parse("127.0.1.15"), 1115);

		protected override ClusterVNodeOptions WithOptions(ClusterVNodeOptions options) =>
			options.WithInternalSecureTcpOn(_internalSecTcp).WithExternalSecureTcpOn(_externalSecTcp) with {
				CertificateFile = new() {
					CertificateFile = GetCertificatePath(),
					CertificatePrivateKeyFile = string.Empty,
					CertificatePassword = "password"
				}
			};


		[Test]
		public void should_set_tls_to_enabled() {
			Assert.IsFalse(_options.Interface.DisableInternalTcpTls);
			Assert.IsFalse(_options.Interface.DisableExternalTcpTls);
		}

		[Test]
		public void should_set_certificate() {
			Assert.AreNotEqual("n/a", _options.Certificate == null ? "n/a" : _options.Certificate.ToString());
		}

		[Test]
		public void should_set_internal_secure_tcp_endpoint() {
			Assert.AreEqual(_internalSecTcp, _node.NodeInfo.InternalSecureTcp);
		}

		[Test]
		public void should_set_external_secure_tcp_endpoint() {
			Assert.AreEqual(_externalSecTcp, _node.NodeInfo.ExternalSecureTcp);
		}

		private string GetCertificatePath() {
			var filePath = Path.Combine(Path.GetTempPath(), string.Format("cert-{0}.p12", Guid.NewGuid()));
			using (var stream = Assembly.GetExecutingAssembly()
				.GetManifestResourceStream("EventStore.Core.Tests.Services.Transport.Tcp.test_certificates.untrusted.untrusted.p12"))
			using (var fileStream = File.Create(filePath)) {
				stream.Seek(0, SeekOrigin.Begin);
				stream.CopyTo(fileStream);
				return filePath;
			}
		}
	}

	[TestFixture(typeof(LogFormat.V2), typeof(string))]
	[TestFixture(typeof(LogFormat.V3), typeof(uint))]
	public class with_ssl_enabled_and_using_a_security_certificate<TLogFormat, TStreamId> : SingleNodeScenario<TLogFormat, TStreamId> {
		private readonly IPEndPoint _internalSecTcp = new(IPAddress.Parse("127.0.1.15"), 1114);
		private readonly IPEndPoint _externalSecTcp = new(IPAddress.Parse("127.0.1.15"), 1115);
		private readonly X509Certificate2 _certificate = ssl_connections.GetServerCertificate();

		protected override ClusterVNodeOptions WithOptions(ClusterVNodeOptions options) => options
				.WithInternalSecureTcpOn(_internalSecTcp)
				.WithExternalSecureTcpOn(_externalSecTcp)
				.Secure(new X509Certificate2Collection(), _certificate);

		[Test]
		public void should_set_tls_to_enabled() {
			Assert.IsFalse(_options.Interface.DisableInternalTcpTls);
			Assert.IsFalse(_options.Interface.DisableExternalTcpTls);
		}

		[Test]
		public void should_set_certificate() {
			Assert.AreNotEqual("n/a", _options.Certificate == null ? "n/a" : _options.Certificate.ToString());
		}

		[Test]
		public void should_set_internal_secure_tcp_endpoint() {
			Assert.AreEqual(_internalSecTcp, _node.NodeInfo.InternalSecureTcp);
		}

		[Test]
		public void should_set_external_secure_tcp_endpoint() {
			Assert.AreEqual(_externalSecTcp, _node.NodeInfo.ExternalSecureTcp);
		}
	}


	[TestFixture(typeof(LogFormat.V2), typeof(string))]
	[TestFixture(typeof(LogFormat.V3), typeof(uint))]
	public class with_secure_tcp_endpoints_and_no_certificates<TLogFormat, TStreamId> {
		private ClusterVNodeOptions _options;
		private Exception _caughtException;

		[OneTimeSetUp]
		public void SetUp() {
			var baseIpAddress = IPAddress.Parse("127.0.1.15");
			var internalSecTcp = new IPEndPoint(baseIpAddress, 1114);
			var externalSecTcp = new IPEndPoint(baseIpAddress, 1115);
			_options = new ClusterVNodeOptions().RunInMemory()
				.WithInternalSecureTcpOn(internalSecTcp)
				.WithExternalSecureTcpOn(externalSecTcp);
			try {
				_ = new ClusterVNode<TStreamId>(_options, LogFormatHelper<TLogFormat, TStreamId>.LogFormatFactory);
			} catch (Exception ex) {
				_caughtException = ex;
			}
		}

		[Test]
		public void should_throw_an_exception() {
			Assert.IsNotNull(_caughtException);
		}
	}
}
