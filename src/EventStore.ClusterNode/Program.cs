using System;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Common.Utils;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace EventStore.ClusterNode {
	internal static class Program {
		public static async Task<int> Main(string[] args) {
			try {
				var cts = new CancellationTokenSource();
				var hostedService = new ClusterVNodeHostedService(args);

				var exitCodeSource = new TaskCompletionSource<int>();
				Application.RegisterExitAction(code => {
					cts.Cancel();
					exitCodeSource.SetResult(code);
				});
				Console.CancelKeyPress += delegate {
					Application.Exit(0, "Cancelled.");
				};

				await CreateHostBuilder(hostedService, args)
					.RunConsoleAsync(options => options.SuppressStatusMessages = true, cts.Token);
				return await exitCodeSource.Task;
			} catch (Exception ex) {
				Log.Fatal(ex, "Host terminated unexpectedly.");
				return 1;
			} finally {
				Log.CloseAndFlush();
			}
		}

		private static IHostBuilder CreateHostBuilder(ClusterVNodeHostedService hostedService, string[] args) =>
			new HostBuilder()
				.ConfigureHostConfiguration(builder =>
					builder.AddEnvironmentVariables("DOTNET_").AddCommandLine(args ?? Array.Empty<string>()))
				.ConfigureAppConfiguration(builder =>
					builder.AddEnvironmentVariables().AddCommandLine(args ?? Array.Empty<string>()))
				.ConfigureServices(services => services.AddSingleton<IHostedService>(hostedService))
				.ConfigureLogging(logging => logging.AddSerilog())
				.ConfigureWebHostDefaults(builder =>
					builder.UseKestrel(server => {
							server.Listen(hostedService.Options.IntIp, hostedService.Options.IntHttpPort,
								listenOptions => listenOptions.UseHttps(new HttpsConnectionAdapterOptions {
									ServerCertificate = hostedService.Node.Certificate,
									ClientCertificateMode = ClientCertificateMode.RequireCertificate,
									ClientCertificateValidation = (certificate, chain, sslPolicyErrors) => {
										var (isValid, error) = hostedService.Node.InternalClientCertificateValidator(certificate, chain,
											sslPolicyErrors);
										if (!isValid && error != null) {
											Log.Error("Client certificate validation error: {e}", error);
										}
										return isValid;
									}
								}));
							server.Listen(hostedService.Options.ExtIp, hostedService.Options.ExtHttpPort,
								listenOptions => listenOptions.UseHttps(hostedService.Node.Certificate));
						})
						.ConfigureServices(services => hostedService.Node.Startup.ConfigureServices(services))
						.Configure(hostedService.Node.Startup.Configure));
	}
}
