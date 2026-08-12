using NUnit.Framework;
using PublicApiGenerator;

namespace TheSpectre.ApiEnvelope.Tests;

[TestFixture]
public sealed class PublicApiTests
{
    private const string ApprovedFileName = "PublicApi.approved.txt";

    [Test]
    public void PublicApi_HasNotChangedWithoutApproval()
    {
        var actual = typeof(ApiResponse<>).Assembly
            .GeneratePublicApi(new ApiGeneratorOptions
            {
                // Assembly-level build metadata is not part of the compile-time contract a
                // consumer binds to, and both of these differ per target framework, so one
                // shared approved file could never pass on both without excluding them:
                //   AssemblyMetadata  — the SDK emits IsAotCompatible/IsTrimmable for
                //                       net10.0 but not net8.0 (a tooling quirk, and NOT
                //                       evidence the analysers were skipped: EnableAotAnalyzer,
                //                       EnableTrimAnalyzer and EnableSingleFileAnalyzer are
                //                       all verified true on net8.0).
                //   TargetFramework   — compiler-emitted, embeds the literal TFM version.
                // Exclude only these two. [Obsolete], [Flags] and the like ARE contract and
                // must keep appearing in the approved file.
                ExcludeAttributes = new[]
                {
                    "System.Reflection.AssemblyMetadataAttribute",
                    "System.Runtime.Versioning.TargetFrameworkAttribute",
                },
            })
            .ReplaceLineEndings("\n")
            .TrimEnd('\n');

        var approvedPath = Path.Combine(
            RepositoryPaths.Root, "tests", "TheSpectre.ApiEnvelope.Tests", ApprovedFileName);

        if (Environment.GetEnvironmentVariable("UPDATE_GOLDENS") == "1")
        {
            File.WriteAllText(approvedPath, actual + "\n");

            // Inconclusive, never Pass — see the same reasoning in GoldenFile.Assert.
            Assert.Inconclusive($"'{ApprovedFileName}' was rewritten.");
            return;
        }

        Assert.That(
            File.Exists(approvedPath),
            Is.True,
            $"'{approvedPath}' is missing. Run with UPDATE_GOLDENS=1 to create it.");

        var approved = File.ReadAllText(approvedPath).ReplaceLineEndings("\n").TrimEnd('\n');

        Assert.That(actual, Is.EqualTo(approved));
    }
}
