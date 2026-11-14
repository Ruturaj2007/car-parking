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
        public const string StockResource = "car";
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

        public static Material LoadMaterial(string name) => Resources.Load<Material>(MaterialFolder + name);

        static PhysicsMaterial _gripFree;

        /// <summary>
        /// The car has no wheels — it is a flat box that has to *slide*. That makes the lot floor's
        /// friction fight the engine, and it loses: measured deceleration from ground contact is
        /// about 11.8 m/s2, more than the engine's acceleration, so the car sits pinned at full
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

        // ------------------------------------------------------------------ the stock car
        /// <summary>Instantiate the hand-built car mesh under <paramref name="parent"/> and paint it.</summary>
        public static GameObject SpawnModel(Transform parent, Color? paintOverride = null)
        {
            var prefab = Resources.Load<GameObject>(StockResource);
            if (prefab == null)
            {
                Debug.LogError("[CarFactory] Resources/" + StockResource + ".fbx is missing");
                return null;
            }

            var model = UnityEngine.Object.Instantiate(prefab, parent, false);
            model.name = "Model";
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;

            // The Blender car faces +Y, which lands on -Z after import, while the controller drives the
            // parent's +Z — so it must be spun round. Measure the front wheel relative to the MODEL
            // root: its own localPosition is relative to its parent, which carries the wheel's rig, and
            // read that way the test never fired.
            var probe = FindDeep(model.transform, "WHEEL_FR") ?? FindDeep(model.transform, "WHEEL_FL");
            if (probe != null && model.transform.InverseTransformPoint(probe.position).z < 0f)
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

        // ------------------------------------------------------------------ fitting a downloaded model
        /// <summary>
        /// Bounds of everything under <paramref name="go"/>, expressed in <paramref name="space"/>'s
        /// local axes. Measuring in world space instead would make the result depend on where the car
        /// happens to be spawned, which breaks swapping a car mid-level.
        /// </summary>
        public static bool TryMeasureLocal(GameObject go, Transform space, out Bounds b)
        {
            b = new Bounds();
            bool any = false;

            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null || !r.enabled) continue;
                var wb = r.bounds;
                var e = wb.extents;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = wb.center + new Vector3(
                        (i & 1) == 0 ? -e.x : e.x,
                        (i & 2) == 0 ? -e.y : e.y,
                        (i & 4) == 0 ? -e.z : e.z);
                    Vector3 p = space.InverseTransformPoint(corner);
                    if (!any) { b = new Bounds(p, Vector3.zero); any = true; }
                    else b.Encapsulate(p);
                }
            }
            return any;
        }

        /// <summary>
        /// Instantiate a spec's model under <paramref name="root"/> and normalise it: long horizontal
        /// axis onto Z, scaled to a real car length, centred over its own footprint, sitting on y=0.
        /// The downloaded models arrive at scales from metres to 1/100 and with arbitrary pivots, so
        /// this is measured rather than hand-tuned — and it re-derives the collider and bay-test
        /// footprint from the same numbers, so a bigger car genuinely needs a bigger bay.
        /// </summary>
        public static GameObject SpawnFitted(Transform root, CarSpec spec)
        {
            var prefab = Resources.Load<GameObject>(spec.modelResource);
            if (prefab == null)
            {
                Debug.LogError("[CarFactory] missing model at Resources/" + spec.modelResource);
                return null;
            }

            var wrapper = new GameObject("Model");
            wrapper.transform.SetParent(root, false);
            wrapper.transform.localPosition = Vector3.zero;
            wrapper.transform.localRotation = Quaternion.identity;
            wrapper.transform.localScale = Vector3.one;

            var inner = UnityEngine.Object.Instantiate(prefab, wrapper.transform, false);
            inner.name = "Mesh";
            inner.transform.localPosition = Vector3.zero;
            inner.transform.localRotation = Quaternion.identity;
            inner.transform.localScale = Vector3.one;

            // Orientation first: everything measured below must be the finished silhouette. Applying
            // it afterwards would centre a car and then rotate it through the floor.
            if (spec.modelRotationDeg != Vector3.zero)
                wrapper.transform.localRotation = Quaternion.Euler(spec.modelRotationDeg);

            if (spec.autoFit)
            {
                Bounds b;
                if (TryMeasureLocal(wrapper, root, out b))
                {
                    // longest horizontal axis onto Z
                    if (b.size.x > b.size.z * 1.05f)
                        wrapper.transform.localRotation *= Quaternion.Euler(0f, 90f, 0f);

                    // scale to a real car length
                    TryMeasureLocal(wrapper, root, out b);
                    float length = Mathf.Max(0.001f, b.size.z);
                    wrapper.transform.localScale = Vector3.one * (spec.targetLength / length);

                    // centre over the footprint and sit on the ground
                    TryMeasureLocal(wrapper, root, out b);
                    wrapper.transform.localPosition -= new Vector3(b.center.x, b.min.y, b.center.z);

                    // the collider and the bay test follow the measured body
                    TryMeasureLocal(wrapper, root, out b);
                    spec.bodySize = new Vector3(b.size.x * 0.92f, b.size.y * 0.95f, b.size.z * 0.95f);
                    spec.bodyCentreY = spec.bodySize.y * 0.5f;
                    spec.footprint = new Vector2(b.size.x * 0.90f, b.size.z * 0.92f);
                }
                else
                {
                    Debug.LogWarning("[CarFactory] '" + spec.key + "' has no enabled renderers; using authored sizes");
                }
            }

            if (spec.remapMaterials) ApplyMaterials(wrapper, null);
            return wrapper;
        }

        public static void ApplyHandling(CarController cc, CarSpec spec)
        {
            if (cc == null) return;
            cc.acceleration = spec.acceleration;
            cc.maxSpeed = spec.maxSpeed;
            cc.steerRateDegPerSec = spec.steerRateDegPerSec;
            cc.lateralGrip = spec.lateralGrip;
            cc.footprintSize = spec.footprint;
            cc.maxReverse = Mathf.Clamp(spec.maxSpeed * 0.6f, 6f, 9f);
        }

        static void WireWheels(CarController cc, GameObject model)
        {
            if (cc == null) return;
            if (model == null) { cc.allWheels = null; cc.frontWheels = null; return; }

            var fl = FindDeep(model.transform, "WHEEL_FL");
            var fr = FindDeep(model.transform, "WHEEL_FR");
            var rl = FindDeep(model.transform, "WHEEL_RL");
            var rr = FindDeep(model.transform, "WHEEL_RR");

            // downloaded models have no wheel nodes; leave them null and the spin simply stops
            if (fl == null || fr == null || rl == null || rr == null)
            {
                cc.allWheels = null;
                cc.frontWheels = null;
                return;
            }
            cc.allWheels = new[] { fl, fr, rl, rr };
            cc.frontWheels = new[] { fl, fr };
        }

        // ------------------------------------------------------------------ vehicles
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
        public static GameObject CreatePlayerCar(Transform parent, Vector3 pos, Quaternion rot, CarSpec spec)
        {
            if (spec == null) spec = CarCatalog.All[0];

            var root = new GameObject("Car");

            var rb = root.AddComponent<Rigidbody>();
            rb.mass = spec.mass;
            rb.linearDamping = 0.02f;
            rb.angularDamping = 2.5f;

            var box = root.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, spec.bodyCentreY, 0f);
            box.size = spec.bodySize;
            box.material = GripFree();      // see GripFree(): otherwise ground friction pins the car

            var cc = root.AddComponent<CarController>();
            ApplyHandling(cc, spec);
            root.AddComponent<CollisionReporter>();

            var model = SpawnFitted(root.transform, spec);

            // auto-fit measures the body, so the collider is set from the result, not the authored guess
            box.center = new Vector3(0f, spec.bodyCentreY, 0f);
            box.size = spec.bodySize;

            WireWheels(cc, model);

            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(pos, rot);
            return root;
        }

        /// <summary>Swap the driving car onto another catalog entry, in place.</summary>
        public static void ApplySpec(GameObject carRoot, CarSpec spec)
        {
            if (carRoot == null || spec == null) return;

            // rename before Destroy: Destroy is deferred, so two objects called "Model" would
            // otherwise exist for a frame and Find("Model") would return the dying one
            var old = carRoot.transform.Find("Model");
            if (old != null)
            {
                old.name = "Model_retired";
                if (Application.isPlaying) UnityEngine.Object.Destroy(old.gameObject);
                else UnityEngine.Object.DestroyImmediate(old.gameObject);
            }

            var model = SpawnFitted(carRoot.transform, spec);

            var box = carRoot.GetComponent<BoxCollider>();
            if (box != null)
            {
                box.center = new Vector3(0f, spec.bodyCentreY, 0f);
                box.size = spec.bodySize;
            }

            var rb = carRoot.GetComponent<Rigidbody>();
            if (rb != null) rb.mass = spec.mass;

            var cc = carRoot.GetComponent<CarController>();
            if (cc != null)
            {
                ApplyHandling(cc, spec);
                WireWheels(cc, model);
            }
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