using System;
using ModernUO.Serialization;
using Server.Spells;

namespace Server.Items;

[SerializationGenerator(0, false)]
public abstract partial class BaseAgilityPotion : BasePotion
{
    public BaseAgilityPotion(PotionEffect effect) : base(0xF08, effect)
    {
    }

    public abstract int DexOffset { get; }
    public abstract TimeSpan Duration { get; }

    public override bool CanDrink(Mobile from)
    {
        if (!base.CanDrink(from))
        {
            return false;
        }

        // TODO: Verify scaled; is it offset, duration, or both?
        if (!SpellHelper.AddStatOffset(from, StatType.Dex, Scale(from, DexOffset), Duration))
        {
            from.SendLocalizedMessage(502173); // You are already under a similar effect.
            return false;
        }

        from.FixedEffect(0x375A, 10, 15);
        from.PlaySound(0x1E7);
        return true;
    }

    public override void Drink(Mobile from)
    {
        PlayDrinkEffect(from);
    }
}
