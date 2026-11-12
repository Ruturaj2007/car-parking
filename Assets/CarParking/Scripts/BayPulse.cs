using UnityEngine;

namespace CarParking
{
    /// <summary>Pulses the emission on the target bay's glow so it reads as "park here".</summary>
    public class BayPulse : MonoBehaviour
    {
        public Renderer rend;
        public Color colour = new Color(0.15f, 0.85f, 1f);
        public float speed = 2.2f;
        public float low = 0.35f;
        public float high = 1.25f;

        MaterialPropertyBlock _mpb;
        static readonly int Emission = Shader.PropertyToID("_EmissionColor");

        void Update()
        {
            if (rend == null) return;

            if (Time.timeScale <= 0f) return;
            float k = Mathf.Lerp(low, high, 0.5f + 0.5f * Mathf.Sin(Time.time * speed));

            if (_mpb == null) _mpb = new MaterialPropertyBlock();
            rend.GetPropertyBlock(_mpb);
            _mpb.SetColor(Emission, colour * k);
            rend.SetPropertyBlock(_mpb);
        }
    }
}
