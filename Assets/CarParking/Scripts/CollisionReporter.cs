using UnityEngine;

namespace CarParking
{
    /// <summary>Marks a static obstacle the player is penalised for touching.</summary>
    public class Obstacle : MonoBehaviour
    {
        public string label = "obstacle";
    }

    /// <summary>
    /// Sits on the player car and forwards real collisions to ParkingManager, with a cooldown so a
    /// single long scrape across a wall counts once rather than once per physics step.
    /// </summary>
    [RequireComponent(typeof(CarController))]
    public class CollisionReporter : MonoBehaviour
    {
        public float cooldown = 0.5f;
        float _last;

        void OnCollisionEnter(Collision c)
        {
            if (Time.time - _last < cooldown) return;

            // only obstacles count — the lot floor, kerbs you are allowed to clip and the bay
            // surface itself carry no Obstacle marker
            var ob = c.collider.GetComponentInParent<Obstacle>();
            if (ob == null) return;

            _last = Time.time;
            float impact = c.relativeVelocity.magnitude;
            if (ParkingManager.Instance) ParkingManager.Instance.OnCollision(ob, impact, c.GetContact(0).point);
        }
    }
}
