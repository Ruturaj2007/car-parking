using UnityEngine;

namespace CarParking
{
    /// <summary>
    /// Builds and paints cars. Everything goes through Resources so the exact same code path runs
    /// in the editor builder and in a built player — a parked car therefore looks identical to the
    /// one the player drives.
    /// </summary>
    public static class CarFactory
    {
        public const string ModelResource = "car";
        public const string MaterialFolder = "Materials/";

        const float BodyCentreY = 0.70f;
        static readonly Vector3 BodySize = new Vector3(1.90f, 1.15f, 4.35f);

        struct Map { public string key, mat; public Map(string k, string m) { key = k; mat = m; } }

        static readonly Map[] MaterialMap =
        {
            new Map("paint",      "CarPaint"),
            new Map("glass",      "Glass"),
            new Map("rubber",     "Rubber"),
            new Map("alloy",      "Alloy"),
            new Map("rim",        "Alloy"),
            new Map("brake",      "BrakeDisc"),
            new Map("liner",      "Liner"),
            new Map("lens-rear",  "LensRear"),
            new Map("lens_rear",  "LensRear"),
            new Map("lens",       "LensFront"),
            new Map("interior",   "Interior"),
            new Map("cabin",      "Interior"),
            new Map("trim",       "Trim"),
        };

        public static GameObject LoadModel() => Resources.Load<GameObject>(ModelResource);

        public static Material LoadMaterial(string name) => Resources.Load<Material>(MaterialFolder + name);

        static PhysicsMaterial _gripFree;

        /// <summary>
        /// The car has no wheels — it is a flat box that has to *slide*. That makes the lot floor's
        /// friction fight the engine, and it loses: measured deceleration from ground contact is
        /// about 11.8 m/s², more than the engine's acceleration, so the car sits pinned at full
        /// throttle. A frictionless material on the car's collider lets the controller be the only
        /// thing deciding how it moves, which is what the lateral-grip and coast terms already assume.
        /// </summary>
        static PhysicsMaterial GripFree()
        {
            if (_gripFree == null)
            {
                _gripFree = new PhysicsMaterial("CarGrip");
                _gripFree.dynamicFriction = 0f;
                _gripFree.staticFriction = 0f;
                _gripFree.bounciness = 0f;
                _gripFree.frictionCombine = PhysicsMaterialCombine.Minimum;
            }
            return _gripFree;
        }

        /// <summary>Instantiate the car mesh under <paramref name="parent"/>, orient it, paint it.</summary>
        public static GameObject SpawnModel(Transform parent, Color? paintOverride = null)
        {
            var prefab = LoadModel();
            if (prefab == null)
            {
                Debug.LogError("[CarFactory] Resources/" + ModelResource + ".fbx is missing");
                return null;
            }

            var model = UnityEngine.Object.Instantiate(prefab, parent, false);
            model.name = "Model";
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;

            // the Blender car faces +Y, which lands on -Z after import, while the controller drives
            // the parent's +Z. If a front wheel sits behind the origin, spin the model round.
            var probe = FindDeep(model.transform, "WHEEL_FR") ?? FindDeep(model.transform, "WHEEL_FL");
            if (probe != null && probe.localPosition.z < 0f)
                model.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

            ApplyMaterials(model, paintOverride);
            return model;
        }

        public static void ApplyMaterials(GameObject model, Color? paintOverride)
        {
            foreach (var r in model.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    string n = mats[i] != null ? mats[i].name.ToLowerInvariant() : "";
                    string target = null;
                    for (int m = 0; m < MaterialMap.Length; m++)
                        if (n.Contains(MaterialMap[m].key)) { target = MaterialMap[m].mat; break; }
                    if (target == null) target = "Trim";

                    var loaded = LoadMaterial(target);
                    if (loaded == null) continue;

                    // only the body paint is per-instance; everything else shares the asset
                    if (target == "CarPaint" && paintOverride.HasValue)
                        mats[i] = new Material(loaded) { color = paintOverride.Value };
                    else
                        mats[i] = loaded;
                }
                r.sharedMaterials = mats;
            }
        }

        /// <summary>A stationary car that blocks the player: model + one box collider + an Obstacle marker.</summary>
        public static GameObject CreateParkedCar(Transform parent, Vector3 pos, float yaw, Color paint)
        {
            var root = new GameObject("ParkedCar");
            root.transform.SetParent(parent, false);
            root.transform.position = pos;
            root.transform.rotation = Quaternion.Euler(0f, yaw, 0f);

            var box = root.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, BodyCentreY, 0f);
            box.size = BodySize;
            root.AddComponent<Obstacle>().label = "parked car";

            SpawnModel(root.transform, paint);
            return root;
        }

        /// <summary>The car the player drives: rigidbody + box collider + CarController + its wheels wired.</summary>
        public static GameObject CreatePlayerCar(Transform parent, Vector3 pos, Quaternion rot, Color? paint = null)
        {
            var root = new GameObject("Car");
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(pos, rot);

            var rb = root.AddComponent<Rigidbody>();
            rb.mass = 1200f;
            rb.linearDamping = 0.02f;
            rb.angularDamping = 2.5f;

            var box = root.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, BodyCentreY, 0f);
            box.size = BodySize;
            box.material = GripFree();      // see GripFree(): otherwise ground friction pins the car

            var cc = root.AddComponent<CarController>();
            root.AddComponent<CollisionReporter>();

            var model = SpawnModel(root.transform, paint);
            if (model != null)
            {
                var fl = FindDeep(model.transform, "WHEEL_FL");
                var fr = FindDeep(model.transform, "WHEEL_FR");
                var rl = FindDeep(model.transform, "WHEEL_RL");
                var rr = FindDeep(model.transform, "WHEEL_RR");
                cc.allWheels = new[] { fl, fr, rl, rr };
                cc.frontWheels = new[] { fl, fr };
                if (fl == null || fr == null || rl == null || rr == null)
                    Debug.LogWarning("[CarFactory] expected 4 wheel nodes (WHEEL_FL/FR/RL/RR); wheels will not spin");
            }
            return root;
        }

        public static Transform FindDeep(Transform root, string name)
        {
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                var hit = FindDeep(root.GetChild(i), name);
                if (hit != null) return hit;
            }
            return null;
        }
    }
}
