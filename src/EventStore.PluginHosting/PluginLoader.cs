using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;

namespace EventStore.PluginHosting {
	public class PluginLoader : IDisposable {
		private readonly DirectoryInfo _rootPluginDirectory;
		private readonly PluginLoadContext[] _contexts;

		private IEnumerable<DirectoryInfo> PluginDirectories {
			get {
				if (!_rootPluginDirectory.Exists) {
					yield break;
				}

				yield return _rootPluginDirectory;
				foreach (var pluginDirectory in _rootPluginDirectory.EnumerateDirectories()) {
					yield return pluginDirectory;
				}
			}
		}

		public PluginLoader(DirectoryInfo rootPluginDirectory) {
			if (rootPluginDirectory == null) {
				throw new ArgumentNullException(nameof(rootPluginDirectory));
			}
			_rootPluginDirectory = rootPluginDirectory;
			_contexts = PluginDirectories.Select(directory => new PluginLoadContext(directory)).ToArray();
		}

		public IEnumerable<T> Load<T>() where T : class =>
			from loadContext in _contexts
			from pluginType in loadContext.Assemblies.SelectMany(assembly => assembly.GetExportedTypes())
				.Where(typeof(T).IsAssignableFrom)
				.ToArray()
			select (T)Activator.CreateInstance(pluginType);

		public void Dispose() {
			foreach (var context in _contexts) {
				context.Unload();
			}
		}

		private class PluginLoadContext : AssemblyLoadContext {
			private static readonly string[] Shared = {"Serilog", "YamlDotNet", "EventStore.Plugins"};
			private readonly AssemblyDependencyResolver _resolver;

			public PluginLoadContext(DirectoryInfo directory) : base(true) {
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
}
