using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using EventStore.Client;
using EventStore.Common.Configuration;
using EventStore.Common.Utils;
using EventStore.Core.Certificates;
using EventStore.Core.Services;
using EventStore.Core.Tests.Services.Transport.Tcp;
using Microsoft.Extensions.Configuration;
using NUnit.Framework;

namespace EventStore.Core.Tests.Common.ClusterNodeOptionsTests.when_building {
	[TestFixture(typeof(LogFormat.V2), typeof(string))]
	[TestFixture(typeof(LogFormat.V3), typeof(uint))]
	public class with_cluster_dns_name<TLogFormat, TStreamId> : ClusterMemberScenario<TLogFormat, TStreamId> {
		protected override ClusterVNodeOptions WithOptions(ClusterVNodeOptions options) =>
			options with {
				Cluster = options.Cluster with {
					ClusterDns = "ClusterDns"
				}
			};

		[Test]
		public void should_set_discover_via_dns_to_true() {
			Assert.IsTrue(_options.Cluster.DiscoverViaDns);
		}

		[Test]
		public void should_set_cluster_dns_name() {
			Assert.AreEqual("ClusterDns", _options.Cluster.ClusterDns);
		}
	}
	
	[TestFixture(true)]
	[TestFixture(false)]
	public class different_certificate_common_name {
		private static readonly string _dummyCommonName = $"Gondor{new UUID()}";
		private CertificateProvider _certificateProvider;
		private readonly bool _devMode;
		private ClusterVNodeOptions _options;

		public different_certificate_common_name(bool devMode) {
			_devMode = devMode;
		}

		[OneTimeSetUp]
		public virtual void SetUp() {
			_options = new ClusterVNodeOptions()
				.RunInMemory()
				.Secure(new X509Certificate2Collection(ssl_connections.GetRootCertificate()),
					ssl_connections.GetServerCertificate())
				.WithDevMode(_devMode);
			
			_options.Certificate.CertificateReservedNodeCommonName = _dummyCommonName;
			_certificateProvider = GetCertificateProvider(_options);
		}

		private static CertificateProvider GetCertificateProvider(ClusterVNodeOptions options) {
			return options.DevMode.Dev ? new DevCertificateProvider(new X509Certificate2(Array.Empty<byte>())) :  new OptionsCertificateProvider();
		}
	
		[Test]
		public void load_certificate_should_fail() {
			LoadCertificateResult result = _certificateProvider.LoadCertificates(_options);
			if (_devMode) {
				Assert.AreEqual(LoadCertificateResult.Skipped, result);
				Assert.Null(_certificateProvider.CertificateCN);
			} else {
				Assert.AreEqual(LoadCertificateResult.VerificationFailed, result);
			}
		}
	}
	
	[TestFixture(true)]
	[TestFixture(false)]
	public class certificate_common_name_absent_in_config {
		private CertificateProvider _certificateProvider;
		private readonly bool _devMode;
		private X509Certificate2 _serverCertificate;
		private ClusterVNodeOptions _options;
		
		public certificate_common_name_absent_in_config(bool devMode) {
			_devMode = devMode;
		}

		[OneTimeSetUp]
		public virtual void SetUp() {
			_serverCertificate = ssl_connections.GetServerCertificate();
			
			_options = new ClusterVNodeOptions()
				.RunInMemory()
				.Secure(new X509Certificate2Collection(ssl_connections.GetRootCertificate()), _serverCertificate)
				.WithDevMode(_devMode);
			
			_options.Certificate.CertificateReservedNodeCommonName = null;
			_certificateProvider = GetCertificateProvider(_options);
		}

		private static CertificateProvider GetCertificateProvider(ClusterVNodeOptions options) {
			return options.DevMode.Dev ? new DevCertificateProvider(new X509Certificate2(Array.Empty<byte>())) :  new OptionsCertificateProvider();
		}
	
		[Test]
		public void certificate_provider_should_load_common_name_from_certificate() {
			var result = _certificateProvider.LoadCertificates(_options);
			if (_devMode) {
				Assert.AreEqual(LoadCertificateResult.Skipped, result);
			} else {
				Assert.AreEqual(LoadCertificateResult.Success, result);	
				Assert.AreEqual(_serverCertificate.GetCommonName(), _certificateProvider.CertificateCN);
			}
		}
	}
	
	[TestFixture(true)]
	[TestFixture(false)]
	public class load_certificate_insecure_mode {
		private static readonly string _dummyCommonName = $"Gondor{new UUID()}";
		private CertificateProvider _certificateProvider;
		private ClusterVNodeOptions _options;
		private readonly bool _devMode;
		public load_certificate_insecure_mode(bool devMode) {
			_devMode = devMode;
		}

		[OneTimeSetUp]
		public virtual void SetUp() {
			_options = new ClusterVNodeOptions().RunInMemory().Insecure().WithDevMode(_devMode);
			_options.Certificate.CertificateReservedNodeCommonName = _dummyCommonName;
			_certificateProvider = GetCertificateProvider(_options);
		}

		private static CertificateProvider GetCertificateProvider(ClusterVNodeOptions options) {
			return options.DevMode.Dev ? new DevCertificateProvider(new X509Certificate2(Array.Empty<byte>())) :  new OptionsCertificateProvider();
		}
	
		[Test]
		public void load_certificate_should_skip() {
			Assert.AreEqual(LoadCertificateResult.Skipped, _certificateProvider.LoadCertificates(_options));
			Assert.Null(_certificateProvider.CertificateCN);
		}
	}

	[TestFixture(typeof(LogFormat.V2), typeof(string))]
	[TestFixture(typeof(LogFormat.V3), typeof(uint))]
	public class with_dns_discovery_disabled_and_no_gossip_seeds<TLogFormat, TStreamId> {
		private Exception _caughtException;
		protected ClusterVNodeOptions _options;

		[OneTimeSetUp]
		public void TestFixtureSetUp() {
			_options = new ClusterVNodeOptions {
				Cluster = new() {
					DiscoverViaDns = false
				}
			}.RunInMemory();
			try {
				_ = new ClusterVNode<TStreamId>(_options, LogFormatHelper<TLogFormat, TStreamId>.LogFormatFactory,
					certificateProvider: new OptionsCertificateProvider());
			} catch (Exception e) {
				_caughtException = e;
			}
		}

		[Test]
		public void should_throw_an_exception() {
			Assert.IsNotNull(_caughtException);
		}
	}

	[TestFixture(typeof(LogFormat.V2), typeof(string))]
	[TestFixture(typeof(LogFormat.V3), typeof(uint))]
	public class with_dns_discovery_disabled_and_gossip_seeds_defined<TLogFormat, TStreamId> : ClusterMemberScenario<TLogFormat, TStreamId> {
		private EndPoint[] _gossipSeeds = {
			new DnsEndPoint("127.0.1.10", 1111),
			new DnsEndPoint("127.0.1.10", 1112),
		};

		protected override ClusterVNodeOptions WithOptions(ClusterVNodeOptions options) =>
			options.WithGossipSeeds(_gossipSeeds);

		[Test]
		public void should_set_discover_via_dns_to_false() {
			Assert.IsFalse(_options.Cluster.DiscoverViaDns);
		}

		[Test]
		public void should_set_the_gossip_seeds() {
			CollectionAssert.AreEqual(_gossipSeeds, _options.Cluster.GossipSeed);
		}
	}

	[TestFixture(typeof(LogFormat.V2), typeof(string))]
	[TestFixture(typeof(LogFormat.V3), typeof(uint))]
	public class with_custom_gossip_seeds<TLogFormat, TStreamId> : ClusterMemberScenario<TLogFormat, TStreamId> {
		private readonly DnsEndPoint[] _gossipSeeds = {new("127.0.1.15", 2112), new("127.0.1.15", 3112)};

		protected override ClusterVNodeOptions WithOptions(ClusterVNodeOptions options) =>
			options.WithGossipSeeds(_gossipSeeds);

		[Test]
		public void should_turn_off_discovery_by_dns() {
			Assert.IsFalse(_options.Cluster.DiscoverViaDns);
		}

		[Test]
		public void should_set_the_gossip_seeds() {
			CollectionAssert.AreEqual(_gossipSeeds, _options.Cluster.GossipSeed);
		}
	}

	[TestFixture(typeof(LogFormat.V2), typeof(string))]
	[TestFixture(typeof(LogFormat.V3), typeof(uint))]
	public class with_custom_external_ip_address_as_advertise_info<TLogFormat, TStreamId> : ClusterMemberScenario<TLogFormat, TStreamId> {
		protected override ClusterVNodeOptions WithOptions(ClusterVNodeOptions options) =>
			options
				.Insecure()
				.WithExternalTcpOn(new IPEndPoint(IPAddress.Loopback, 11130))
				.WithInternalTcpOn(new IPEndPoint(IPAddress.Loopback, 11120))
				.AdvertiseExternalHostAs(new DnsEndPoint("196.168.1.1", 11131))
				.AdvertiseHttpHostAs(new DnsEndPoint("196.168.1.1", 21130));

		[Test]
		public void should_set_the_custom_advertise_info_for_external() {
			Assert.AreEqual(new DnsEndPoint("196.168.1.1", 11131),
				_node.GossipAdvertiseInfo.ExternalTcp);
			Assert.AreEqual(new DnsEndPoint("196.168.1.1", 21130),
				_node.GossipAdvertiseInfo.HttpEndPoint);
		}

		[Test]
		public void should_set_the_loopback_address_as_advertise_info_for_internal() {
			Assert.AreEqual(new DnsEndPoint(IPAddress.Loopback.ToString(), 11120), _node.GossipAdvertiseInfo.InternalTcp);
		}
	}

	[TestFixture(typeof(LogFormat.V2), typeof(string))]
	[TestFixture(typeof(LogFormat.V3), typeof(uint))]
	public class with_0_0_0_0_as_external_ip_address_and_custom_advertise_info<TLogFormat, TStreamId> : ClusterMemberScenario<TLogFormat, TStreamId> {
		protected override ClusterVNodeOptions WithOptions(ClusterVNodeOptions options) =>
			options
				.Insecure()
				.WithInternalTcpOn(new IPEndPoint(IPAddress.Any, 11120))
				.WithExternalTcpOn(new IPEndPoint(IPAddress.Any, 11130))
				.AdvertiseExternalHostAs(new DnsEndPoint("10.0.0.1", 11131));

		[Test]
		public void should_set_the_custom_advertise_info_for_external() {
			Assert.AreEqual(new DnsEndPoint("10.0.0.1", 11131),
				_node.GossipAdvertiseInfo.ExternalTcp);
			Assert.AreEqual(new DnsEndPoint("10.0.0.1", 2113),
				_node.GossipAdvertiseInfo.HttpEndPoint);
		}

		[Test]
		public void should_set_the_non_loopback_address_as_advertise_info_for_internal() {
			Assert.AreEqual(new DnsEndPoint(IPFinder.GetNonLoopbackAddress().ToString(), 11120),
				_node.GossipAdvertiseInfo.InternalTcp);
		}
	}

	[TestFixture(typeof(LogFormat.V2), typeof(string))]
	[TestFixture(typeof(LogFormat.V3), typeof(uint))]
	public class with_0_0_0_0_as_external_ip_address_with_no_explicit_advertise_info_set<TLogFormat, TStreamId> : ClusterMemberScenario<TLogFormat, TStreamId> {
		protected override ClusterVNodeOptions WithOptions(ClusterVNodeOptions options) =>
			options
				.Insecure()
				.WithHttpOn(new IPEndPoint(IPAddress.Any, 21130))
				.WithExternalTcpOn(new IPEndPoint(IPAddress.Any, 11130))
				.WithInternalTcpOn(new IPEndPoint(IPAddress.Loopback, 11120));

		[Test]
		public void should_use_the_non_default_loopback_ip_as_advertise_info_for_external() {
			Assert.AreEqual(new DnsEndPoint(IPFinder.GetNonLoopbackAddress().ToString(), 11130),
				_node.GossipAdvertiseInfo.ExternalTcp);
			Assert.AreEqual(new DnsEndPoint(IPFinder.GetNonLoopbackAddress().ToString(), 21130),
				_node.GossipAdvertiseInfo.HttpEndPoint);
		}

		[Test]
		public void should_use_loopback_ip_as_advertise_info_for_internal() {
			Assert.AreEqual(new DnsEndPoint(IPAddress.Loopback.ToString(), 11120), _node.GossipAdvertiseInfo.InternalTcp);
		}
	}

	[TestFixture(typeof(LogFormat.V2), typeof(string))]
	[TestFixture(typeof(LogFormat.V3), typeof(uint))]
	public class
		with_0_0_0_0_for_internal_and_external_ips_with_advertise_info_set_for_external<TLogFormat, TStreamId> : ClusterMemberScenario<TLogFormat, TStreamId> {
		protected override ClusterVNodeOptions WithOptions(ClusterVNodeOptions options) =>
			options
				.Insecure()
				.WithHttpOn(new IPEndPoint(IPAddress.Loopback, 21130))
				.WithExternalTcpOn(new IPEndPoint(IPAddress.Loopback, 11130))
				.WithInternalTcpOn(new IPEndPoint(IPAddress.Any, 11120))
				.AdvertiseExternalHostAs(new DnsEndPoint("10.0.0.1", 11131))
				.AdvertiseHttpHostAs(new DnsEndPoint("10.0.0.1", 21131));

		[Test]
		public void should_set_the_custom_advertise_info_for_external() {
			Assert.AreEqual(new DnsEndPoint("10.0.0.1", 11131),
				_node.GossipAdvertiseInfo.ExternalTcp);
			Assert.AreEqual(new DnsEndPoint("10.0.0.1", 21131),
				_node.GossipAdvertiseInfo.HttpEndPoint);
		}

		[Test]
		public void should_use_the_non_default_loopback_ip_as_advertise_info_for_internal() {
			Assert.AreEqual(new DnsEndPoint(IPFinder.GetNonLoopbackAddress().ToString(), 11120),
				_node.GossipAdvertiseInfo.InternalTcp);
		}
	}
	
	[TestFixture(typeof(LogFormat.V2), typeof(string))]
	[TestFixture(typeof(LogFormat.V3), typeof(uint))]
	public class with_cluster_custom_password_for_admin_and_ops_user<TLogFormat, TStreamId> : ClusterMemberScenario<TLogFormat, TStreamId> {
		private const string _adminPassword = "Admin";
		private const string _opsPassword = "Ops";

		protected override ClusterVNodeOptions WithOptions(ClusterVNodeOptions options) =>
			options with {
				DefaultUser = new ClusterVNodeOptions.DefaultUserOptions { DefaultAdminPassword = _adminPassword, DefaultOpsPassword = _opsPassword }
			};

		[Test]
		public void should_set_the_custom_admin_and_ops_user_password() {
			Assert.AreEqual(_adminPassword,_options.DefaultUser.DefaultAdminPassword);
			Assert.AreEqual(_opsPassword, _options.DefaultUser.DefaultOpsPassword);
		}
	}
	
	[TestFixture(typeof(LogFormat.V2), typeof(string))]
	[TestFixture(typeof(LogFormat.V3), typeof(uint))]
	public class with_cluster_custom_settings_check_for_environment_only_options<TLogFormat, TStreamId> : SingleNodeScenario<TLogFormat,
			TStreamId> {
		protected override ClusterVNodeOptions WithOptions(ClusterVNodeOptions options) =>
			options with {
			};
		
		private IConfigurationRoot _configurationRoot;
		
		[Test]
		public void should_return_error_when_default_password_options_pass_through_command_line() {

			var args = new string[] {
				"--DefaultAdminPassword=Admin2023#",
				"--DefaultOpsPassword=Ops2023#"
			};
			_configurationRoot = new ConfigurationBuilder()
				.Add(new DefaultSource(new Dictionary<string, object> {
					[nameof(ClusterVNodeOptions.DefaultUser.DefaultAdminPassword)] = SystemUsers.DefaultAdminPassword,
					[nameof(ClusterVNodeOptions.DefaultUser.DefaultOpsPassword)] = SystemUsers.DefaultOpsPassword,
					[nameof(ClusterVNodeOptions.Certificate.CertificateReservedNodeCommonName)] = "es"
				}))
				.Add(new CommandLineSource(args))
				.Build();
			
			var clusterVNodeOptions = ClusterVNodeOptions.FromConfiguration(_configurationRoot);
			
			Assert.NotNull(clusterVNodeOptions.CheckForEnvironmentOnlyOptions());
		}
		
		[Test]
		public void should_return_null_when_default_password_options_pass_through_environment_variables() {

			var args = Array.Empty<string>();
			IDictionary environmentVariables = new Dictionary<string, string>();
			environmentVariables.Add("EVENTSTORE_DEFAULT_ADMIN_PASSWORD", "Admin#");
			environmentVariables.Add("EVENTSTORE_DEFAULT_OPS_PASSWORD", "Ops#");
			
			_configurationRoot = new ConfigurationBuilder()
				.Add(new DefaultSource(new Dictionary<string, object> {
					[nameof(ClusterVNodeOptions.DefaultUser.DefaultAdminPassword)] = SystemUsers.DefaultAdminPassword,
					[nameof(ClusterVNodeOptions.DefaultUser.DefaultOpsPassword)] = SystemUsers.DefaultOpsPassword,
					[nameof(ClusterVNodeOptions.Certificate.CertificateReservedNodeCommonName)] = "es"
				}))
				.Add(new CommandLineSource(args))
				.Add(new EnvironmentVariablesSource(environmentVariables))
				.Build();
			
			var clusterVNodeOptions = ClusterVNodeOptions.FromConfiguration(_configurationRoot);
			
			Assert.Null(clusterVNodeOptions.CheckForEnvironmentOnlyOptions());
		}
	}
}
