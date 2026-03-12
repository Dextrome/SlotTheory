using SlotTheory.Core;
using Xunit;

namespace SlotTheory.Tests;

public class SpectacleDamageCoreTests
{
    [Fact]
    public void ApplyRawDamage_ReducesHp_AndReturnsDealtAmount()
    {
        var enemy = new FakeEnemy { Hp = 100f };

        float dealt = SpectacleDamageCore.ApplyRawDamage(enemy, 27.5f);

        Assert.Equal(27.5f, dealt, 3);
        Assert.Equal(72.5f, enemy.Hp, 3);
    }

    [Fact]
    public void ApplyRawDamage_ClampsHpAtZero()
    {
        var enemy = new FakeEnemy { Hp = 15f };

        float dealt = SpectacleDamageCore.ApplyRawDamage(enemy, 999f);

        Assert.Equal(15f, dealt, 3);
        Assert.Equal(0f, enemy.Hp, 3);
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(-5f)]
    [InlineData(0.01f)]
    public void ApplyRawDamage_IgnoresNonEffectiveDamage(float damage)
    {
        var enemy = new FakeEnemy { Hp = 80f };

        float dealt = SpectacleDamageCore.ApplyRawDamage(enemy, damage);

        Assert.Equal(0f, dealt, 3);
        Assert.Equal(80f, enemy.Hp, 3);
    }

    [Fact]
    public void ApplyRawDamage_IgnoresAlreadyDeadEnemy()
    {
        var enemy = new FakeEnemy { Hp = 0f };

        float dealt = SpectacleDamageCore.ApplyRawDamage(enemy, 25f);

        Assert.Equal(0f, dealt, 3);
        Assert.Equal(0f, enemy.Hp, 3);
    }
}
