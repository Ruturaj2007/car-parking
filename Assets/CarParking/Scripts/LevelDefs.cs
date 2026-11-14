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

    /// <summary>
    /// The six lots, laid out on paper rather than in a scene file.
    ///
    /// Each lot is a *place* with one idea, not the same slab with more boxes on it. The sequence
    /// teaches one thing at a time: drive straight in, line up through a gate, turn in from an
    /// approach that does not line up, reverse into a canyon, reverse into a street gap, then find
    /// the one route through a barrier. Difficulty comes from the shape of the space and the
    /// manoeuvre it demands, never from shrinking numbers alone.
    ///
    /// Dimensions are checked against the widest car in the garage (Ferrari, 2.23 m wide, 4.40 m
    /// long) and the largest bay-test footprint (2.01 x 4.05), so every bay is parkable by every car.
    /// </summary>
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
            // ---------------------------------------------------------------- 1  teach: drive in
            new LevelDef
            {
                name = "First Day",
                hint = "Straight ahead into the glowing bay. Nothing to hit — get a feel for the car.",
                lotW = 30f, lotL = 24f, timeLimit = 50f,
                spawnX = -2.7f, spawnZ = -8.5f, spawnYaw = 0f,
                target = BayDef.At(-2.7f, 6f, 0f, 3.0f, 5.9f),
                bays = new[]
                {
                    BayDef.At(-9.5f, 6f, 0f, 3.0f, 5.9f), BayDef.At(-6.1f, 6f, 0f, 3.0f, 5.9f),
                    BayDef.At(-2.7f, 6f, 0f, 3.0f, 5.9f), BayDef.At(0.7f, 6f, 0f, 3.0f, 5.9f),
                    BayDef.At(4.1f, 6f, 0f, 3.0f, 5.9f),
                },
                props = new[]
                {
                    // the row has a back edge, so the bays read as a row rather than floating lines
                    PropDef.Kerb(0f, 9.7f, 0.6f, 0.3f, 30f),
                },
            },

            // ---------------------------------------------------------------- 2  teach: line up
            new LevelDef
            {
                name = "Between Two",
                hint = "Neighbours either side. Thread the cones, then slide straight in.",
                lotW = 30f, lotL = 24f, timeLimit = 55f,
                spawnX = -2.7f, spawnZ = -8.5f, spawnYaw = 0f,
                target = BayDef.At(-2.7f, 6f, 0f, 3.0f, 5.9f),
                bays = new[]
                {
                    BayDef.At(-9.5f, 6f, 0f, 3.0f, 5.9f), BayDef.At(-6.1f, 6f, 0f, 3.0f, 5.9f),
                    BayDef.At(-2.7f, 6f, 0f, 3.0f, 5.9f), BayDef.At(0.7f, 6f, 0f, 3.0f, 5.9f),
                    BayDef.At(4.1f, 6f, 0f, 3.0f, 5.9f),
                },
                props = new[]
                {
                    PropDef.Car(-9.5f, 6f, 180f, Silver),
                    PropDef.Car(-6.1f, 6f, 180f, Red),
                    PropDef.Car(0.7f, 6f, 180f, Green),
                    PropDef.Car(4.1f, 6f, 180f, Grey),
                    // a 4.6 m gate on the bay's centre line: wide enough to be fair, narrow enough
                    // that wandering off line makes the park harder
                    PropDef.Cone(-5.0f, -2f),
                    PropDef.Cone(-0.4f, -2f),
                    PropDef.Kerb(0f, 9.7f, 0.6f, 0.3f, 30f),
                },
            },

            // ---------------------------------------------------------------- 3  teach: turn in
            new LevelDef
            {
                name = "The Turn",
                hint = "The row faces across your path, not down it. Drive past, then turn in.",
                lotW = 34f, lotL = 30f, timeLimit = 70f,
                spawnX = -12f, spawnZ = -10f, spawnYaw = 0f,
                // row against the east wall, opening to the west: the approach is perpendicular to it
                target = BayDef.At(13.2f, 4.8f, 90f, 3.0f, 5.9f),
                bays = new[]
                {
                    BayDef.At(13.2f, -2.0f, 90f, 3.0f, 5.9f), BayDef.At(13.2f, 1.4f, 90f, 3.0f, 5.9f),
                    BayDef.At(13.2f, 4.8f, 90f, 3.0f, 5.9f), BayDef.At(13.2f, 8.2f, 90f, 3.0f, 5.9f),
                },
                props = new[]
                {
                    PropDef.Car(13.2f, -2.0f, 90f, Bronze),
                    PropDef.Car(13.2f, 1.4f, 90f, Silver),
                    PropDef.Car(13.2f, 8.2f, 90f, White),
                    // a colonnade down the middle: you cannot take a straight run at the bay, and the
                    // 3.7 m openings between pillars make the crossing a real piece of driving
                    PropDef.Pillar(-4.5f, -7f, 0.8f, 3.4f),
                    PropDef.Pillar(-4.5f, -2.5f, 0.8f, 3.4f),
                    PropDef.Pillar(-4.5f, 2f, 0.8f, 3.4f),
                    PropDef.Pillar(-4.5f, 6.5f, 0.8f, 3.4f),
                },
            },

            // ---------------------------------------------------------------- 4  teach: reverse in
            new LevelDef
            {
                name = "Squeeze",
                hint = "A 5.7 m aisle with cars either side. No room to swing round — reverse in.",
                lotW = 26f, lotL = 28f, timeLimit = 80f,
                spawnX = 2.8f, spawnZ = -10f, spawnYaw = 0f,
                // two facing rows: bays run across the aisle, so the aisle is the only space you have
                target = BayDef.At(-3.0f, 5.4f, 90f, 3.0f, 5.9f),
                bays = new[]
                {
                    BayDef.At(-3.0f, 2.0f, 90f, 3.0f, 5.9f),
                    BayDef.At(-3.0f, 5.4f, 90f, 3.0f, 5.9f),
                    BayDef.At(-3.0f, 8.8f, 90f, 3.0f, 5.9f),
                    BayDef.At(8.6f, 2.0f, -90f, 3.0f, 5.9f),
                    BayDef.At(8.6f, 5.4f, -90f, 3.0f, 5.9f),
                    BayDef.At(8.6f, 8.8f, -90f, 3.0f, 5.9f),
                },
                props = new[]
                {
                    PropDef.Car(-3.0f, 2.0f, 90f, Grey),
                    PropDef.Car(-3.0f, 8.8f, 90f, Silver),
                    PropDef.Car(8.6f, 2.0f, -90f, Red),
                    PropDef.Car(8.6f, 5.4f, -90f, White),
                    PropDef.Car(8.6f, 8.8f, -90f, Green),
                },
            },

            // ---------------------------------------------------------------- 5  teach: the street
            new LevelDef
            {
                name = "Kerb Side",
                hint = "Along the kerb, between two cars. Pull alongside, then reverse into the gap.",
                lotW = 40f, lotL = 10f, timeLimit = 90f,
                spawnX = -14f, spawnZ = -1.6f, spawnYaw = 90f,
                // a 6.6 m gap for a 4.4 m car: enough to be possible, tight enough to need the swing
                target = BayDef.At(0f, 2.4f, 90f, 2.9f, 6.6f),
                bays = new[]
                {
                    BayDef.At(0f, 2.4f, 90f, 2.9f, 6.6f),
                },
                props = new[]
                {
                    PropDef.Car(-5.5f, 2.4f, 90f, Silver),
                    PropDef.Car(5.5f, 2.4f, 90f, Red),
                    PropDef.Car(12.5f, 2.4f, 90f, Green),
                    // the kerb is the far side of the bay, so overshooting it costs a scrape
                    PropDef.Kerb(0f, 4.4f, 0.6f, 0.3f, 40f),
                },
            },

            // ---------------------------------------------------------------- 6  teach: find the route
            new LevelDef
            {
                name = "Full House",
                hint = "One bay left, far corner. The barrier has a single gap — find it first.",
                lotW = 34f, lotL = 34f, timeLimit = 95f,
                spawnX = -13f, spawnZ = -13f, spawnYaw = 0f,
                target = BayDef.At(6.8f, 12f, 0f, 3.0f, 5.9f),
                bays = new[]
                {
                    BayDef.At(-6.8f, 12f, 0f, 3.0f, 5.9f), BayDef.At(-3.4f, 12f, 0f, 3.0f, 5.9f),
                    BayDef.At(0f, 12f, 0f, 3.0f, 5.9f),    BayDef.At(3.4f, 12f, 0f, 3.0f, 5.9f),
                    BayDef.At(6.8f, 12f, 0f, 3.0f, 5.9f),
                },
                props = new[]
                {
                    PropDef.Car(-6.8f, 12f, 180f, Bronze),
                    PropDef.Car(-3.4f, 12f, 180f, Silver),
                    PropDef.Car(0f, 12f, 180f, Grey),
                    PropDef.Car(3.4f, 12f, 180f, Red),
                    // the barrier runs wall to wall except for a single 5.0 m gap at x 1..6, so the
                    // only way north is through it; the aisle beyond is 5.75 m, enough to reverse in
                    PropDef.Block(-8f, 3f, 0f, 0.6f, 1.3f, 18f),
                    PropDef.Block(11.5f, 3f, 0f, 0.6f, 1.3f, 11f),
                    // off-centre pillars in the approach: the run up to the gap is a weave, not a straight
                    PropDef.Pillar(-14f, -6f, 0.9f, 3.4f),
                    PropDef.Pillar(-9f, -9f, 0.9f, 3.4f),
                },
            },
        };
    }
}