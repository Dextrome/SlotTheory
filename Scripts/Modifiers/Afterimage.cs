using SlotTheory.Data;

namespace SlotTheory.Modifiers;

/// <summary>
/// Afterimage behavior is orchestrated by DamageModel + CombatSim:
/// hits seed a delayed imprint, then CombatSim triggers one reduced echo.
/// This class exists as the equipable modifier identity.
/// </summary>
public class Afterimage : Modifier
{
    public Afterimage(ModifierDef def) { ModifierId = def.Id; }
}
