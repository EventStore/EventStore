// ReSharper disable CheckNamespace

using System;
using System.Net.Http;
using EventStore.Client;
using Grpc.Core.Interceptors;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace Microsoft.Extensions.DependencyInjection {
	public static class EventStoreClientServiceCollectionExtensions {
		public static IServiceCollection AddEventStoreClient(this IServiceCollection services, Uri address,
			Func<HttpMessageHandler> createHttpMessageHandler = null)
			=> services.AddEventStoreClient(options => {
				options.ConnectivitySettings.Address = address;
				options.CreateHttpMessageHandler = createHttpMessageHandler;
			});

		public static IServiceCollection AddEventStoreClient(this IServiceCollection services,
			Action<EventStoreClientSettings> configureSettings = null) {
			if (services == null) {
				throw new ArgumentNullException(nameof(services));
			}

			var settings = new EventStoreClientSettings();
			configureSettings?.Invoke(settings);

			services.TryAddSingleton(provider => {
				settings.LoggerFactory ??= provider.GetService<ILoggerFactory>();
				settings.Interceptors ??= provider.GetServices<Interceptor>();

				return new EventStoreClient(settings);
			});

			return services;
		}
	}
}
// ReSharper restore CheckNamespace
