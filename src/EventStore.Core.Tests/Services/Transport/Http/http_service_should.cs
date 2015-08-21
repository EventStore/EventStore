using System;
using System.Net;
using System.Threading;
using EventStore.Core.Messages;
using EventStore.Core.Tests.Helpers;
using EventStore.Transport.Http;
using NUnit.Framework;
using EventStore.Common.Utils;
using System.Linq;
using HttpStatusCode = System.Net.HttpStatusCode;

namespace EventStore.Core.Tests.Services.Transport.Http
{
    [TestFixture,Category("LongRunning")]
    public class http_service_should
    {
        private readonly IPEndPoint _serverEndPoint;
        private readonly PortableServer _portableServer;

        public http_service_should()
        {
            var port = PortsHelper.GetAvailablePort(IPAddress.Loopback);
            _serverEndPoint = new IPEndPoint(IPAddress.Loopback, port);
            _portableServer = new PortableServer(_serverEndPoint);
        }

        [SetUp]
        public void SetUp()
        {
            _portableServer.SetUp();
        }

        [TearDown]
        public void TearDown()
        {
            _portableServer.TearDown();
        }

        [TestFixtureTearDown]
        public void TestFixtureTearDown()
        {
            PortsHelper.ReturnPort(_serverEndPoint.Port);            
        }

        [Test]
        [Category("Network")]
        public void start_after_system_message_system_init_published()
        {
            Assert.IsFalse(_portableServer.IsListening);
            _portableServer.Publish(new SystemMessage.SystemInit());
            Assert.IsTrue(_portableServer.IsListening);
        }

        [Test]
        [Category("Network")]
        public void ignore_shutdown_message_that_does_not_say_shut_down()
        {
            _portableServer.Publish(new SystemMessage.SystemInit());
            Assert.IsTrue(_portableServer.IsListening);

            _portableServer.Publish(new SystemMessage.BecomeShuttingDown(Guid.NewGuid(), exitProcess: false, shutdownHttp: false));
            Assert.IsTrue(_portableServer.IsListening);
        }

        [Test]
        [Category("Network")]
        public void react_to_shutdown_message_that_cause_process_exit()
        {
            _portableServer.Publish(new SystemMessage.SystemInit());
            Assert.IsTrue(_portableServer.IsListening);

            _portableServer.Publish(new SystemMessage.BecomeShuttingDown(Guid.NewGuid(), exitProcess: true, shutdownHttp:true));
            Assert.IsFalse(_portableServer.IsListening);
        }

        [Test]
        [Category("Network")]
        public void reply_with_404_to_every_request_when_there_are_no_registered_controllers()
        {
            var requests = new[] {"/ping", "/streams", "/gossip", "/stuff", "/notfound", "/magic/url.exe"};
            var successes = new bool[requests.Length];
            var errors = new string[requests.Length];
            var signals = new AutoResetEvent[requests.Length];
            for (var i = 0; i < signals.Length; i++)
                signals[i] = new AutoResetEvent(false);

            _portableServer.Publish(new SystemMessage.SystemInit());

            for (var i = 0; i < requests.Length; i++)
            {
                var i1 = i;
                _portableServer.BuiltInClient.Get(_serverEndPoint.ToHttpUrl(requests[i]),
                            TimeSpan.FromMilliseconds(10000),
                            response =>
                                {
                                    successes[i1] = response.HttpStatusCode == (int) HttpStatusCode.NotFound;
                                    signals[i1].Set();
                                },
                            exception =>
                                {
                                    successes[i1] = false;
                                    errors[i1] = exception.Message;
                                    signals[i1].Set();
                                });
            }

            foreach (var signal in signals)
                signal.WaitOne();

            Assert.IsTrue(successes.All(x => x), string.Join(";", errors.Where(e => !string.IsNullOrEmpty(e))));
        }

        [Test]
        [Category("Network")]
        public void handle_invalid_characters_in_url()
        {
            var url = _serverEndPoint.ToHttpUrl("/ping^\"");
            Func<HttpResponse, bool> verifier = response => string.IsNullOrEmpty(response.Body) &&
                                                            response.HttpStatusCode == (int) HttpStatusCode.NotFound;

            var result = _portableServer.StartServiceAndSendRequest(HttpBootstrap.RegisterPing, url, verifier);
            Assert.IsTrue(result.Item1, result.Item2);
        }
    }
}
