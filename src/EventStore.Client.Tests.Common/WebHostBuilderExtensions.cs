using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace EventStore.Client
{
	internal static class WebHostBuilderExtensions {
		public static IWebHostBuilder UseStartup(this IWebHostBuilder builder, IStartup startup)
			=> builder
				.ConfigureServices(services => services.AddSingleton(startup));
	}
}
