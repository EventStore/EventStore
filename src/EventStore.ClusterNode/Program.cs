using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Common.Configuration;
using EventStore.Common.Exceptions;
using EventStore.Common.Log;
using EventStore.Common.Utils;
using EventStore.Core.Services.Transport.Http;
using EventStore.Core;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace EventStore.ClusterNode {
	internal static class Program {
		public static async Task<int> Main(string[] args) {
			ThreadPool.SetMaxThreads(1000, 1000);
			ClusterVNodeOptions options;
			var exitCodeSource = new TaskCompletionSource<int>();
			var cts = new CancellationTokenSource();

			Log.Logger = EventStoreLoggerConfiguration.ConsoleLog;
			try {
				try {
					options = ClusterVNodeOptions.FromConfiguration(args, Environment.GetEnvironmentVariables());
				} catch (Exception ex) {
					Log.Fatal($"Error while parsing options: {ex.Message}");
					Log.Information($"Options:{Environment.NewLine}{ClusterVNodeOptions.HelpText}");

					return 1;
				}

				var logsDirectory = string.IsNullOrWhiteSpace(options.Log.Log)
					? Locations.DefaultLogDirectory
					: options.Log.Log;
				EventStoreLoggerConfiguration.Initialize(logsDirectory, options.GetComponentName(),
					options.Log.LogConsoleFormat,
					options.Log.LogFileSize,
					options.Log.LogFileInterval,
					options.Log.LogFileRetentionCount,
					options.Log.DisableLogFile,
					options.Log.LogConfig);

				if (options.Application.Help) {
					await Console.Out.WriteLineAsync(ClusterVNodeOptions.HelpText);
					return 0;
				}

				if (options.Application.Version) {
					await Console.Out.WriteLineAsync(VersionInfo.Text);
					return 0;
				}

				Log.Information("\n{description,-25} {version} ({branch}/{hashtag}, {timestamp})", "ES VERSION:",
					VersionInfo.Version, VersionInfo.Branch, VersionInfo.Hashtag, VersionInfo.Timestamp);
				Log.Information("{description,-25} {osArchitecture} ", "OS ARCHITECTURE:",
					RuntimeInformation.OSArchitecture);
				Log.Information("{description,-25} {osFlavor} ({osVersion})", "OS:", OS.OsFlavor,
					Environment.OSVersion);
				Log.Information("{description,-25} {osRuntimeVersion} ({architecture}-bit)", "RUNTIME:",
					OS.GetRuntimeVersion(),
					Marshal.SizeOf(typeof(IntPtr)) * 8);
				Log.Information("{description,-25} {maxGeneration}", "GC:",
					GC.MaxGeneration == 0
						? "NON-GENERATION (PROBABLY BOEHM)"
						: $"{GC.MaxGeneration + 1} GENERATIONS");
				Log.Information("{description,-25} {logsDirectory}", "LOGS:", logsDirectory);
				Log.Information(options.DumpOptions());

				var deprecationWarnings = options.GetDeprecationWarnings();
				if (deprecationWarnings != null) {
					Log.Warning($"DEPRECATED{Environment.NewLine}{deprecationWarnings}");
				}

				if (options.Application.Insecure) {
					Log.Warning(
						"\n==============================================================================================================\n" +
						"INSECURE MODE IS ON. THIS MODE IS *NOT* RECOMMENDED FOR PRODUCTION USE.\n" +
						"INSECURE MODE WILL DISABLE ALL AUTHENTICATION, AUTHORIZATION AND TRANSPORT SECURITY FOR ALL CLIENTS AND NODES.\n" +
						"==============================================================================================================\n");
				}

				if (!options.Cluster.DiscoverViaDns && options.Cluster.GossipSeed.Length == 0 &&
				    options.Cluster.ClusterSize == 1) {
					Log.Information(
						"DNS discovery is disabled, but no gossip seed endpoints have been specified. Since "
						+ "the cluster size is set to 1, this may be intentional. Gossip seeds can be specified "
						+ "using the `GossipSeed` option.");
				}

				if (options.Application.WhatIf) {
					return 0;
				}

				Application.RegisterExitAction(code => {
					cts.Cancel();
					exitCodeSource.SetResult(code);
				});

				Console.CancelKeyPress += delegate {
					Application.Exit(0, "Cancelled.");
				};
				using (var hostedService = new ClusterVNodeHostedService(options)) {
					using var signal = new ManualResetEventSlim(false);
					_ = Run(hostedService, signal);
					// ReSharper disable MethodSupportsCancellation
					signal.Wait();
					// ReSharper restore MethodSupportsCancellation
				}

				return await exitCodeSource.Task;

				async Task Run(ClusterVNodeHostedService hostedService, ManualResetEventSlim signal) {
					try {
						await new HostBuilder()
							.ConfigureHostConfiguration(builder =>
								builder.AddEnvironmentVariables("DOTNET_").AddCommandLine(args))
							.ConfigureAppConfiguration(builder =>
								builder.AddEnvironmentVariables().AddCommandLine(args))
							.ConfigureServices(services => services.AddSingleton<IHostedService>(hostedService))
							.ConfigureLogging(logging => logging.AddSerilog())
							.ConfigureServices(services => services.Configure<KestrelServerOptions>(
								EventStoreKestrelConfiguration.GetConfiguration()))
							.ConfigureWebHostDefaults(builder => builder
								.UseKestrel(server => {
									server.Limits.Http2.KeepAlivePingDelay =
										TimeSpan.FromMilliseconds(options.Grpc.KeepAliveInterval);
									server.Limits.Http2.KeepAlivePingTimeout =
										TimeSpan.FromMilliseconds(options.Grpc.KeepAliveTimeout);
									server.Listen(options.Interface.ExtIp, options.Interface.HttpPort,
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
															hostedService.Node.InternalClientCertificateValidator(
																certificate,
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
								.Configure(hostedService.Node.Startup.Configure))
							.RunConsoleAsync(options => options.SuppressStatusMessages = true, cts.Token);
					} catch (Exception ex) {
						Log.Fatal("Error occurred during setup: {e}", ex);
						exitCodeSource.TrySetResult(1);
					} finally {
						signal.Set();
					}
				}

			} catch (InvalidConfigurationException ex) {
				Log.Fatal("Invalid Configuration: " + ex.Message);
				return 1;
			}
			catch (Exception ex) {
				Log.Fatal(ex, "Host terminated unexpectedly.");
				return 1;
			} finally {
				Log.CloseAndFlush();
			}
		}
	}
}
