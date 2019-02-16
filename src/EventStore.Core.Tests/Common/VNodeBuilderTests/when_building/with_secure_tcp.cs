using NUnit.Framework;
using System;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.IO;
using System.Reflection;
using EventStore.Core.Tests.Services.Transport.Tcp;

namespace EventStore.Core.Tests.Common.VNodeBuilderTests.when_building {
	[TestFixture]
	[Category("LongRunning")]
	public class with_ssl_enabled_and_using_a_security_certificate_from_file : SingleNodeScenario {
		private IPEndPoint _internalSecTcp;
		private IPEndPoint _externalSecTcp;

		public override void Given() {
			var certPath = GetCertificatePath();
			var baseIpAddress = IPAddress.Parse("192.168.1.15");
			_internalSecTcp = new IPEndPoint(baseIpAddress, 1114);
			_externalSecTcp = new IPEndPoint(baseIpAddress, 1115);
			_builder.WithInternalSecureTcpOn(_internalSecTcp)
				.WithExternalSecureTcpOn(_externalSecTcp)
				.EnableSsl()
				.WithSslTargetHost("Host")
				.ValidateSslServer()
				.WithServerCertificateFromFile(certPath, "1111");
		}

		[Test]
		public void should_set_ssl_to_enabled() {
			Assert.IsTrue(_settings.UseSsl);
		}

		[Test]
		public void should_set_certificate() {
			Assert.AreNotEqual("n/a", _settings.Certificate == null ? "n/a" : _settings.Certificate.ToString());
		}

		[Test]
		public void should_set_internal_secure_tcp_endpoint() {
			Assert.AreEqual(_internalSecTcp, _settings.NodeInfo.InternalSecureTcp);
		}

		[Test]
		public void should_set_external_secure_tcp_endpoint() {
			Assert.AreEqual(_externalSecTcp, _settings.NodeInfo.ExternalSecureTcp);
		}

		[Test]
		public void should_set_ssl_target_host() {
			Assert.AreEqual("Host", _settings.SslTargetHost);
		}

		[Test]
		public void should_enable_validating_ssl_server() {
			Assert.IsTrue(_settings.SslValidateServer);
		}

		private string GetCertificatePath() {
			var filePath = Path.Combine(Path.GetTempPath(), string.Format("cert-{0}.p12", Guid.NewGuid()));
			using (var stream = Assembly.GetExecutingAssembly()
				.GetManifestResourceStream("EventStore.Core.Tests.server.p12"))
			using (var fileStream = File.Create(filePath)) {
				stream.Seek(0, SeekOrigin.Begin);
				stream.CopyTo(fileStream);
				return filePath;
			}
		}
	}

	[TestFixture]
	public class with_ssl_enabled_and_using_a_security_certificate : SingleNodeScenario {
		private IPEndPoint _internalSecTcp;
		private IPEndPoint _externalSecTcp;
		private X509Certificate2 _certificate;

		public override void Given() {
			_certificate = ssl_connections.GetCertificate();
			var baseIpAddress = IPAddress.Parse("192.168.1.15");
			_internalSecTcp = new IPEndPoint(baseIpAddress, 1114);
			_externalSecTcp = new IPEndPoint(baseIpAddress, 1115);
			_builder.WithInternalSecureTcpOn(_internalSecTcp)
				.WithExternalSecureTcpOn(_externalSecTcp)
				.EnableSsl()
				.WithSslTargetHost("Host")
				.ValidateSslServer()
				.WithServerCertificate(_certificate);
		}

		[Test]
		public void should_set_ssl_to_enabled() {
			Assert.IsTrue(_settings.UseSsl);
		}

		[Test]
		public void should_set_certificate() {
			Assert.AreNotEqual("n/a", _settings.Certificate == null ? "n/a" : _settings.Certificate.ToString());
		}

		[Test]
		public void should_set_internal_secure_tcp_endpoint() {
			Assert.AreEqual(_internalSecTcp, _settings.NodeInfo.InternalSecureTcp);
		}

		[Test]
		public void should_set_external_secure_tcp_endpoint() {
			Assert.AreEqual(_externalSecTcp, _settings.NodeInfo.ExternalSecureTcp);
		}

		[Test]
		public void should_set_ssl_target_host() {
			Assert.AreEqual("Host", _settings.SslTargetHost);
		}

		[Test]
		public void should_enable_validating_ssl_server() {
			Assert.IsTrue(_settings.SslValidateServer);
		}
	}


	[TestFixture]
	public class with_secure_tcp_endpoints_and_no_certificates {
		private VNodeBuilder _builder;
		private Exception _caughtException;

		[OneTimeSetUp]
		public void SetUp() {
			var baseIpAddress = IPAddress.Parse("192.168.1.15");
			var internalSecTcp = new IPEndPoint(baseIpAddress, 1114);
			var externalSecTcp = new IPEndPoint(baseIpAddress, 1115);
			_builder = TestVNodeBuilder.AsSingleNode()
				.RunInMemory()
				.OnDefaultEndpoints()
				.WithInternalSecureTcpOn(internalSecTcp)
				.WithExternalSecureTcpOn(externalSecTcp);
			try {
				_builder.Build();
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
