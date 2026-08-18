using NUnit.Framework;
using PublicApiGenerator;

namespace TheSpectre.ApiEnvelope.EntityFrameworkCore.Tests;

[TestFixture]
public sealed class PublicApiTests
{
    private const string ApprovedFileName = "PublicApi.approved.txt";

    [Test]
    public void PublicApi_HasNotChangedWithoutApproval()
    {
        var actual = typeof(PagedResultQueryableExtensions).Assembly
            .GeneratePublicApi(new ApiGeneratorOptions
            {
                // Per-target-framework build metadata, not compile-time contract.
                // See the core package's approval test for the full reasoning.
                ExcludeAttributes = new[]
                {
                    "System.Reflection.AssemblyMetadataAttribute",
                    "System.Runtime.Versioning.TargetFrameworkAttribute",
                },
            })
            .ReplaceLineEndings("\n")
            .TrimEnd('\n');

        var approvedPath = Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", ApprovedFileName);
        approvedPath = Path.GetFullPath(approvedPath);

        if (Environment.GetEnvironmentVariable("UPDATE_GOLDENS") == "1")
        {
            File.WriteAllText(approvedPath, actual + "\n");

            // Inconclusive, never Pass: a stale UPDATE_GOLDENS=1 in an environment would
            // otherwise turn the entire contract suite green while asserting nothing.
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
