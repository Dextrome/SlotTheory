namespace SlotTheory.Core;

/// <summary>
/// Holds the active campaign stage for the current run.
/// Set by CampaignSelectPanel before entering Main.tscn.
/// Cleared when the player arrives at ModeSelect or MainMenu.
/// GameController reads this on _Ready() to apply the mandate and intro overlay.
/// </summary>
public static class CampaignManager
{
    public static CampaignStageDefinition? ActiveStage { get; private set; }

    public static bool IsCampaignRun => ActiveStage != null;

    public static void SetActiveStage(CampaignStageDefinition stage)
        => ActiveStage = stage;

    public static void ClearActiveStage()
        => ActiveStage = null;
}
