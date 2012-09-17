//using System;
//using System.Collections.Generic;
//using System.Net;
//using EventStore.Common.Configuration;
//using EventStore.Common.Settings;
//using NUnit.Framework;

//namespace EventStore.Core.Tests.Startup
//{
//    //note MM: tests are a bit deprecated because most of new settings are not tested
//    [TestFixture]
//    public class reading_args_for_vnode_with_id_1
//    {
//        private static readonly Dictionary<string, string> ConfigEntries = new Dictionary<string, string>()
//        {
//            {"cluster-dns","test-cluster.demo.eleks.local"},
//            {"manager-ip", "127.0.0.1"},
//            {"manager-port", "30777"},
//            {"nodes-count", "3"},
//            {"id","1"},
//            {"vnode1-ip", "127.0.0.1"},
//            {"vnode1-tcp-port", "1111"},
//            {"vnode1-http-port", "2111"},
//            {"fake-dns", "true"},
//            {"stats-frequency", "30"},
//            {"prepare-count", "2"},
//            {"commit-count", "2"}
//        };

//        private IConfigurationReader _configReader;

//        private StartUpSettings _expectedSets;

//        [SetUp]
//        public void SetUp()
//        {
//            _configReader = new FakeConfigurationReader(ConfigEntries);

//            var expectedCluster = new ClusterSettings(ConfigEntries["cluster-dns"],
//                new ClusterManagerSettings(
//                    new IPEndPoint(
//                        IPAddress.Parse(ConfigEntries["manager-ip"]),
//                        int.Parse(ConfigEntries["manager-port"]))),
//                new VNodeSettings(new IPEndPoint(
//                                      IPAddress.Parse(ConfigEntries["vnode1-ip"]),
//                                      int.Parse(ConfigEntries["vnode1-tcp-port"])),
//                                  new IPEndPoint(
//                                      IPAddress.Parse(ConfigEntries["vnode1-ip"]),
//                                      int.Parse(ConfigEntries["vnode1-tcp-port"])),
//                                  new IPEndPoint(
//                                      IPAddress.Parse(ConfigEntries["vnode1-ip"]),
//                                      int.Parse(ConfigEntries["vnode1-http-port"]))),
//                int.Parse(ConfigEntries["nodes-count"]));
//            var expectedApp = new ApplicationSettings(bool.Parse(ConfigEntries["fake-dns"]),
//                                                      bool.Parse(ConfigEntries["use-nlog"]), 
//                                                      null, 
//                                                      0, 
//                                                      0, 
//                                                      TimeSpan.Zero);

//            _expectedSets = new StartUpSettings(expectedApp, expectedCluster);

//        }

//        [TearDown]
//        public void TearDown()
//        {
//            _configReader = null;
//            _expectedSets = null;
//        }

//        [Test]
//        public void empty_args_should_be_fucked_off()
//        {
//            var args = "".Split(' ');
//            var sets = StartUpSettings.ReadVNodeSettingsFromArgs(args, _configReader);
//            Assert.That(sets, Is.Null);
//        }

//        [Test]
//        public void when_passing_only_ip_the_rest_is_taken_from_config()
//        {
//            var args = "--ip=8.8.8.8".Split(' ');
//            _expectedSets.Cluster.Self.HttpEndPoint.Address = IPAddress.Parse("8.8.8.8");
//            _expectedSets.Cluster.Self.InternalTcpEndPoint.Address = IPAddress.Parse("8.8.8.8");
//            _expectedSets.Cluster.Self.ExternalTcpEndPoint.Address = IPAddress.Parse("8.8.8.8");

//            var sets = StartUpSettings.ReadVNodeSettingsFromArgs(args, _configReader);

//            Assert.That(sets.AreEqualTo(_expectedSets));
//        }

//        [Test]
//        public void when_passing_only_tcp_port_the_rest_is_taken_from_config()
//        {
//            var args = "--tcp-port=1020".Split(' ');
//            _expectedSets.Cluster.Self.InternalTcpEndPoint.Port = 1020;

//            var sets = StartUpSettings.ReadVNodeSettingsFromArgs(args, _configReader);

//            Assert.That(sets.AreEqualTo(_expectedSets));
//        }

//        [Test]
//        public void when_passing_only_http_port_the_rest_is_taken_from_config()
//        {
//            var args = "--tcp-port=1020".Split(' ');
//            _expectedSets.Cluster.Self.HttpEndPoint.Port = 1020;

//            var sets = StartUpSettings.ReadVNodeSettingsFromArgs(args, _configReader);

//            Assert.That(sets.AreEqualTo(_expectedSets));
//        }

//        [Test]
//        public void when_passing_only_ip_and_manager_port_the_rest_is_taken_from_config()
//        {
//            var args = "--ip=8.8.8.8 --manager-port=1020".Split(' ');
//            _expectedSets.Cluster.ClusterManager.EndPoint.Port = 1020;
//            _expectedSets.Cluster.Self.HttpEndPoint.Address = IPAddress.Parse("8.8.8.8");
//            _expectedSets.Cluster.Self.InternalTcpEndPoint.Address = IPAddress.Parse("8.8.8.8");
//            _expectedSets.Cluster.Self.ExternalTcpEndPoint.Address = IPAddress.Parse("8.8.8.8");

//            var sets = StartUpSettings.ReadVNodeSettingsFromArgs(args, _configReader);

//            Assert.That(sets.AreEqualTo(_expectedSets));
//        }

//        [Test]
//        public void app_settings_are_parsed_correctly_1()
//        {
//            var args = "--fake-dns=false".Split(' ');
//            _expectedSets.App = new ApplicationSettings(false, true, null, 0, 0, TimeSpan.Zero);

//            var sets = StartUpSettings.ReadVNodeSettingsFromArgs(args, _configReader);

//            Assert.That(sets.AreEqualTo(_expectedSets));
//        }

//        [Test]
//        public void app_settings_are_parsed_correctly_2()
//        {
//            var args = "--fake-dns=true".Split(' ');
//            _expectedSets.App = new ApplicationSettings(true, false, null, 0, 0, TimeSpan.Zero);

//            var sets = StartUpSettings.ReadVNodeSettingsFromArgs(args, _configReader);

//            Assert.That(sets.AreEqualTo(_expectedSets));
//        }

//        [Test]
//        public void when_passing_all_args_they_should_apply()
//        {
//            var args = ("--ip=8.8.8.8 --tcp-port=1111 --http-port=2112 " +
//                       "--manager-port=1020 --manager-ip=8.8.8.9 " +
//                       "--cluster-dns=cool --nodes-count=7 " +
//                       "--fake-dns=true").Split(' ');
//            var expectedCluster = new ClusterSettings("cool",
//                new ClusterManagerSettings(
//                    new IPEndPoint(IPAddress.Parse("8.8.8.9"), 1020)),
//                    new VNodeSettings(new IPEndPoint(IPAddress.Parse("8.8.8.8"), 1111),
//                                      new IPEndPoint(IPAddress.Parse("8.8.8.8"), 1111),
//                                      new IPEndPoint(IPAddress.Parse("8.8.8.8"), 2112)),
//                    7);
//            var expectedApp = new ApplicationSettings(true, false, null, 0, 0, TimeSpan.Zero);

//            _expectedSets = new StartUpSettings(expectedApp, expectedCluster);

//            var sets = StartUpSettings.ReadVNodeSettingsFromArgs(args, _configReader);

//            Assert.That(sets.AreEqualTo(_expectedSets));
//        }

//        [Test]
//        public void when_passing_invalid_arguments_should_fail()
//        {
//            var args = "--ip-1".Split(' ');
//            var sets = StartUpSettings.ReadVNodeSettingsFromArgs(args, _configReader);
//            Assert.That(sets == null);
//        }

//        [Test]
//        public void when_passing_argument_without_hyphen_should_ignore_it()
//        {
//            var args = "ip=1".Split(' ');
//            var sets = StartUpSettings.ReadVNodeSettingsFromArgs(args, _configReader);
//            Assert.That(sets.AreEqualTo(_expectedSets));
//        }
//    }
//}
