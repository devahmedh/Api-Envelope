using System.Text;

namespace TheSpectre.ApiEnvelope.Tests;

/// <summary>Reads, compares and optionally rewrites committed golden files.</summary>
internal static class GoldenFile
{
    private static bool ShouldUpdate =>
        Environment.GetEnvironmentVariable("UPDATE_GOLDENS") == "1";

    /// <summary>
    /// Asserts that <paramref name="actualUtf8"/> matches the committed golden file
    /// <paramref name="name"/> as UTF-8 text, ignoring a trailing newline. Set
    /// <c>UPDATE_GOLDENS=1</c> to rewrite it.
    /// </summary>
    internal static void Assert(string name, byte[] actualUtf8)
    {
        var path = Path.Combine(RepositoryPaths.GoldenDirectory, name);

        if (ShouldUpdate)
        {
            Directory.CreateDirectory(RepositoryPaths.GoldenDirectory);
            File.WriteAllBytes(path, actualUtf8);

            // Inconclusive, never Pass. A rewriting run has verified nothing, and a stale
            // UPDATE_GOLDENS=1 in a CI job or shell profile would otherwise turn the entire
            // wire-contract suite green while checking nothing at all.
            NUnit.Framework.Assert.Inconclusive($"Golden file '{name}' was rewritten.");
            return;
        }

        NUnit.Framework.Assert.That(
            File.Exists(path),
            NUnit.Framework.Is.True,
            $"Golden file '{path}' is missing. Run with UPDATE_GOLDENS=1 to create it.");

        // Goldens are committed with a trailing newline for readability; the writer does not
        // emit one, so compare against the trimmed content.
        var expected = File.ReadAllText(path, Encoding.UTF8).TrimEnd('\n', '\r');
        var actual = Encoding.UTF8.GetString(actualUtf8);

        NUnit.Framework.Assert.That(actual, NUnit.Framework.Is.EqualTo(expected));
    }
}
