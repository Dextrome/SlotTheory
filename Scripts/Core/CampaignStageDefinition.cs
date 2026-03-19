using SlotTheory.Data;

namespace SlotTheory.Core;

/// <summary>
/// Runtime representation of a single campaign stage.
/// Wraps CampaignStageDef (pure data) and exposes a typed MandateDefinition.
/// </summary>
public sealed class CampaignStageDefinition
{
    public int               StageIndex    { get; }
    public string            MapId         { get; }
    public MandateDefinition Mandate       { get; }
    public string            StageName     { get; }
    public string            StageSubtitle { get; }
    public string            IntroLine     { get; }
    public string            ClearStamp    { get; }

    public CampaignStageDefinition(CampaignStageDef def)
    {
        StageIndex    = def.StageIndex;
        MapId         = def.MapId;
        Mandate       = new MandateDefinition(def.Mandate);
        StageName     = def.StageName;
        StageSubtitle = def.StageSubtitle;
        IntroLine     = def.IntroLine;
        ClearStamp    = def.ClearStamp;
    }
}
