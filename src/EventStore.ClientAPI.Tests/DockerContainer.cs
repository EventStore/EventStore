using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Docker.DotNet;
using Docker.DotNet.Models;

namespace EventStore.ClientAPI.Tests {
	internal class DockerContainer : IAsyncDisposable {
		private const string UnixPipe = "unix:///var/run/docker.sock";
		private const string WindowsPipe = "npipe://./pipe/docker_engine";

		private static readonly Uri s_dockerUri =
			new Uri(RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? WindowsPipe : UnixPipe);

		private static readonly DockerClientConfiguration s_dockerClientConfiguration =
			new DockerClientConfiguration(s_dockerUri);

		private readonly IDictionary<int, int> _ports;
		private readonly string _image;
		private readonly string _tag;
		private readonly Func<CancellationToken, Task<bool>> _healthCheck;
		private readonly IDockerClient _dockerClient;
		private string _containerId;
		private string ImageWithTag => $"{_image}:{_tag}";

		public DockerContainer(
			string image,
			string tag,
			Func<CancellationToken, Task<bool>> healthCheck,
			IDictionary<int, int> ports) {
			_dockerClient = s_dockerClientConfiguration.CreateClient();
			_ports = ports;
			_image = image;
			_tag = tag;
			_healthCheck = healthCheck;
		}

		public string ContainerName { get; set; } = Guid.NewGuid().ToString("n");

		public string[] Env { get; set; } = Array.Empty<string>();

		public string[] Cmd { get; set; } = Array.Empty<string>();

		public async Task TryStart(CancellationToken cancellationToken = default) {
			var images = await _dockerClient.Images.ListImagesAsync(new ImagesListParameters {
				MatchName = ImageWithTag
			}, cancellationToken);

			if (images.Count == 0) {
				// No image found. Pulling latest ..
				var imagesCreateParameters = new ImagesCreateParameters {
					FromImage = _image,
					Tag = _tag,
				};

				await _dockerClient
					.Images
					.CreateImageAsync(imagesCreateParameters, null, IgnoreProgress.Forever, cancellationToken);
			}

			_containerId = await FindContainer(cancellationToken)
			               ?? await CreateContainer(cancellationToken);

			await StartContainer(cancellationToken);
		}

		private async Task<string> FindContainer(CancellationToken cancellationToken) {
			var containers = await _dockerClient.Containers.ListContainersAsync(new ContainersListParameters {
					All = true,
					Filters = new Dictionary<string, IDictionary<string, bool>> {
						["name"] = new Dictionary<string, bool> {
							[ContainerName] = true
						}
					}
				},
				cancellationToken);

			return containers
				.Where(c => c.State != "exited")
				.Select(x => x.ID).FirstOrDefault();
		}

		private async Task<string> CreateContainer(CancellationToken cancellationToken) {
			var portBindings = _ports.ToDictionary(
				pair => $"{pair.Key}/tcp",
				pair => (IList<PortBinding>)new List<PortBinding> {
					new PortBinding {
						HostPort = pair.Value.ToString()
					}
				});

			var createContainerParameters = new CreateContainerParameters {
				Image = ImageWithTag,
				Name = ContainerName,
				Tty = false,
				Env = Env,
				HostConfig = new HostConfig {
					PortBindings = portBindings,
					AutoRemove = true
				},
				Cmd = Cmd
			};

			var container = await _dockerClient.Containers.CreateContainerAsync(
				createContainerParameters,
				cancellationToken);

			return container.ID;
		}

		private async Task StartContainer(CancellationToken cancellationToken) {
			// Starting the container ...
			await _dockerClient.Containers.StartContainerAsync(
				_containerId,
				new ContainerStartParameters(),
				cancellationToken);

			while (!await _healthCheck(cancellationToken)) {
				await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);
			}
		}

		private Task StopContainer(CancellationToken cancellationToken) =>
			_dockerClient.Containers.StopContainerAsync(
				_containerId,
				new ContainerStopParameters {WaitBeforeKillSeconds = 5},
				cancellationToken);

		private class IgnoreProgress : IProgress<JSONMessage> {
			public static readonly IProgress<JSONMessage> Forever = new IgnoreProgress();

			public void Report(JSONMessage value) { }
		}

		public ValueTask DisposeAsync() => new ValueTask(StopContainer(CancellationToken.None));
	}
}
