namespace TheSpectre.ApiEnvelope.Tests;

/// <summary>Locates repository paths from the test output directory.</summary>
internal static class RepositoryPaths
{
    // Directory.Build.props is used as the root marker rather than the solution file:
    // it exists exactly once, at the root, and its name does not vary with the SDK
    // version the way .sln / .slnx does.
    private const string RootMarkerFileName = "Directory.Build.props";

    /// <summary>The repository root directory.</summary>
    internal static string Root { get; } = FindRoot();

    /// <summary>The directory holding the committed golden files.</summary>
    internal static string GoldenDirectory { get; } =
        Path.Combine(Root, "tests", "golden");

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, RootMarkerFileName)))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException(
            $"Could not locate '{RootMarkerFileName}' above '{AppContext.BaseDirectory}'.");
    }
}
