using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EventStore.Client.Logging {
	public static class LogProvider {
		/// <summary>
		///
		/// </summary>
		public static Action<IServiceCollection> Configure;

		private static readonly Lazy<ILoggerFactory> s_LoggerFactoryFactory = new Lazy<ILoggerFactory>(
			() => {
				var services = new ServiceCollection()
					.AddLogging();
				Configure?.Invoke(services);

				return services.BuildServiceProvider().GetRequiredService<ILoggerFactory>();
			});

		public static ILoggerFactory LoggerFactory => s_LoggerFactoryFactory.Value;

		internal static ILogger CreateLogger(string categoryName) => LoggerFactory.CreateLogger(categoryName);
		internal static ILogger CreateLogger(Type type) => LoggerFactory.CreateLogger(type);
		internal static ILogger<T> CreateLogger<T>() => LoggerFactory.CreateLogger<T>();
	}
}
