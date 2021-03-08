using System;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Common.Log;
using EventStore.Common.Utils;
using EventStore.Core.Services.Transport.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Core;

namespace EventStore.ClusterNode {
	internal static class Program {
		public static int Main(string[] args) {
			try {
				Serilog.Log.Logger = EventStoreLoggerConfiguration.ConsoleLog;
				var cts = new CancellationTokenSource();
				var hostedService = new ClusterVNodeHostedService(args);
				if (hostedService.SkipRun)
					return 0;

				var exitCodeSource = new TaskCompletionSource<int>();
				Application.RegisterExitAction(code => {
					cts.Cancel();
					exitCodeSource.SetResult(code);
				});
				Console.CancelKeyPress += delegate {
					Application.Exit(0, "Cancelled.");
				};

				if (hostedService.Options.WhatIf) {
					Log.Information("Exiting with exit code: 0.\nExit reason: WhatIf option specified");
					return (int)ExitCode.Success;
				}

				var signal = new ManualResetEventSlim(false);
				using (hostedService) {
					var t = Run(hostedService, exitCodeSource, args, cts.Token, signal);
					// ReSharper disable once MethodSupportsCancellation
					signal.Wait();
				}

				return exitCodeSource.Task.Result;
			} catch (Exception ex) {
				Log.Fatal(ex, "Host terminated unexpectedly.");
				return 1;
			} finally {
				Log.CloseAndFlush();
			}
		}

		public static async Task Run(ClusterVNodeHostedService hostedService, TaskCompletionSource<int> exitCodeSource, string[] args, CancellationToken token, ManualResetEventSlim signal) {
			try {
				await CreateHostBuilder(hostedService, args)
					.RunConsoleAsync(options => options.SuppressStatusMessages = true, token);
				await exitCodeSource.Task;
			} finally {
				signal.Set();
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
				.ConfigureWebHostDefaults(builder => builder
					.UseKestrel(server => {
						server.Limits.Http2.KeepAlivePingDelay =
							TimeSpan.FromMilliseconds(hostedService.Options.KeepAliveInterval);
						server.Limits.Http2.KeepAlivePingTimeout =
							TimeSpan.FromMilliseconds(hostedService.Options.KeepAliveTimeout);
						server.Listen(hostedService.Options.ExtIp, hostedService.Options.HttpPort,
							listenOptions => {
								if (hostedService.Node.DisableHttps) {
									listenOptions.Use(next =>
										new ClearTextHttpMultiplexingMiddleware(next).OnConnectAsync);
								} else {
									listenOptions.UseHttps(new HttpsConnectionAdapterOptions {
										ServerCertificateSelector = delegate {
											return hostedService.Node.CertificateSelector();
										},
										ClientCertificateMode = ClientCertificateMode.AllowCertificate,
										ClientCertificateValidation = (certificate, chain, sslPolicyErrors) => {
											var (isValid, error) =
												hostedService.Node.InternalClientCertificateValidator(certificate,
													chain,
													sslPolicyErrors);
											if (!isValid && error != null) {
												Log.Error("Client certificate validation error: {e}", error);
											}

											return isValid;
										}
									});
								}
							});
					})
					.ConfigureServices(services => hostedService.Node.Startup.ConfigureServices(services))
					.Configure(hostedService.Node.Startup.Configure));
		}
	}
