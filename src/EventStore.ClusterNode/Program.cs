using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Security;
using System.Runtime.InteropServices;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Common.Configuration;
using EventStore.Common.Exceptions;
using EventStore.Common.Log;
using EventStore.Common.Utils;
using EventStore.Core.Services.Transport.Http;
using EventStore.Core.Certificates;
using EventStore.Core;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using System.Runtime;
using EventStore.Common.DevCertificates;
using Serilog.Events;

namespace EventStore.ClusterNode {
	internal static class Program {
		public static async Task<int> Main(string[] args) {
			ThreadPool.SetMaxThreads(1000, 1000);
			var exitCodeSource = new TaskCompletionSource<int>();
			var cts = new CancellationTokenSource();

			Log.Logger = EventStoreLoggerConfiguration.ConsoleLog;
			try {
				var options = ClusterVNodeOptions.FromConfiguration(args, Environment.GetEnvironmentVariables());
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

				if (options.DevMode.RemoveDevCerts) {
					Log.Information("Removing EventStoreDB dev certs.");
					Common.DevCertificates.CertificateManager.Instance.CleanupHttpsCertificates();
					Log.Information("Dev certs removed. Exiting.");
					return 0;
				}

				Log.Information("\n{description,-25} {version} ({branch}/{hashtag}, {timestamp})", "ES VERSION:",
					VersionInfo.Version, VersionInfo.Tag, VersionInfo.Hashtag, VersionInfo.Timestamp);
				Log.Information("{description,-25} {osArchitecture} ", "OS ARCHITECTURE:",
					RuntimeInformation.OSArchitecture);
				Log.Information("{description,-25} {osFlavor} ({osVersion})", "OS:", OS.OsFlavor,
					Environment.OSVersion);
				Log.Information("{description,-25} {osRuntimeVersion} ({architecture}-bit)", "RUNTIME:",
					OS.GetRuntimeVersion(),
					Marshal.SizeOf(typeof(IntPtr)) * 8);
				Log.Information("{description,-25} {maxGeneration} {isServerGC} {latencyMode}", "GC:",
					GC.MaxGeneration == 0
						? "NON-GENERATION (PROBABLY BOEHM)"
						: $"{GC.MaxGeneration + 1} GENERATIONS",
					$"IsServerGC: {GCSettings.IsServerGC}",
					$"Latency Mode: {GCSettings.LatencyMode}");
				Log.Information("{description,-25} {logsDirectory}", "LOGS:", logsDirectory);
				Log.Information(options.DumpOptions());

				var level = options.Application.AllowUnknownOptions
					? LogEventLevel.Information
					: LogEventLevel.Fatal;

				foreach (var key in options.Unknown.Keys) {
					Log.Write(level, "The option {key} is not a known option.", key);
				}

				if (options.Unknown.Keys.Any() && !options.Application.AllowUnknownOptions) {
					Log.Fatal(
						$"Found unknown options. To continue anyway, set {nameof(ClusterVNodeOptions.ApplicationOptions.AllowUnknownOptions)} to true.");
					Log.Information($"Options:{Environment.NewLine}{ClusterVNodeOptions.HelpText}");

					return 1;
				}

				CertificateProvider certificateProvider;
				if (options.DevMode.Dev) {
					Log.Information("Dev mode is enabled.");
					Log.Warning(
						"\n==============================================================================================================\n" +
						"DEV MODE IS ON. THIS MODE IS *NOT* RECOMMENDED FOR PRODUCTION USE.\n" +
						"DEV MODE WILL GENERATE AND TRUST DEV CERTIFICATES FOR RUNNING A SINGLE SECURE NODE ON LOCALHOST.\n" +
						"==============================================================================================================\n");
					var manager = CertificateManager.Instance;
					var result = manager.EnsureDevelopmentCertificate(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddMonths(1));
					if (result is not (EnsureCertificateResult.Succeeded or EnsureCertificateResult.ValidCertificatePresent)) {
						Log.Fatal("Could not ensure dev certificate is available. Reason: {result}", result);
						return 1;
					}

					var userCerts = manager.ListCertificates(StoreName.My, StoreLocation.CurrentUser, true);
					var machineCerts = manager.ListCertificates(StoreName.My, StoreLocation.LocalMachine, true);
					var certs = userCerts.Concat(machineCerts).ToList();

					if (!certs.Any()) {
						Log.Fatal("Could not create dev certificate.");
						return 1;
					}
					if (!manager.IsTrusted(certs[0]) && OperatingSystem.IsWindows()) {
						Log.Information("Dev certificate {cert} is not trusted. Adding it to the trusted store.", certs[0]);
						manager.TrustCertificate(certs[0]);
					} else {
						Log.Warning("Automatically trusting dev certs is only supported on Windows.\n" +
									"Please trust certificate {cert} if it's not trusted already.", certs[0]);
					}

					Log.Information("Running in dev mode using certificate '{cert}'", certs[0]);
					certificateProvider = new DevCertificateProvider(certs[0]);
				} else {
					certificateProvider = new OptionsCertificateProvider();
				}

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
				using (var hostedService = new ClusterVNodeHostedService(options, certificateProvider)) {
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
												listenOptions.UseHttps(CreateServerOptionsSelectionCallback(hostedService), null);
											}
										});
								})
								.ConfigureServices(services => hostedService.Node.Startup.ConfigureServices(services))
								.Configure(hostedService.Node.Startup.Configure))
							.RunConsoleAsync(options => options.SuppressStatusMessages = true, cts.Token);

						exitCodeSource.TrySetResult(0);
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

		private static ServerOptionsSelectionCallback CreateServerOptionsSelectionCallback(ClusterVNodeHostedService hostedService) {
			return ((_, _, _, _) => {
				var serverOptions = new SslServerAuthenticationOptions {
					ServerCertificateContext = SslStreamCertificateContext.Create(
						hostedService.Node.CertificateSelector(),
						hostedService.Node.IntermediateCertificatesSelector(),
						offline: true),
					ClientCertificateRequired = true, // request a client certificate but it's not necessary for the client to supply one
					RemoteCertificateValidationCallback = (_, certificate, chain, sslPolicyErrors) => {
						if(certificate == null) // not necessary to have a client certificate
							return true;

						var (isValid, error) =
							hostedService.Node.InternalClientCertificateValidator(
								certificate,
								chain,
								sslPolicyErrors);
						if (!isValid && error != null) {
							Log.Error("Client certificate validation error: {e}", error);
						}

						return isValid;
					},
					CertificateRevocationCheckMode = X509RevocationMode.NoCheck,
					EnabledSslProtocols = SslProtocols.None, // let the OS choose a secure TLS protocol
					ApplicationProtocols = new List<SslApplicationProtocol> {
						SslApplicationProtocol.Http2,
						SslApplicationProtocol.Http11
					},
					AllowRenegotiation = false
				};

				return ValueTask.FromResult(serverOptions);
			});
		}
	}
}
