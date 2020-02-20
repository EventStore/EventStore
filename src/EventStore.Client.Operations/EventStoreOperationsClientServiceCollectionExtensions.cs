// ReSharper disable CheckNamespace

using System;
using System.Net.Http;
using EventStore.Client;
using EventStore.Client.Operations;
using Grpc.Core.Interceptors;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace Microsoft.Extensions.DependencyInjection {
	public static class EventStoreOperationsClientServiceCollectionExtensions {
		public static IServiceCollection AddEventStoreOperationsClient(this IServiceCollection services, Uri address,
			Func<HttpMessageHandler> createHttpMessageHandler = null)
			=> services.AddEventStoreOperationsClient(options => {
				options.ConnectivitySettings.Address = address;
				options.CreateHttpMessageHandler = createHttpMessageHandler;
			});

		public static IServiceCollection AddEventStoreOperationsClient(this IServiceCollection services,
			Action<EventStoreClientSettings> configureOptions = null) {
			if (services == null) {
				throw new ArgumentNullException(nameof(services));
			}

			var options = new EventStoreClientSettings();
			configureOptions?.Invoke(options);

			services.TryAddSingleton(provider => {
				options.LoggerFactory ??= provider.GetService<ILoggerFactory>();
				options.Interceptors ??= provider.GetServices<Interceptor>();

				return new EventStoreOperationsClient(options);
			});

			return services;
		}
	}
}
// ReSharper restore CheckNamespace
