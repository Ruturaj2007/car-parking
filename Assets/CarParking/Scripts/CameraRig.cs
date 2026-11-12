using UnityEngine;

namespace CarParking
{
    /// <summary>
    /// Chase camera, plus an overhead mode for judging a bay. Chase sits behind and above and
    /// widens its FOV with speed; overhead looks straight down in fixed world axes, which is what
    /// makes it readable when you are lining the car up rather than driving it.
    /// </summary>
    public class CameraRig : MonoBehaviour
    {
        public enum Mode { Chase, Overhead }

        public Transform target;
        public Mode mode = Mode.Chase;
        public KeyCode toggleKey = KeyCode.C;

        [Header("Chase")]
        public float distance = 7.5f;
        public float height = 2.9f;
        public float lookHeight = 1.0f;
        public float positionDamping = 6f;
        public float rotationDamping = 8f;
        public float baseFov = 58f;
        public float maxFov = 70f;
        public float speedForMaxFov = 14f;

        [Header("Overhead")]
        public float overheadHeight = 26f;

        CarController _car;
        Camera _cam;

        public void SetMode(Mode m) => mode = m;

        void Awake()
        {
            _cam = GetComponent<Camera>();
            if (target) _car = target.GetComponent<CarController>();
        }

        void Update()
        {
            if (Input.GetKeyDown(toggleKey))
                mode = mode == Mode.Chase ? Mode.Overhead : Mode.Chase;
        }

        void LateUpdate()
        {
            if (!target) return;

            Vector3 desired;
            Quaternion look;
            float targetFov;

            if (mode == Mode.Overhead)
            {
                desired = target.position + Vector3.up * overheadHeight;
                look = Quaternion.Euler(90f, 0f, 0f);
                targetFov = baseFov;
            }
            else
            {
                Vector3 flatForward = target.forward;
                flatForward.y = 0f;
                if (flatForward.sqrMagnitude < 0.001f) flatForward = Vector3.forward;
                flatForward.Normalize();

                desired = target.position - flatForward * distance + Vector3.up * height;
                Vector3 lookAt = target.position + Vector3.up * lookHeight + flatForward * 2f;
                look = Quaternion.LookRotation(lookAt - desired, Vector3.up);

                float speed = _car ? Mathf.Abs(_car.SpeedMps) : 0f;
                targetFov = Mathf.Lerp(baseFov, maxFov, Mathf.Clamp01(speed / speedForMaxFov));
            }

            transform.position = Vector3.Lerp(transform.position, desired,
                                              1f - Mathf.Exp(-positionDamping * Time.deltaTime));
            transform.rotation = Quaternion.Slerp(transform.rotation, look,
                                                  1f - Mathf.Exp(-rotationDamping * Time.deltaTime));

            if (_cam)
                _cam.fieldOfView = Mathf.Lerp(_cam.fieldOfView, targetFov,
                                              1f - Mathf.Exp(-3f * Time.deltaTime));
        }
    }
}
