using System;
using System.IO;
using System.Text.Json;
using Xunit;

namespace SlotTheory.Tests;

public class LatchNestContentRegistrationTests
{
    [Fact]
    public void TowersData_ContainsLatchNestDefinition()
    {
        string root = FindRepoRoot();
        string path = Path.Combine(root, "Data", "towers.json");
        using var doc = JsonDocument.Parse(File.ReadAllText(path));

        Assert.True(doc.RootElement.TryGetProperty("latch_nest", out JsonElement latch));
        Assert.Equal("Latch Nest", latch.GetProperty("Name").GetString());
    }

    [Fact]
    public void PlayerFacingAndCodexFiles_ReferenceLatchNest()
    {
        string root = FindRepoRoot();
        var checks = new (string Path, string Snippet)[]
        {
            (Path.Combine(root, "Scripts", "UI", "ProductCopy.cs"), "LatchNestBaseDescription"),
            (Path.Combine(root, "Scripts", "UI", "HowToPlay.cs"), "latch_nest"),
            (Path.Combine(root, "Scripts", "UI", "SlotCodexPanel.cs"), "latch_nest"),
            (Path.Combine(root, "Scripts", "Core", "Unlocks.cs"), "LatchNestTowerId"),
        };

        foreach ((string file, string snippet) in checks)
        {
            string text = File.ReadAllText(file);
            Assert.Contains(snippet, text, StringComparison.Ordinal);
        }
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            bool hasProject = File.Exists(Path.Combine(dir.FullName, "SlotTheory.csproj"));
            bool hasTests = Directory.Exists(Path.Combine(dir.FullName, "SlotTheory.Tests"));
            if (hasProject && hasTests)
                return dir.FullName;
            dir = dir.Parent;
        }

        throw new InvalidOperationException("Could not locate repository root from test runtime path.");
    }
}
