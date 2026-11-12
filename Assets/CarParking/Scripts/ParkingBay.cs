using UnityEngine;

namespace CarParking
{
    /// <summary>
    /// A marked bay. The bay is a rectangle lying on the ground whose local +Z runs along the car's
    /// length and local +X across it. It answers two questions: how far outside the bay the car is,
    /// and how square the car sits to the bay axis.
    /// </summary>
    public class ParkingBay : MonoBehaviour
    {
        [Tooltip("local X = width across the car, local Z = length along the car")]
        public Vector2 size = new Vector2(2.8f, 5.8f);

        public bool isTarget;

        /// <summary>Heading error beyond which the car is not considered square to the bay.</summary>
        public const float NeatToleranceDeg = 22f;

        /// <summary>How far outside the car's box may poke before the park is rejected.</summary>
        public const float EdgeTolerance = 0.10f;

        /// <summary>Metres the point sits outside the rectangle; 0 when inside.</summary>
        public float OutsideDistanceXZ(Vector3 world)
        {
            Vector3 l = transform.InverseTransformPoint(world);
            float dx = Mathf.Max(0f, Mathf.Abs(l.x) - size.x * 0.5f);
            float dz = Mathf.Max(0f, Mathf.Abs(l.z) - size.y * 0.5f);
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        public bool ContainsXZ(Vector3 world) => OutsideDistanceXZ(world) <= 0f;

        /// <summary>Degrees between the car's nose/tail and the bay's long axis, 0..90.</summary>
        public float HeadingErrorDeg(CarController car)
        {
            Vector3 a = car.transform.forward; a.y = 0f;
            Vector3 b = transform.forward; b.y = 0f;
            float ang = Vector3.Angle(a, b);
            return Mathf.Min(ang, 180f - ang);      // nose-in and tail-in both count
        }

        /// <summary>The worst corner overshoot in metres across the car's four footprint corners.</summary>
        public float WorstCornerOutside(CarController car, Vector3[] corners)
        {
            float worst = 0f;
            for (int i = 0; i < 4; i++)
                worst = Mathf.Max(worst, OutsideDistanceXZ(corners[i]));
            return worst;
        }

        public bool IsNeatlyParked(CarController car, Vector3[] corners)
        {
            return WorstCornerOutside(car, corners) <= EdgeTolerance
                && HeadingErrorDeg(car) <= NeatToleranceDeg;
        }

        /// <summary>0..1 — how squarely and how completely the car sits in the bay, for the HUD meter.</summary>
        public float Score(CarController car, Vector3[] corners, out float cornerScore, out float headingScore)
        {
            cornerScore = Mathf.Clamp01(1f - WorstCornerOutside(car, corners) / 1.5f);
            headingScore = Mathf.Clamp01(1f - HeadingErrorDeg(car) / 25f);
            return cornerScore * 0.6f + headingScore * 0.4f;
        }

        /// <summary>Distance from the car to the middle of the bay, in metres.</summary>
        public float DistanceToCentre(CarController car) =>
            Vector2.Distance(new Vector2(car.transform.position.x, car.transform.position.z),
                             new Vector2(transform.position.x, transform.position.z));
    }
}
