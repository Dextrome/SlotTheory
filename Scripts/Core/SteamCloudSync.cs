using System.Runtime.CompilerServices;
using Godot;

namespace SlotTheory.Core;

/// <summary>
/// Thin wrapper around SteamRemoteStorage for syncing save files to Steam Cloud.
/// All methods are safe to call when Steam is unavailable - failures are logged and swallowed.
/// All Steamworks.NET references are isolated in NoInlining helpers so the JIT never loads
/// the Steamworks assembly on platforms where it doesn't exist (Android, iOS).
///
/// Usage pattern:
///   PullIfNewer(globalizedPath, cloudName)  ← call BEFORE loading the local file
///   Push(globalizedPath, cloudName)          ← call AFTER a successful local save
/// </summary>
public static class SteamCloudSync
{
    /// <summary>Returns true when Steam is running and Cloud is enabled for this app.</summary>
    public static bool IsAvailable()
    {
        if (OS.GetName() != "Windows") return false;
        return CheckSteamAvailable();
    }

    /// <summary>
    /// Downloads <paramref name="cloudFileName"/> from Steam Cloud and overwrites the local file
    /// only when the cloud copy is strictly newer than the local copy.
    /// </summary>
    public static void PullIfNewer(string localPath, string cloudFileName)
    {
        if (!IsAvailable()) return;
        DoPull(localPath, cloudFileName);
    }

    /// <summary>
    /// Uploads the local file at <paramref name="localPath"/> to Steam Cloud under
    /// <paramref name="cloudFileName"/>. Called immediately after each successful local save.
    /// </summary>
    public static void Push(string localPath, string cloudFileName)
    {
        if (!IsAvailable()) return;
        DoPush(localPath, cloudFileName);
    }

    // ── Steamworks-dependent helpers (NoInlining = JIT only compiles if actually called) ──

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool CheckSteamAvailable()
    {
        try
        {
            return Steamworks.SteamAPI.IsSteamRunning()
                && Steamworks.SteamRemoteStorage.IsCloudEnabledForApp();
        }
        catch { return false; }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void DoPull(string localPath, string cloudFileName)
    {
        try
        {
            if (!Steamworks.SteamRemoteStorage.FileExists(cloudFileName)) return;

            long cloudUnix = Steamworks.SteamRemoteStorage.GetFileTimestamp(cloudFileName);
            long localUnix = 0;
            if (System.IO.File.Exists(localPath))
            {
                var utc = System.IO.File.GetLastWriteTimeUtc(localPath);
                localUnix = new System.DateTimeOffset(utc).ToUnixTimeSeconds();
            }

            if (cloudUnix <= localUnix) return;

            int size = Steamworks.SteamRemoteStorage.GetFileSize(cloudFileName);
            if (size <= 0) return;

            byte[] buf = new byte[size];
            int read = Steamworks.SteamRemoteStorage.FileRead(cloudFileName, buf, size);
            if (read <= 0) return;

            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(localPath)!);
            System.IO.File.WriteAllBytes(localPath, buf[..read]);
            GD.Print($"[CloudSync] Pulled '{cloudFileName}' from Steam Cloud ({read} bytes, cloud ts {cloudUnix} > local {localUnix}).");
        }
        catch (System.Exception ex)
        {
            GD.PrintErr($"[CloudSync] Pull failed for '{cloudFileName}': {ex.Message}");
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void DoPush(string localPath, string cloudFileName)
    {
        try
        {
            if (!System.IO.File.Exists(localPath)) return;

            byte[] data = System.IO.File.ReadAllBytes(localPath);
            bool ok = Steamworks.SteamRemoteStorage.FileWrite(cloudFileName, data, data.Length);
            if (ok)
                GD.Print($"[CloudSync] Pushed '{cloudFileName}' to Steam Cloud ({data.Length} bytes).");
            else
                GD.PrintErr($"[CloudSync] FileWrite returned false for '{cloudFileName}'.");
        }
        catch (System.Exception ex)
        {
            GD.PrintErr($"[CloudSync] Push failed for '{cloudFileName}': {ex.Message}");
        }
    }
}
