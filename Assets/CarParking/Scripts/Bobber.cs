using UnityEngine;

namespace CarParking
{
    /// <summary>Gentle vertical bob, used to make the target bay's beam catch the eye.</summary>
    public class Bobber : MonoBehaviour
    {
        public float amplitude = 0.3f;
        public float speed = 1.2f;

        Vector3 _base;

        void Start() { _base = transform.localPosition; }

        void Update()
        {
            transform.localPosition = _base + Vector3.up * (Mathf.Sin(Time.time * speed) * amplitude);
        }
    }
}
