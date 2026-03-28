using System;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace SlotTheory.Tests;

public class PlayerCopyDriftTests
{
    private static readonly string[] PlayerFacingFiles =
    {
        "Scripts/UI/ProductCopy.cs",
        "Scripts/UI/DraftPanel.cs",
        "Scripts/UI/HowToPlay.cs",
        "Scripts/UI/SlotCodexPanel.cs",
        "Scripts/UI/UnlockRevealScreen.cs",
        "Scripts/Core/GameController.cs",
        "Scripts/Core/TutorialManager.cs",
    };

    private static readonly string[] BannedSnippets =
    {
        "Burst Core",
        "You start with 10.",
        "for 35% damage each",
        "for 60% damage",
        "60% damage decay per bounce",
        "Drags enemies backward so they spend longer inside your defenses.",
        "3.5Ã",
    };

    [Fact]
    public void Player_facing_copy_avoids_known_drift_phrases()
    {
        string root = FindRepoRoot();
        var offenders = new List<string>();

        foreach (string relPath in PlayerFacingFiles)
        {
            string fullPath = Path.Combine(root, relPath);
            string contents = File.ReadAllText(fullPath);

            foreach (string snippet in BannedSnippets)
            {
                if (contents.Contains(snippet, StringComparison.Ordinal))
                    offenders.Add($"{relPath}: contains banned snippet '{snippet}'");
            }
        }

        Assert.True(offenders.Count == 0, "Copy drift findings:\n" + string.Join('\n', offenders));
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
