#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.DependencyModel;

namespace EventStore.Core.Scanning;

public sealed class DependencyContextAssemblyCatalog {
    public static readonly DependencyContextAssemblyCatalog Default = new();

    // ReSharper disable once MemberInitializerValueIgnored
    DependencyContext DependencyContext { get; } = null!;

    public DependencyContextAssemblyCatalog()
        : this(Assembly.GetEntryAssembly(), Assembly.GetCallingAssembly(), Assembly.GetExecutingAssembly()) { }

    DependencyContextAssemblyCatalog(params Assembly?[] assemblies) {
        DependencyContext = assemblies
            .Where(assembly => assembly is not null)
            .Aggregate(
                DependencyContext.Default!,
                (ctx, assembly) => {
                    var loadedContext = DependencyContext.Load(assembly!);
                    return loadedContext is not null ? ctx.Merge(loadedContext) : ctx;
                }
            );
    }

    public IReadOnlyCollection<Assembly> GetAssemblies() {
        var results = new HashSet<Assembly> {
            typeof(DependencyContextAssemblyCatalog).Assembly
        };

        var assemblyNames = DependencyContext.RuntimeLibraries
            //.Where(x => !IsReferencing(x))
            .SelectMany(library => library.GetDefaultAssemblyNames(DependencyContext));

        foreach (var assemblyName in assemblyNames)
            if (TryLoadAssembly(assemblyName, out var assembly))
                results.Add(assembly!);

        return results;
    }

    static bool TryLoadAssembly(AssemblyName assemblyName, out Assembly? assembly) {
        try {
            return (assembly = Assembly.Load(assemblyName)) is not null;
        }
        catch (Exception) {
            return (assembly = null) is not null;
        }
    }

    public static bool IsReferencing(Library library, string assemblyName) => 
        library.Dependencies.Any(dependency => dependency.Name.Equals(assemblyName));
}
