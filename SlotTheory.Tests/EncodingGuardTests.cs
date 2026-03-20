using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Xunit;

namespace SlotTheory.Tests;

public class EncodingGuardTests
{
    private static readonly HashSet<string> GuardedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cs",
        ".csproj",
        ".sln",
        ".json",
        ".md",
        ".yml",
        ".yaml",
        ".editorconfig",
        ".gitattributes",
    };

    private static readonly HashSet<string> IgnoredDirectories = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git",
        ".godot",
        "bin",
        "obj",
        "release",
    };

    [Fact]
    public void Source_and_config_files_are_utf8_compatible()
    {
        string root = FindRepoRoot();
        var utf8Strict = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
        var offenders = new List<string>();

        foreach (string file in EnumerateGuardedFiles(root))
        {
            byte[] bytes = File.ReadAllBytes(file);
            string rel = Path.GetRelativePath(root, file);

            if (HasBom(bytes, 0xFF, 0xFE) || HasBom(bytes, 0xFE, 0xFF) ||
                HasBom(bytes, 0xFF, 0xFE, 0x00, 0x00) || HasBom(bytes, 0x00, 0x00, 0xFE, 0xFF))
            {
                offenders.Add($"{rel}: UTF-16/32 BOM not allowed");
                continue;
            }

            int offset = HasBom(bytes, 0xEF, 0xBB, 0xBF) ? 3 : 0;
            try
            {
                _ = utf8Strict.GetString(bytes, offset, bytes.Length - offset);
            }
            catch (DecoderFallbackException)
            {
                offenders.Add($"{rel}: invalid UTF-8 byte sequence");
            }
        }

        Assert.True(offenders.Count == 0, "Encoding violations:\n" + string.Join('\n', offenders));
    }

    private static IEnumerable<string> EnumerateGuardedFiles(string root)
    {
        foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            if (ShouldSkip(file))
                continue;

            string name = Path.GetFileName(file);
            string ext = Path.GetExtension(file);
            if (GuardedExtensions.Contains(ext) || GuardedExtensions.Contains(name))
                yield return file;
        }
    }

    private static bool ShouldSkip(string fullPath)
    {
        var parts = fullPath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return parts.Any(part => IgnoredDirectories.Contains(part));
    }

    private static bool HasBom(byte[] bytes, params byte[] bom)
    {
        if (bytes.Length < bom.Length) return false;
        for (int i = 0; i < bom.Length; i++)
        {
            if (bytes[i] != bom[i]) return false;
        }
        return true;
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
