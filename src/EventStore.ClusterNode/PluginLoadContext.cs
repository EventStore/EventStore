using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;

namespace EventStore.ClusterNode {
	internal class PluginLoadContext : AssemblyLoadContext {
		private static readonly string[] Shared = {"Serilog", "YamlDotNet", "EventStore.Plugins"};
		private readonly AssemblyDependencyResolver _resolver;

		public PluginLoadContext(DirectoryInfo directory) {
			_resolver = new AssemblyDependencyResolver(directory.FullName);
			foreach (var library in directory.GetFiles("*.dll")
				.Where(file => !Shared.Contains(Path.GetFileNameWithoutExtension(file.Name)))) {
				LoadFromAssemblyPath(library.FullName);
			}
		}

		protected override Assembly Load(AssemblyName assemblyName) {
			var path = _resolver.ResolveAssemblyToPath(assemblyName);
			return path != null ? LoadFromAssemblyPath(path) : null;
		}
	}
}
