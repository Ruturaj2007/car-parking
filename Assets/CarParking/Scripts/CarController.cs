using UnityEngine;

namespace CarParking
{
    /// <summary>
    /// Arcade car controller tuned for slow, precise parking manoeuvres. Physics-driven, but the
    /// rotation is locked on X/Z so the car cannot roll over and a strong lateral grip term makes
    /// it corner instead of sliding. Same model as car-game, retuned for a low speed range.
    /// Input: legacy Input Manager axes (Vertical/Horizontal), Space = handbrake.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class CarController : MonoBehaviour
    {
        [Header("Engine")]
        public float maxSpeed = 13f;            // m/s (~47 km/h) — a car park, not a circuit
        public float maxReverse = 8f;
        public float acceleration = 8.5f;       // m/s^2 at full throttle
        public float brakeDeceleration = 20f;
        public float coastDamping = 1.1f;       // lifts off -> scrubs off quickly

        [Header("Steering")]
        public float steerRateDegPerSec = 92f;
        public float steerFalloffSpeed = 8f;    // authority tapers above this
        public float lateralGrip = 14f;

        [Header("Wheels (visual)")]
        public Transform[] frontWheels;
        public Transform[] allWheels;
        public float wheelRadius = 0.335f;
        public float maxSteerVisualAngle = 32f;

        [Header("Footprint (the box the bay test uses)")]
        public Vector2 footprintSize = new Vector2(1.80f, 4.10f);   // x = width, z = length

        [Header("State (read-only)")]
        public bool ControlsLocked;

        Rigidbody _rb;
        float _throttle, _steer;
        bool _handbrake;

        public Rigidbody Body => _rb;
        public float SpeedMps => _rb ? Vector3.Dot(_rb.velocity, transform.forward) : 0f;
        public float SpeedKph => Mathf.Abs(SpeedMps) * 3.6f;
        public float SteerInput => _steer;
        public bool Reversing => SpeedMps < -0.3f;

        void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            _rb.centerOfMass = new Vector3(0f, -0.25f, 0f);
            _rb.interpolation = RigidbodyInterpolation.Interpolate;
            _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        }

        void Update()
        {
            if (ControlsLocked) { _throttle = 0f; _steer = 0f; _handbrake = false; }
            else
            {
                _throttle = Input.GetAxisRaw("Vertical");
                _steer = Input.GetAxisRaw("Horizontal");
                _handbrake = Input.GetKey(KeyCode.Space);
            }
            AnimateWheels();
        }

        void FixedUpdate()
        {
            if (ControlsLocked) return;

            Vector3 forward = transform.forward; forward.y = 0f; forward.Normalize();
            Vector3 right = transform.right; right.y = 0f; right.Normalize();
            float speed = Vector3.Dot(_rb.velocity, forward);

            // ---- longitudinal: throttle, brake, reverse, coast ----
            if (_throttle > 0.01f)
            {
                if (speed < maxSpeed)
                    _rb.AddForce(forward * (acceleration * _throttle), ForceMode.Acceleration);
            }
            else if (_throttle < -0.01f)
            {
                if (speed > 0.5f)                                     // rolling forward: brake
                    _rb.AddForce(-forward * (brakeDeceleration * -_throttle), ForceMode.Acceleration);
                else if (speed > -maxReverse)                         // stopped: reverse
                    _rb.AddForce(forward * (acceleration * 0.6f * _throttle), ForceMode.Acceleration);
            }
            else if (Mathf.Abs(speed) > 0.05f)                        // coast
            {
                _rb.AddForce(-forward * (speed * coastDamping), ForceMode.Acceleration);
            }

            if (_handbrake && Mathf.Abs(speed) > 0.4f)
                _rb.AddForce(-forward * (Mathf.Sign(speed) * brakeDeceleration * 0.8f), ForceMode.Acceleration);

            // ---- lateral grip: kill sideways velocity so it corners instead of drifting ----
            float lateral = Vector3.Dot(_rb.velocity, right);
            _rb.AddForce(-right * (lateral * lateralGrip), ForceMode.Acceleration);

            // ---- steering as a speed-scaled yaw; authority arrives almost immediately so
            //      the car can be placed at walking pace ----
            float speedAbs = Mathf.Abs(speed);
            float authority = Mathf.Clamp01(speedAbs / 1.2f) *
                              (1f - Mathf.Clamp01(speedAbs / steerFalloffSpeed) * 0.5f);
            float yaw = _steer * steerRateDegPerSec * authority * Time.fixedDeltaTime;
            if (speedAbs > 0.1f) _rb.MoveRotation(_rb.rotation * Quaternion.Euler(0f, yaw, 0f));
        }

        void AnimateWheels()
        {
            float travelled = SpeedMps * Time.deltaTime;

            if (allWheels != null)
                for (int i = 0; i < allWheels.Length; i++)
                    if (allWheels[i])
                        allWheels[i].Rotate(transform.right,
                                            (travelled / Mathf.Max(0.01f, wheelRadius)) * Mathf.Rad2Deg,
                                            Space.World);

            if (frontWheels != null)
                foreach (var w in frontWheels)
                    if (w) w.localRotation = Quaternion.Euler(0f, _steer * maxSteerVisualAngle, 0f);
        }

        /// <summary>The four ground-plane corners of the car body, world space, into <paramref name="buffer"/>.</summary>
        public void FootprintCorners(Vector3[] buffer)
        {
            Vector3 c = transform.position;
            Vector3 right = transform.right * (footprintSize.x * 0.5f);
            Vector3 fwd = transform.forward * (footprintSize.y * 0.5f);

            buffer[0] = c - right - fwd;    // rear left
            buffer[1] = c + right - fwd;    // rear right
            buffer[2] = c + right + fwd;    // front right
            buffer[3] = c - right + fwd;    // front left
        }

        public void Teleport(Vector3 position, Quaternion rotation)
        {
            _rb.velocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
            transform.SetPositionAndRotation(position, rotation);
            _rb.position = position;
            _rb.rotation = rotation;
        }

        public void Freeze() { _rb.velocity = Vector3.zero; _rb.angularVelocity = Vector3.zero; }
    }
}
