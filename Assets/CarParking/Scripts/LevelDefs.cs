using UnityEngine;

namespace CarParking
{
    public enum PropKind { ParkedCar, Cone, Pillar, Block, Kerb }

    /// <summary>One static object placed in a lot. Sizes are full extents in metres.</summary>
    public struct PropDef
    {
        public PropKind kind;
        public float x, z, yaw;
        public float sx, sy, sz;
        public Color paint;

        public static PropDef Car(float x, float z, float yaw, Color c)
        { return new PropDef { kind = PropKind.ParkedCar, x = x, z = z, yaw = yaw, paint = c, sx = 1.9f, sy = 1.15f, sz = 4.35f }; }

        public static PropDef Cone(float x, float z)
        { return new PropDef { kind = PropKind.Cone, x = x, z = z, sx = 0.44f, sy = 0.72f, sz = 0.44f }; }

        public static PropDef Pillar(float x, float z, float w, float h)
        { return new PropDef { kind = PropKind.Pillar, x = x, z = z, sx = w, sy = h, sz = w }; }

        /// <summary>A barrier lying across the lot. <paramref name="length"/> is its extent on X before yaw.</summary>
        public static PropDef Block(float x, float z, float yaw, float thickness, float height, float length)
        { return new PropDef { kind = PropKind.Block, x = x, z = z, yaw = yaw, sx = length, sy = height, sz = thickness }; }

        /// <summary>A kerb running east-west. <paramref name="length"/> is its extent on X.</summary>
        public static PropDef Kerb(float x, float z, float thickness, float height, float length)
        { return new PropDef { kind = PropKind.Kerb, x = x, z = z, sx = length, sy = height, sz = thickness }; }
    }

    /// <summary>A painted bay: centre, heading, and full width (across the car) and length (along it).</summary>
    public struct BayDef
    {
        public float x, z, yaw, w, len;

        public static BayDef At(float x, float z, float yaw = 0f, float w = 3.3f, float len = 5.9f)
        { return new BayDef { x = x, z = z, yaw = yaw, w = w, len = len }; }
    }

    public class LevelDef
    {
        public string name = "Level";
        public string hint = "";
        public float lotW = 30f, lotL = 26f;
        public float timeLimit = 60f;
        public float spawnX, spawnZ, spawnYaw;
        public BayDef target;
        public BayDef[] bays = new BayDef[0];
        public PropDef[] props = new PropDef[0];
    }

    /// <summary>The six lots, laid out on paper rather than in a scene file.</summary>
    public static class Levels
    {
        public static readonly Color Red = new Color(0.42f, 0.06f, 0.07f);
        public static readonly Color Silver = new Color(0.55f, 0.57f, 0.60f);
        public static readonly Color Green = new Color(0.10f, 0.24f, 0.14f);
        public static readonly Color Grey = new Color(0.28f, 0.29f, 0.31f);
        public static readonly Color White = new Color(0.78f, 0.78f, 0.76f);
        public static readonly Color Bronze = new Color(0.45f, 0.30f, 0.05f);

        public static readonly LevelDef[] All =
        {
            // ---------------------------------------------------------------- 1
            new LevelDef
            {
                name = "Empty Lot",
                hint = "Drive forward and park in the glowing bay.",
                lotW = 34f, lotL = 26f, timeLimit = 60f,
                spawnX = -6.5f, spawnZ = -9.5f, spawnYaw = 0f,
                target = BayDef.At(-6.5f, 6f),
                bays = new[]
                {
                    BayDef.At(-13.7f, 6f), BayDef.At(-10.1f, 6f),
                    BayDef.At(-6.5f, 6f),  BayDef.At(-2.9f, 6f), BayDef.At(0.7f, 6f),
                },
                props = new PropDef[0],
            },

            // ---------------------------------------------------------------- 2
            new LevelDef
            {
                name = "Between Neighbours",
                hint = "Both adjacent bays are taken. Mind the paintwork.",
                lotW = 34f, lotL = 26f, timeLimit = 55f,
                spawnX = -6.5f, spawnZ = -9.5f, spawnYaw = 0f,
                target = BayDef.At(-6.5f, 6f),
                bays = new[]
                {
                    BayDef.At(-13.7f, 6f), BayDef.At(-10.1f, 6f),
                    BayDef.At(-6.5f, 6f),  BayDef.At(-2.9f, 6f), BayDef.At(0.7f, 6f),
                },
                props = new[]
                {
                    PropDef.Car(-13.7f, 6f, 180f, Silver),
                    PropDef.Car(-10.1f, 6f, 180f, Red),
                    PropDef.Car(-2.9f, 6f, 180f, Green),
                    PropDef.Car(0.7f, 6f, 180f, Grey),
                },
            },

            // ---------------------------------------------------------------- 3
            new LevelDef
            {
                name = "Turn In",
                hint = "The row runs across your path. Turn right, then back left into the gap.",
                lotW = 34f, lotL = 30f, timeLimit = 70f,
                spawnX = 0f, spawnZ = -11f, spawnYaw = 0f,
                target = BayDef.At(8.0f, 7.5f),
                bays = new[]
                {
                    BayDef.At(-3.4f, 7.5f), BayDef.At(0.4f, 7.5f), BayDef.At(4.2f, 7.5f),
                    BayDef.At(8.0f, 7.5f),  BayDef.At(11.8f, 7.5f),
                },
                props = new[]
                {
                    PropDef.Car(-3.4f, 7.5f, 180f, Bronze),
                    PropDef.Car(0.4f, 7.5f, 180f, Silver),
                    PropDef.Car(4.2f, 7.5f, 180f, White),
                    PropDef.Car(11.8f, 7.5f, 180f, Red),
                    PropDef.Pillar(-2.5f, -4f, 0.8f, 3.4f),
                    PropDef.Pillar(2.5f, -4f, 0.8f, 3.4f),
                },
            },

            // ---------------------------------------------------------------- 4
            new LevelDef
            {
                name = "Tight Aisle",
                hint = "A 5 m aisle. There is no room to swing round — reverse in.",
                lotW = 24f, lotL = 28f, timeLimit = 75f,
                spawnX = 2.6f, spawnZ = -11f, spawnYaw = 0f,
                target = BayDef.At(-2.9f, 2.8f, 90f, 2.9f, 5.8f),
                bays = new[]
                {
                    BayDef.At(-2.9f, 2.8f, 90f, 2.9f, 5.8f),
                    BayDef.At(-2.9f, 6.4f, 90f, 2.9f, 5.8f),
                    BayDef.At(-2.9f, 10.0f, 90f, 2.9f, 5.8f),
                    BayDef.At(8.4f, 2.8f, -90f, 2.9f, 5.8f),
                    BayDef.At(8.4f, 6.4f, -90f, 2.9f, 5.8f),
                    BayDef.At(8.4f, 10.0f, -90f, 2.9f, 5.8f),
                },
                props = new[]
                {
                    PropDef.Car(-2.9f, 6.4f, 90f, Grey),
                    PropDef.Car(-2.9f, 10.0f, 90f, Silver),
                    PropDef.Car(8.4f, 2.8f, -90f, Red),
                    PropDef.Car(8.4f, 6.4f, -90f, White),
                    PropDef.Car(8.4f, 10.0f, -90f, Green),
                },
            },

            // ---------------------------------------------------------------- 5
            new LevelDef
            {
                name = "Parallel Parking",
                hint = "Along the kerb, between two parked cars. Swing out, then back in.",
                lotW = 36f, lotL = 14f, timeLimit = 90f,
                spawnX = -14.5f, spawnZ = 0.5f, spawnYaw = 90f,
                target = BayDef.At(-0.55f, 4.6f, 90f, 2.4f, 6.6f),
                bays = new[] { BayDef.At(-0.55f, 4.6f, 90f, 2.4f, 6.6f) },
                props = new[]
                {
                    PropDef.Car(-6.6f, 4.6f, 90f, Silver),
                    PropDef.Car(5.2f, 4.6f, 90f, Red),
                    PropDef.Car(-14.5f, 4.6f, 90f, Grey),
                    PropDef.Car(12.8f, 4.6f, 90f, Green),
                    PropDef.Kerb(0f, 6.4f, 0.5f, 0.26f, 36f),
                },
            },

            // ---------------------------------------------------------------- 6
            new LevelDef
            {
                name = "Full House",
                hint = "The far corner bay. The barrier only has one way through.",
                lotW = 30f, lotL = 30f, timeLimit = 85f,
                spawnX = -11f, spawnZ = -10f, spawnYaw = 0f,
                target = BayDef.At(6.2f, 10f),
                bays = new[]
                {
                    BayDef.At(-8.2f, 10f), BayDef.At(-4.6f, 10f), BayDef.At(-1.0f, 10f),
                    BayDef.At(2.6f, 10f),  BayDef.At(6.2f, 10f),
                },
                props = new[]
                {
                    PropDef.Car(-8.2f, 10f, 180f, Bronze),
                    PropDef.Car(-4.6f, 10f, 180f, Silver),
                    PropDef.Car(-1.0f, 10f, 180f, Grey),
                    PropDef.Car(2.6f, 10f, 180f, Red),
                    // The barrier must leave a real aisle in front of the bays. At z=6 its north
                    // face sat 1.53 m from the parked cars while the car is 1.9 m wide, so the lot
                    // was impassable. At z=1.5 the aisle is 6.03 m.
                    PropDef.Block(-8.5f, 1.5f, 0f, 0.6f, 1.3f, 13f),
                    PropDef.Block(9.0f, 1.5f, 0f, 0.6f, 1.3f, 12f),
                    PropDef.Pillar(-5f, -6f, 0.9f, 3.6f),
                    PropDef.Pillar(5f, -6f, 0.9f, 3.6f),
                    // these two mark the gap edges from the approach side, without blocking the aisle
                    PropDef.Cone(-2.6f, -1.5f),
                    PropDef.Cone(3.6f, -1.5f),
                },
            },
        };
    }
}
