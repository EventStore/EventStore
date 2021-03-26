using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace EventStore.TestClient {
	internal static class Program {
		public static async Task<int> Main(string[] args) {
			try {
				var hostedService = new TestClientHostedService(args);
				await CreateHostBuilder(hostedService, args)
					.RunConsoleAsync(options => options.SuppressStatusMessages = true, hostedService.CancellationToken);
				return await hostedService.Exited;
			} catch (Exception ex) {
				Log.Fatal(ex, "Host terminated unexpectedly.");
				return 1;
			} finally {
				Log.CloseAndFlush();
			}
		}

		private static IHostBuilder CreateHostBuilder(TestClientHostedService hostedService, string[] args) =>
			new HostBuilder()
				.ConfigureHostConfiguration(builder =>
					builder.AddEnvironmentVariables("DOTNET_").AddCommandLine(args ?? Array.Empty<string>()))
				.ConfigureAppConfiguration(builder =>
					builder.AddEnvironmentVariables().AddCommandLine(args ?? Array.Empty<string>()))
				.ConfigureServices(services => services.AddSingleton<IHostedService>(hostedService))
				.ConfigureLogging(logging => logging.AddSerilog());
	}
}
