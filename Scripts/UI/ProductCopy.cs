namespace SlotTheory.UI;

/// <summary>
/// Shared player-facing product copy for demo/full-game messaging.
/// Keep this intentionally small so teaser language stays consistent.
/// </summary>
public static class ProductCopy
{
    public const string WishlistCta = "Wishlist on Steam";

    public const string FullGameSectorLabel = "Full Game Sector";
    public const string FullGameButtonLabel = "FULL GAME";
    public const string FullGameMapTeaseLine = "Part of the full game circuit.";
    public const string FullGameTooltip = "Unlocked in the full game build.";

    public const string DemoCompleteTitle = "Demo complete.";
    public const string DemoCompleteBody =
        "You cleared the demo circuit.\nThis was the controlled environment.\nThe full game is where the variables\nstart fighting back.";

    public const string StageUnlockHint = "Clear the previous sector to unlock this one.";

    // Canonical tower language reused across UI surfaces.
    public const string RocketLauncherBaseDescription =
        "Rocket Launcher fires explosive rockets that damage the target and nearby enemies.";
    public const string RocketLauncherBlastCoreDescription =
        RocketLauncherBaseDescription + " Blast Core further expands the blast radius.";
    public const string UndertowEngineBaseDescription =
        "Undertow Engine drags enemies backward so they spend longer inside your defenses.";
    public const string LatchNestBaseDescription =
        "Latch Nest fires parasite pods. Primary impacts latch living parasites that repeatedly bite as secondary hits.";

    // Tutorial / first-run banner copy.
    public const string DraftBannerBasicsHeader = "HOW THIS WORKS";
    public const string DraftBannerBasicsBody =
        "Pick one card each draft. Towers fill empty slots; modifiers upgrade towers you already have.\n" +
        "Waves run automatically. Survive 20 waves to win.";
    public const string DraftBannerSurgesHeader = "SURGES";
    public const string DraftBannerSurgesBody =
        "Modifiers generate charge as they activate (hits, kills, procs). When a tower's meter fills, it triggers a Surge: a powerful mid-wave effect.\n" +
        "Twist = the 2nd mod's trait added to that Surge. Bonus = Pulse (area), Strike (heavy), or Recharge (instant refire).\n" +
        "Each Surge adds to the Global Surge bar. Fill it and click the ready bar to activate a Global Surge, refunding cooldowns and hitting every enemy on the lane.";
}
