using EventStore.Core.Authentication;
using EventStore.Core.Bus;
using EventStore.Core.Services;
using EventStore.Core.Services.Transport.Http;

namespace EventStore.Core.Tests.Common.VNodeBuilderTests
{
    public class TestAuthenticationProviderFactory : IAuthenticationProviderFactory
    {
        public IAuthenticationProvider BuildAuthenticationProvider(IPublisher mainQueue, ISubscriber mainBus, IPublisher workersQueue, InMemoryBus[] workerBusses)
        {
            return new TestAuthenticationProvider();
        }
        
        public void RegisterHttpControllers(HttpService externalHttpService, HttpService internalHttpService, HttpSendService httpSendService, IPublisher mainQueue, IPublisher networkSendQueue)
        {
        }
    }

    public class TestAuthenticationProvider : IAuthenticationProvider
    {
        public void Authenticate(AuthenticationRequest authenticationRequest)
        {
        }
    }
}