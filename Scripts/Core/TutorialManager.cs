using System.Collections.Generic;

namespace SlotTheory.Core;

/// <summary>
/// Manages the scripted tutorial run: curated draft picks and contextual callouts.
/// Created and owned by GameController when a tutorial run starts.
///
/// Intentionally has no Godot dependencies - all UI/scene interactions (overlays,
/// pausing, HUD highlighting) are handled by GameController, which calls into here
/// only to check flags and get draft pick data / callout text.
/// </summary>
public class TutorialManager
{
    // Scripted draft picks per wave index (0-based). null entry = random draft.
    private static readonly Dictionary<int, DraftOption[]> ScriptedPicks = new()
    {
        [0] = new[] { new DraftOption(DraftOptionType.Tower, "rapid_shooter") },
        [1] = new[] { new DraftOption(DraftOptionType.Modifier, "focus_lens"),
                      new DraftOption(DraftOptionType.Modifier, "hair_trigger") },
        [2] = new[] { new DraftOption(DraftOptionType.Modifier, "overreach") },
        [3] = new[] { new DraftOption(DraftOptionType.Tower, "marker_tower") },
        [4] = new[] { new DraftOption(DraftOptionType.Modifier, "exploit_weakness") },
        [5] = new[] { new DraftOption(DraftOptionType.Modifier, "chain_reaction"),
                      new DraftOption(DraftOptionType.Modifier, "momentum"),
                      new DraftOption(DraftOptionType.Modifier, "slow") },
    };

    public bool BuildNamePanelShown        { get; private set; } = false;
    public bool SurgePanelShown            { get; private set; } = false;

    public void MarkBuildNamePanelShown()           => BuildNamePanelShown           = true;
    public void MarkSurgePanelShown()               => SurgePanelShown               = true;

    private readonly UI.TutorialCallout _callout;

    public TutorialManager(UI.TutorialCallout callout)
    {
        _callout = callout;
    }

    /// <summary>Returns scripted options for this wave, or null for a free draft.</summary>
    public List<DraftOption>? GetScriptedOptions(int waveIndex)
    {
        if (!ScriptedPicks.TryGetValue(waveIndex, out var picks)) return null;
        return new List<DraftOption>(picks);
    }

    public void OnDraftOpened(int waveIndex)
    {
        string? text = waveIndex switch
        {
            0 => "DRAFT\nPick a tower. It defends the path.\nYou have 6 slots - fill them each wave.",
            1 => "MODIFIER\nAttach one to a tower you've placed.\nModifiers change how a tower fights.",
            2 => "OVERREACH\nExtends your tower's range.\nWatch the circle grow after you equip it.",
            3 => "MARKER TOWER\nDoesn't deal much damage on its own.\nMarks enemies so everything else hits harder.",
            4 => "EXPLOIT WEAKNESS\nDeals bonus damage to Marked enemies.\nPair it with your Marker Tower.",
            5 => "Three strong options - your call.\nNo wrong answer from here.",
            _ => null
        };
        if (text != null)
            _callout.Show(text);
    }

    public void OnWaveStarted(int waveIndex)
    {
        if (waveIndex == 0)
        {
            _callout.Show(
                "Enemies walk the path. Stop them before they reach the end.\nEach one that escapes costs you a life.");
        }
        else if (waveIndex == 4)
        {
            _callout.Show(
                "ARMORED WALKER - 3.5× HP, costs 2 lives if it escapes.\nFocus your heaviest hitters on it.");
        }
    }

    /// <summary>Clears any active or queued wave callouts (called when the wave ends).</summary>
    public void DismissCallouts() => _callout.DismissAll();
}
