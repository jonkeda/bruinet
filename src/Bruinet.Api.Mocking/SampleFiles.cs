namespace Bruinet.Api.Mocking;

/// <summary>
/// Finds recorded response files for <see cref="ApiStubBuilder.ReturnsSample"/>.
/// </summary>
/// <remarks>
/// The lookup of <c>Exact.Construction.UITests</c>' <c>ContractSampleBody</c>, without its
/// project name: the test output folder first, then every folder above it.
/// </remarks>
public static class SampleFiles
{
    /// <summary>
    /// Reads <paramref name="relativePath"/> from <paramref name="samplesDirectory"/>.
    /// </summary>
    /// <exception cref="FileNotFoundException">The file is in neither the output nor the source tree.</exception>
    public static string Load(string samplesDirectory, string relativePath)
        => File.ReadAllText(Resolve(samplesDirectory, relativePath));

    /// <summary>
    /// The full path of <paramref name="relativePath"/> under <paramref name="samplesDirectory"/>.
    /// </summary>
    /// <exception cref="FileNotFoundException">The file is in neither the output nor the source tree.</exception>
    public static string Resolve(string samplesDirectory, string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(samplesDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);

        var normalized = relativePath.Replace('/', Path.DirectorySeparatorChar);

        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, samplesDirectory, normalized);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new FileNotFoundException(
            $"Sample '{relativePath}' was not found under a '{samplesDirectory}' folder in "
            + $"'{AppContext.BaseDirectory}' or any folder above it.",
            Path.Combine(AppContext.BaseDirectory, samplesDirectory, normalized));
    }
}
