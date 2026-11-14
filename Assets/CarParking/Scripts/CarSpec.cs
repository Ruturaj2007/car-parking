using UnityEngine;

namespace CarParking
{
    /// <summary>
    /// One drivable car: where its model lives, how it is fitted into the world, and how it
    /// handles. The GLB-derived models arrive at wildly different scales (two of them are
    /// authored at 1/100), so those use <see cref="autoFit"/> and are normalised from their own
    /// measured bounds instead of carrying hand-tuned numbers.
    /// </summary>
    public class CarSpec
    {
        public string key;
        public string displayName;
        public string modelResource;          // Resources path, no extension

        /// <summary>Normalise scale, axis and ground contact from the model's own bounds.</summary>
        public bool autoFit;

        /// <summary>Length the auto-fit model is scaled to, in metres.</summary>
        public float targetLength = 4.4f;

        /// <summary>
        /// Orientation correction applied to the model before it is measured, so the finished car is
        /// upright with its nose along the car's +Z (the direction the controller drives).
        /// Every downloaded model arrives facing -Z, and two of them are also upside down; a 180 turn
        /// about X corrects both defects at once, which is why those two are both flipped and reversed.
        /// It must be applied before auto-fit measures, or a car is centred and then flipped through
        /// the floor.
        /// </summary>
        public Vector3 modelRotationDeg;

        /// <summary>Use the hand-authored body paint material mapping (the original car only).</summary>
        public bool remapMaterials;

        // collider + bay-test footprint; overwritten by auto-fit for the measured cars
        public Vector3 bodySize = new Vector3(1.90f, 1.15f, 4.35f);
        public float bodyCentreY = 0.70f;
        public Vector2 footprint = new Vector2(1.80f, 4.10f);

        // handling
        public float mass = 1200f;
        public float acceleration = 8.5f;
        public float maxSpeed = 13f;
        public float steerRateDegPerSec = 92f;
        public float lateralGrip = 14f;
    }

    public static class CarCatalog
    {
        public static readonly CarSpec[] All =
        {
            new CarSpec
            {
                key = "stock", displayName = "Blue Track Car",
                modelResource = "car", autoFit = false, remapMaterials = true,
                modelRotationDeg = new Vector3(0f, 180f, 0f),
                bodySize = new Vector3(1.90f, 1.15f, 4.35f), bodyCentreY = 0.70f,
                footprint = new Vector2(1.80f, 4.10f),
                mass = 1200f, acceleration = 8.5f, maxSpeed = 13f,
                steerRateDegPerSec = 92f, lateralGrip = 14f,
            },
            new CarSpec
            {
                key = "c63", displayName = "Mercedes-AMG C63 DTM",
                modelResource = "Cars/c63/c63", autoFit = true, modelRotationDeg = new Vector3(180f, 0f, 0f),
                mass = 1250f, acceleration = 9.5f, maxSpeed = 14f,
                steerRateDegPerSec = 90f, lateralGrip = 16f,
            },
            new CarSpec
            {
                key = "clk", displayName = "Mercedes CLK DTM",
                modelResource = "Cars/clk/clk", autoFit = true,
                mass = 1150f, acceleration = 10f, maxSpeed = 14.5f,
                steerRateDegPerSec = 90f, lateralGrip = 16f,
            },
            new CarSpec
            {
                key = "ferrari", displayName = "Ferrari Daytona SP3",
                modelResource = "Cars/ferrari/ferrari", autoFit = true, modelRotationDeg = new Vector3(180f, 0f, 0f),
                mass = 1100f, acceleration = 10.5f, maxSpeed = 15f,
                steerRateDegPerSec = 86f, lateralGrip = 17f,
            },
            new CarSpec
            {
                key = "delorean", displayName = "DeLorean Time Machine",
                modelResource = "Cars/delorean/delorean", autoFit = true,
                mass = 1350f, acceleration = 8f, maxSpeed = 12.5f,
                steerRateDegPerSec = 100f, lateralGrip = 12f,
            },
        };

        public static int IndexOf(string key)
        {
            for (int i = 0; i < All.Length; i++)
                if (All[i].key == key) return i;
            return 0;
        }

        public static CarSpec ByKey(string key) => All[IndexOf(key)];
    }
}