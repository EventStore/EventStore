using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using System.Threading.Tasks;
using EventStore.PluginHosting;
using NUnit.Framework;

namespace EventStore.Core.Tests {
	[TestFixture]
	public class PluginLoaderTests {
		[Test]
		public void root_plugin_folder_does_not_exist() {
			var rootPluginDirectory = new DirectoryInfo(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()));

			Assert.False(rootPluginDirectory.Exists);
			using var sut = new PluginLoader(rootPluginDirectory);
			Assert.IsEmpty(sut.Load<ICloneable>());
		}

		[Test]
		public void root_plugin_folder_does_exist_but_is_empty() {
			var rootPluginDirectory = new DirectoryInfo(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()));

			try {
				rootPluginDirectory.Create();

				Assert.True(rootPluginDirectory.Exists);
				using var sut = new PluginLoader(rootPluginDirectory);
				Assert.IsEmpty(sut.Load<ICloneable>());
			} finally {
				rootPluginDirectory.Delete(true);
			}
		}

		[Test]
		public void sub_plugin_folder_does_exist_but_is_empty() {
			var rootPluginDirectory = new DirectoryInfo(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()));

			try {
				rootPluginDirectory.Create();

				var subPluginDirectory = rootPluginDirectory.CreateSubdirectory(Guid.NewGuid().ToString());

				Assert.True(subPluginDirectory.Exists);
				using var sut = new PluginLoader(rootPluginDirectory);
				Assert.IsEmpty(sut.Load<ICloneable>());
			} finally {
				rootPluginDirectory.Delete(true);
			}
		}

		[Test]
		public async Task plugin_exists_in_root_plugin_folder() {
			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
				Assert.Warn($"Can't remove assemblies from disk once loaded via {nameof(AssemblyLoadContext)} on Windows.");
				return;
			}
			var rootPluginDirectory = new DirectoryInfo(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()));

			try {
				rootPluginDirectory.Create();
				await BuildFakePlugin(rootPluginDirectory);

				using var sut = new PluginLoader(rootPluginDirectory);
				Assert.IsNotEmpty(sut.Load<ICloneable>());
			} finally {
				rootPluginDirectory.Delete(true);
			}
		}

		[Test]
		public async Task plugin_exists_in_sub_plugin_folder() {
			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
				Assert.Warn($"Can't remove assemblies from disk once loaded via {nameof(AssemblyLoadContext)} on Windows.");
				return;
			}

			var rootPluginDirectory = new DirectoryInfo(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()));

			try {
				rootPluginDirectory.Create();
				var subPluginDirectory = rootPluginDirectory.CreateSubdirectory(Guid.NewGuid().ToString());
				subPluginDirectory.Create();
				await BuildFakePlugin(subPluginDirectory);

				using var sut = new PluginLoader(rootPluginDirectory);
				Assert.IsNotEmpty(sut.Load<ICloneable>());
			} finally {
				rootPluginDirectory.Delete(true);
			}
		}

		[Test]
		public async Task plugin_exists_in_sub_sub_plugin_folder_should_not_get_loaded() {
			var rootPluginDirectory = new DirectoryInfo(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()));

			try {
				rootPluginDirectory.Create();
				var subPluginDirectory = rootPluginDirectory.CreateSubdirectory(Guid.NewGuid().ToString());
				subPluginDirectory.Create();
				var subSubPluginDirectory = subPluginDirectory.CreateSubdirectory(Guid.NewGuid().ToString());
				subSubPluginDirectory.Create();
				await BuildFakePlugin(subSubPluginDirectory);

				using var sut = new PluginLoader(rootPluginDirectory);
				Assert.IsEmpty(sut.Load<ICloneable>());
			} finally {
				rootPluginDirectory.Delete(true);
			}
		}

		private static async Task BuildFakePlugin(DirectoryInfo outputFolder) {
			var tcs = new TaskCompletionSource<bool>();
			using var process = new Process {
				StartInfo = new ProcessStartInfo {
					FileName = "dotnet",
					Arguments = $"publish --configuration {BuildConfiguration} --framework=net6.0 --output {outputFolder.FullName}",
					WorkingDirectory = PluginSourceDirectory,
					UseShellExecute = false,
					RedirectStandardError = true,
					RedirectStandardOutput = true
				},
				EnableRaisingEvents = true
			};

			process.Exited += (_, e) => tcs.SetResult(default);
			process.Start();

			var stdout = process.StandardOutput.ReadToEndAsync();
			var stderr = process.StandardError.ReadToEndAsync();

			await Task.WhenAll(tcs.Task, stdout, stderr);

			if (process.ExitCode != 0) {
				throw new Exception(stdout.Result);
			}
		}
		private static string PluginSourceDirectory => Path.Combine(Environment.CurrentDirectory, "FakePlugin");

		private const string BuildConfiguration =
#if DEBUG
			"Debug";
#else
			"Release";
#endif
	}
}
