using Unity.Netcode;
using UnityEngine;

namespace Vykhod
{
    /// <summary>Здоровье по частям тела + статусы. Синхронизируется как NetworkVariable.</summary>
    public struct BodyHealth : INetworkSerializable, System.IEquatable<BodyHealth>
    {
        public float head, thorax, stomach, larm, rarm, lleg, rleg;
        public float energy, hydration;
        public byte bleed, heavyBleed, fracture; // битовые маски по индексу части
        public const float MaxHead = 35, MaxThorax = 85, MaxStomach = 70, MaxArm = 60, MaxLeg = 65;
        public static readonly string[] Parts = { "head", "thorax", "stomach", "larm", "rarm", "lleg", "rleg" };
        public static readonly string[] PartNames = { "Голова", "Грудь", "Живот", "Л. рука", "П. рука", "Л. нога", "П. нога" };

        public static BodyHealth Full(float mul)
        {
            return new BodyHealth { head = MaxHead * mul, thorax = MaxThorax * mul, stomach = MaxStomach * mul, larm = MaxArm * mul, rarm = MaxArm * mul, lleg = MaxLeg * mul, rleg = MaxLeg * mul, energy = 100, hydration = 100 };
        }

        public static float MaxOf(int i, float mul) => (i == 0 ? MaxHead : i == 1 ? MaxThorax : i == 2 ? MaxStomach : i < 5 ? MaxArm : MaxLeg) * mul;
        public static int Index(string part)
        {
            switch (part)
            {
                case "head": return 0; case "thorax": return 1; case "stomach": return 2; case "larm": case "leftarm": return 3; case "rarm": case "rightarm": return 4;
                case "lleg": case "leftleg": return 5; case "rleg": case "rightleg": return 6; case "legs": return Random.value < 0.5f ? 5 : 6; case "arms": return Random.value < 0.5f ? 3 : 4;
            }
            return 1;
        }

        public float Get(int i) { switch (i) { case 0: return head; case 1: return thorax; case 2: return stomach; case 3: return larm; case 4: return rarm; case 5: return lleg; default: return rleg; } }
        public void Set(int i, float v) { switch (i) { case 0: head = v; break; case 1: thorax = v; break; case 2: stomach = v; break; case 3: larm = v; break; case 4: rarm = v; break; case 5: lleg = v; break; default: rleg = v; break; } }
        public float Total => head + thorax + stomach + larm + rarm + lleg + rleg;
        public float MaxTotal(float mul) => (MaxHead + MaxThorax + MaxStomach + MaxArm * 2 + MaxLeg * 2) * mul;
        public bool IsDead => head <= 0 || thorax <= 0;
        public bool Bleeding(int i) => (bleed & (1 << i)) != 0;
        public bool HeavyBleeding(int i) => (heavyBleed & (1 << i)) != 0;
        public bool Fractured(int i) => (fracture & (1 << i)) != 0;
        public bool AnyBleed => bleed != 0 || heavyBleed != 0;
        public bool AnyFracture => fracture != 0;
        public int WorstPart(float mul)
        {
            int best = 1; float bestFrac = 2;
            for (int i = 0; i < 7; i++) { float f = Get(i) / MaxOf(i, mul); if (f < bestFrac) { bestFrac = f; best = i; } }
            return best;
        }

        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        {
            s.SerializeValue(ref head); s.SerializeValue(ref thorax); s.SerializeValue(ref stomach); s.SerializeValue(ref larm); s.SerializeValue(ref rarm); s.SerializeValue(ref lleg); s.SerializeValue(ref rleg);
            s.SerializeValue(ref energy); s.SerializeValue(ref hydration); s.SerializeValue(ref bleed); s.SerializeValue(ref heavyBleed); s.SerializeValue(ref fracture);
        }

        public bool Equals(BodyHealth o) => head == o.head && thorax == o.thorax && stomach == o.stomach && larm == o.larm && rarm == o.rarm && lleg == o.lleg && rleg == o.rleg && energy == o.energy && hydration == o.hydration && bleed == o.bleed && heavyBleed == o.heavyBleed && fracture == o.fracture;
    }
}
