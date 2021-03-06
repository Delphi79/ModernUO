using System;
using System.Buffers;
using Server.Mobiles;
using Server.Network;

namespace Server
{
    public class BuffInfo
    {
        public BuffInfo(BuffIcon iconID, int titleCliloc)
            : this(iconID, titleCliloc, titleCliloc + 1)
        {
        }

        public BuffInfo(BuffIcon iconID, int titleCliloc, int secondaryCliloc)
        {
            ID = iconID;
            TitleCliloc = titleCliloc;
            SecondaryCliloc = secondaryCliloc;
        }

        public BuffInfo(BuffIcon iconID, int titleCliloc, TimeSpan length, Mobile m)
            : this(iconID, titleCliloc, titleCliloc + 1, length, m)
        {
        }

        // Only the timed one needs to Mobile to know when to automagically remove it.
        public BuffInfo(BuffIcon iconID, int titleCliloc, int secondaryCliloc, TimeSpan length, Mobile m)
            : this(iconID, titleCliloc, secondaryCliloc)
        {
            TimeLength = length;
            TimeStart = DateTime.UtcNow;

            Timer = Timer.DelayCall(length, RemoveBuff, m, this);
        }

        public BuffInfo(BuffIcon iconID, int titleCliloc, TextDefinition args)
            : this(iconID, titleCliloc, titleCliloc + 1, args)
        {
        }

        public BuffInfo(BuffIcon iconID, int titleCliloc, int secondaryCliloc, TextDefinition args)
            : this(iconID, titleCliloc, secondaryCliloc) =>
            Args = args;

        public BuffInfo(BuffIcon iconID, int titleCliloc, bool retainThroughDeath)
            : this(iconID, titleCliloc, titleCliloc + 1, retainThroughDeath)
        {
        }

        public BuffInfo(BuffIcon iconID, int titleCliloc, int secondaryCliloc, bool retainThroughDeath)
            : this(iconID, titleCliloc, secondaryCliloc) =>
            RetainThroughDeath = retainThroughDeath;

        public BuffInfo(BuffIcon iconID, int titleCliloc, TextDefinition args, bool retainThroughDeath)
            : this(iconID, titleCliloc, titleCliloc + 1, args, retainThroughDeath)
        {
        }

        public BuffInfo(BuffIcon iconID, int titleCliloc, int secondaryCliloc, TextDefinition args, bool retainThroughDeath)
            : this(iconID, titleCliloc, secondaryCliloc, args) =>
            RetainThroughDeath = retainThroughDeath;

        public BuffInfo(BuffIcon iconID, int titleCliloc, TimeSpan length, Mobile m, TextDefinition args)
            : this(iconID, titleCliloc, titleCliloc + 1, length, m, args)
        {
        }

        public BuffInfo(
            BuffIcon iconID, int titleCliloc, int secondaryCliloc, TimeSpan length, Mobile m,
            TextDefinition args
        )
            : this(iconID, titleCliloc, secondaryCliloc, length, m) =>
            Args = args;

        public BuffInfo(
            BuffIcon iconID, int titleCliloc, TimeSpan length, Mobile m, TextDefinition args,
            bool retainThroughDeath
        )
            : this(iconID, titleCliloc, titleCliloc + 1, length, m, args, retainThroughDeath)
        {
        }

        public BuffInfo(
            BuffIcon iconID, int titleCliloc, int secondaryCliloc, TimeSpan length, Mobile m,
            TextDefinition args, bool retainThroughDeath
        )
            : this(iconID, titleCliloc, secondaryCliloc, length, m)
        {
            Args = args;
            RetainThroughDeath = retainThroughDeath;
        }

        public static bool Enabled { get; private set; }

        public BuffIcon ID { get; }

        public int TitleCliloc { get; }

        public int SecondaryCliloc { get; }

        public TimeSpan TimeLength { get; }

        public DateTime TimeStart { get; }

        public Timer Timer { get; }

        public bool RetainThroughDeath { get; }

        public TextDefinition Args { get; }

        public static void Configure()
        {
            Enabled = ServerConfiguration.GetOrUpdateSetting("buffIcons.enable", Core.ML);
        }

        public static void Initialize()
        {
            if (Enabled)
            {
                EventSink.ClientVersionReceived += ResendBuffsOnClientVersionReceived;
            }
        }

        public static void ResendBuffsOnClientVersionReceived(NetState ns, ClientVersion cv)
        {
            if (ns.Mobile is PlayerMobile pm)
            {
                Timer.DelayCall(pm.ResendBuffs);
            }
        }

        public static void AddBuff(Mobile m, BuffInfo b)
        {
            (m as PlayerMobile)?.AddBuff(b);
        }

        public static void RemoveBuff(Mobile m, BuffInfo b)
        {
            (m as PlayerMobile)?.RemoveBuff(b);
        }

        public static void RemoveBuff(Mobile m, BuffIcon b)
        {
            (m as PlayerMobile)?.RemoveBuff(b);
        }

        public void SendAddBuffPacket(NetState ns, Serial m) => SendAddBuffPacket(
            ns,
            m,
            ID,
            TitleCliloc,
            SecondaryCliloc,
            Args,
            TimeStart != DateTime.MinValue ? TimeStart + TimeLength - DateTime.UtcNow : TimeSpan.Zero
        );

        public static void SendAddBuffPacket(
            NetState ns, Serial mob, BuffIcon iconID, int titleCliloc, int secondaryCliloc, TextDefinition args,
            TimeSpan ts
        )
        {
            if (ns == null)
            {
                return;
            }

            var hasArgs = args != null;
            var length = hasArgs ? args.ToString().Length * 2 + 52 : 46;
            var writer = new SpanWriter(stackalloc byte[length]);
            writer.Write((byte)0xDF); // Packet ID
            writer.Write((ushort)length);
            writer.Write(mob);
            writer.Write((short)iconID);
            writer.Write((short)0x1); // command (0 = remove, 1 = add, 2 = data)
            writer.Write(0);

            writer.Write((short)iconID);
            writer.Write((short)0x1); // command (0 = remove, 1 = add, 2 = data)
            writer.Write(0);
            writer.Write((short)(ts <= TimeSpan.Zero ? 0 : ts.TotalSeconds));
            writer.Clear(3);
            writer.Write(titleCliloc);
            writer.Write(secondaryCliloc);

            if (hasArgs)
            {
                writer.Write(0);
                writer.Write((short)0x1);
                writer.Write((ushort)0);
                writer.WriteLE('\t');
                writer.WriteLittleUniNull(args);
                writer.Write((short)0x1);
                writer.Write((ushort)0);
            }
            else
            {
                writer.Clear(10);
            }

            ns.Send(writer.Span);
        }

        public void SendRemoveBuffPacket(NetState ns, Serial mob) => SendRemoveBuffPacket(ns, mob, ID);

        public static void SendRemoveBuffPacket(NetState ns, Serial mob, BuffIcon iconID)
        {
            if (ns == null)
            {
                return;
            }

            var writer = new SpanWriter(stackalloc byte[15]);
            writer.Write((byte)0xDF); // Packet ID
            writer.Write((ushort)15);
            writer.Write(mob);
            writer.Write((short)iconID);
            writer.Write((short)0x0); // command (0 = remove, 1 = add, 2 = data)
            writer.Write(0);

            ns.Send(writer.Span);
        }
    }

    public enum BuffIcon : short
    {
        DismountPrevention = 0x3E9,
        NoRearm = 0x3EA,

        // Currently, no 0x3EB or 0x3EC
        NightSight = 0x3ED, // *
        DeathStrike,
        EvilOmen,
        UnknownStandingSwirl, // Which is healing throttle & Stamina throttle?
        UnknownKneelingSword,
        DivineFury,         // *
        EnemyOfOne,         // *
        HidingAndOrStealth, // *
        ActiveMeditation,   // *
        BloodOathCaster,    // *
        BloodOathCurse,     // *
        CorpseSkin,         // *
        Mindrot,            // *
        PainSpike,          // *
        Strangle,
        GiftOfRenewal,     // *
        AttuneWeapon,      // *
        Thunderstorm,      // *
        EssenceOfWind,     // *
        EtherealVoyage,    // *
        GiftOfLife,        // *
        ArcaneEmpowerment, // *
        MortalStrike,
        ReactiveArmor, // *
        Protection,    // *
        ArchProtection,
        MagicReflection, // *
        Incognito,       // *
        Disguised,
        AnimalForm,
        Polymorph,
        Invisibility, // *
        Paralyze,     // *
        Poison,
        Bleed,
        Clumsy,     // *
        FeebleMind, // *
        Weaken,     // *
        Curse,      // *
        MassCurse,
        Agility,  // *
        Cunning,  // *
        Strength, // *
        Bless,    // *
        Sleep,
        StoneForm,
        SpellPlague,
        SpellTrigger,
        NetherBolt,
        Fly
    }
}
