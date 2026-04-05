using SlotTheory.Core;
using Xunit;

namespace SlotTheory.Tests;

public sealed class EnemyCatalogMechanicsTests
{
    [Fact]
    public void ControlProfile_AnchorHasStrongerResistanceThanArmored()
    {
        EnemyControlProfile armored = EnemyCatalog.GetControlProfile(EnemyCatalog.ArmoredWalkerId);
        EnemyControlProfile anchor = EnemyCatalog.GetControlProfile(EnemyCatalog.AnchorWalkerId);

        Assert.True(anchor.UndertowPullMultiplier < armored.UndertowPullMultiplier);
        Assert.True(anchor.AccordionCompressionMultiplier < 1f);
    }

    [Fact]
    public void ResolveLancerDashDistance_StrongSlowSuppressesDash()
    {
        float dash = EnemyCatalog.ResolveLancerDashDistance(
            isPinned: false,
            isSlowed: true,
            slowSpeedFactor: 0.28f);

        Assert.True(dash < Balance.LancerWalkerDashDistanceMin);
    }

    [Fact]
    public void ResolveLancerDashDistance_PinFullyBlocksDash()
    {
        float dash = EnemyCatalog.ResolveLancerDashDistance(
            isPinned: true,
            isSlowed: false,
            slowSpeedFactor: 1f);

        Assert.Equal(0f, dash, precision: 4);
    }

    [Fact]
    public void TryConsumeVeilShell_ReducesIncomingDamageAndStartsRefresh()
    {
        bool shell = true;
        float refresh = 0f;
        float damage = 100f;

        bool consumed = EnemyCatalog.TryConsumeVeilShell(ref shell, ref refresh, ref damage);

        Assert.True(consumed);
        Assert.False(shell);
        Assert.Equal(100f * (1f - Balance.VeilWalkerShellDamageReduction), damage, precision: 3);
        Assert.Equal(Balance.VeilWalkerShellRefreshDelay, refresh, precision: 3);
    }

    [Fact]
    public void AdvanceVeilRefresh_ReactivatesShellAtZero()
    {
        bool shell = false;
        float refresh = 0.4f;

        EnemyCatalog.AdvanceVeilRefresh(ref shell, ref refresh, delta: 0.5f);

        Assert.True(shell);
        Assert.Equal(0f, refresh, precision: 3);
    }
}
