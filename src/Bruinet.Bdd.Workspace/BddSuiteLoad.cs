using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text;

namespace Bruinet.Bdd.Workspace;

// One load of a suite's assemblies: each build-output folder copied once, loaded into one collectible
// context, and unloaded together. The copy is what keeps a build from meeting a locked file (AD-018);
// the collectible context is what lets a long-running tool load the next build without growing.
internal sealed class BddSuiteLoad : IDisposable
{
    private readonly Dictionary<string, string> _copies = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<Func<AssemblyLoadContext, AssemblyName, Assembly?>> _resolvers = [];
    private SuiteLoadContext? _context = new();

    public BddCatalogSet Catalogs { get; set; } = null!;

    // The built assembly the fixture was found in, as built: the runner starts from it, not from the copy.
    public string? FixtureAssemblyPath { get; set; }

    // Whether a loaded assembly has a [BddScenarioTheory] method, so its scenarios have addresses to run by.
    public bool HasScenarioTheory { get; private set; }

    // The context, for a test that the load is released.
    internal WeakReference Context => new(_context);

    public IEnumerable<Type> TypesOf(string assemblyPath)
    {
        var context = _context ?? throw new ObjectDisposedException(nameof(BddSuiteLoad));
        var copy = Path.Combine(CopyOf(Path.GetDirectoryName(assemblyPath)!), Path.GetFileName(assemblyPath));
        Assembly assembly;
        try
        {
            context.AddFolder(copy);
            assembly = context.LoadFromAssemblyPath(copy);
        }
        catch (Exception exception) when (exception is BadImageFormatException or FileLoadException)
        {
            throw new BddWorkspaceException($"'{assemblyPath}' cannot be loaded ({exception.Message}).");
        }

        IEnumerable<Type> types;
        try
        {
            types = assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException exception)
        {
            types = exception.Types.OfType<Type>();
        }

        var list = types.ToList();
        HasScenarioTheory |= list.Any(HasScenarioTheoryMethod);
        return list;
    }

    private IReadOnlyList<Bruinet.TestData.ITestDataDefinition>? _definitions;

    // The Bruinet assemblies the suite's own assemblies were built against.
    public IReadOnlyList<AssemblyName> BruinetReferences()
        => (_context?.Assemblies ?? [])
            .SelectMany(assembly => assembly.GetReferencedAssemblies())
            .Where(name => name.Name?.StartsWith("Bruinet.", StringComparison.OrdinalIgnoreCase) == true)
            .ToList();

    // The test-data definitions in these assemblies, made once per load.
    public IReadOnlyList<Bruinet.TestData.ITestDataDefinition> TestDataDefinitions(IReadOnlyList<string> assemblyPaths)
    {
        if (_definitions is not null)
        {
            return _definitions;
        }

        List<Bruinet.TestData.ITestDataDefinition> definitions = [];
        foreach (var path in assemblyPaths)
        {
            try
            {
                definitions.AddRange(TypesOf(path)
                    .Where(type => typeof(Bruinet.TestData.ITestDataDefinition).IsAssignableFrom(type) && type is { IsAbstract: false, IsPublic: true } && type.GetConstructor(Type.EmptyTypes) is not null)
                    .Select(type => BddTestDataDefinition.Placed((Bruinet.TestData.ITestDataDefinition)Activator.CreateInstance(type)!, path, BddShadowCopy.Root)));
            }
            catch (Exception exception) when (exception is BddWorkspaceException or TargetInvocationException or MissingMethodException)
            {
                // An assembly or a definition that cannot load gives no definitions; the config check says why elsewhere.
            }
        }

        return _definitions = definitions;
    }

    // The loaded suite's classes marked [BddStepDefinitions], by name so the attribute's type need not load.
    public IReadOnlyList<Type> StepDefinitionTypes()
        => (_context?.Assemblies ?? [])
            .SelectMany(assembly =>
            {
                try
                {
                    return assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException exception)
                {
                    return exception.Types.OfType<Type>().ToArray();
                }
            })
            .Where(type => type.CustomAttributes.Any(attribute => attribute.AttributeType.Name == "BddStepDefinitionsAttribute"))
            .ToList();

    // A type of the loaded suite by its full name (nested types with '+'); null when none has it.
    public Type? FindType(string fullName)
        => _context?.Assemblies.Select(assembly => assembly.GetType(fullName, throwOnError: false)).FirstOrDefault(type => type is not null);

    // By name, from the metadata: the attribute's type need not load for the check.
    private static bool HasScenarioTheoryMethod(Type type)
    {
        try
        {
            return type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Any(method => method.CustomAttributes.Any(attribute => attribute.AttributeType.FullName is "Bruinet.Uat.BddScenarioTheoryAttribute" or "Bruinet.Uat.UatScenarioTheoryAttribute"));
        }
        catch (Exception exception) when (exception is TypeLoadException or FileNotFoundException or FileLoadException)
        {
            return false;
        }
    }

    public void Dispose()
    {
        foreach (var resolver in _resolvers)
        {
            AssemblyLoadContext.Default.Resolving -= resolver;
        }

        _resolvers.Clear();
        Catalogs = null!;
        _context?.Unload();
        _context = null;
    }

    private string CopyOf(string folder)
    {
        if (_copies.TryGetValue(folder, out var copy))
        {
            return copy;
        }

        copy = BddShadowCopy.Of(folder);
        _copies[folder] = copy;

        // The tool's own Bruinet assemblies may need what only the suite ships (xunit for Bruinet.Core).
        Func<AssemblyLoadContext, AssemblyName, Assembly?> resolver = (context, name) =>
            File.Exists(Path.Combine(copy, name.Name + ".dll")) ? context.LoadFromAssemblyPath(Path.Combine(copy, name.Name + ".dll")) : null;
        AssemblyLoadContext.Default.Resolving += resolver;
        _resolvers.Add(resolver);
        return copy;
    }

    // An assembly the tool ships (Bruinet.Uat, Bruinet.Core, DI) comes from the tool, so the suite's
    // attributes and interfaces are the tool's types; everything else from the suite's own folder.
    private sealed class SuiteLoadContext() : AssemblyLoadContext("BruinetUatSuite", isCollectible: true)
    {
        private readonly List<(string Folder, AssemblyDependencyResolver Resolver)> _folders = [];

        public void AddFolder(string mainAssemblyPath)
        {
            var folder = Path.GetDirectoryName(mainAssemblyPath)!;
            if (!_folders.Any(entry => string.Equals(entry.Folder, folder, StringComparison.OrdinalIgnoreCase)))
            {
                _folders.Add((folder, new AssemblyDependencyResolver(mainAssemblyPath)));
            }
        }

        protected override Assembly? Load(AssemblyName assemblyName)
        {
            if (File.Exists(Path.Combine(AppContext.BaseDirectory, assemblyName.Name + ".dll")))
            {
                return null;
            }

            foreach (var (folder, resolver) in _folders)
            {
                var path = resolver.ResolveAssemblyToPath(assemblyName) ?? Path.Combine(folder, assemblyName.Name + ".dll");
                if (File.Exists(path))
                {
                    return LoadFromAssemblyPath(path);
                }
            }

            return null;
        }

        protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
        {
            foreach (var (_, resolver) in _folders)
            {
                if (resolver.ResolveUnmanagedDllToPath(unmanagedDllName) is { } path)
                {
                    return LoadUnmanagedDllFromPath(path);
                }
            }

            return IntPtr.Zero;
        }
    }
}

// A copy of a build-output folder's assemblies under %TEMP%, one per build: named by the folder and
// the newest write time of what it copies, so an unchanged build is not copied again, even by the
// next process. Older copies are deleted when they are no longer in use.
internal static class BddShadowCopy
{
    private const string DoneMarker = ".copied";

    private static readonly string[] Extensions = [".dll", ".pdb", ".json", ".xml"];

    public static string Root { get; set; } = Path.Combine(Path.GetTempPath(), "Bruinet", "BddWorkspace");

    public static string Of(string folder)
    {
        var files = Directory.EnumerateFiles(folder)
            .Where(file => Extensions.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase))
            .ToList();
        var stamp = files.Count == 0 ? 0 : files.Max(file => File.GetLastWriteTimeUtc(file).Ticks);
        var parent = Path.Combine(Root, Hash(Path.GetFullPath(folder)));
        var copy = Path.Combine(parent, stamp.ToString("x", System.Globalization.CultureInfo.InvariantCulture));
        if (File.Exists(Path.Combine(copy, DoneMarker)))
        {
            return copy;
        }

        // Copied beside, then moved into place: two processes copying the same build both end with
        // a complete copy.
        var staging = copy + "." + Guid.NewGuid().ToString("N");
        Directory.CreateDirectory(staging);
        foreach (var file in files)
        {
            File.Copy(file, Path.Combine(staging, Path.GetFileName(file)));
        }

        File.WriteAllText(Path.Combine(staging, DoneMarker), folder);
        if (!TryMove(staging, copy))
        {
            // Another process moved its copy first, or a virus scanner still has a file open: the
            // staged copy is complete too, and the next copy of this folder cleans it up.
            return File.Exists(Path.Combine(copy, DoneMarker)) && TryDelete(staging) ? copy : staging;
        }

        // Another process's staging folder is left alone while it may still be copying.
        foreach (var older in Directory.EnumerateDirectories(parent)
            .Where(other => !string.Equals(other, copy, StringComparison.OrdinalIgnoreCase))
            .Where(other => !Path.GetFileName(other).Contains('.') || Directory.GetCreationTimeUtc(other) < DateTime.UtcNow.AddMinutes(-10)))
        {
            TryDelete(older);
        }

        return copy;
    }

    // A copy another process (or this one, before its context is collected) still has loaded stays
    // until the next build's copy cleans up again.
    private static bool TryDelete(string folder)
    {
        try
        {
            Directory.Delete(folder, recursive: true);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static bool TryMove(string from, string to)
    {
        try
        {
            Directory.Move(from, to);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static string Hash(string text)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToUpperInvariant())))[..16].ToLowerInvariant();
}
