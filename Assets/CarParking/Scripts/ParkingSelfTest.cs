using System.Collections;
using System.Text;
using UnityEngine;

namespace CarParking
{
    /// <summary>
    /// Headless end-to-end verification of the parking rules, run against the shipped player.
    ///
    ///     CarParking.app/Contents/MacOS/CarParking -batchmode -nographics -selftest -logFile /tmp/selftest.log
    ///
    /// It drives the real <see cref="ParkingManager"/> through its real Update loop: load a lot, put the
    /// car in a known pose, let the game decide, then assert the verdict. Nothing here is a mock, so a
    /// pass means the rule the player ships with actually works.
    ///
    /// Does nothing at all unless -selftest is on the command line.
    /// </summary>
    public class ParkingSelfTest : MonoBehaviour
    {
        const string Flag = "-selftest";
        const float WatchdogSeconds = 180f;

        readonly StringBuilder _log = new StringBuilder();
        readonly Vector3[] _corners = new Vector3[4];
        int _pass, _fail;
        float _started;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void MaybeBoot()
        {
            foreach (var a in System.Environment.GetCommandLineArgs())
                if (a == Flag) { DontDestroyOnLoad(new GameObject("~ParkingSelfTest").AddComponent<ParkingSelfTest>().gameObject); return; }
        }

        IEnumerator Start()
        {
            _started = Time.realtimeSinceStartup;
            _log.AppendLine("=== CarParking self-test ===");

            // one frame so ParkingManager.Start() has already run LoadLevel(0)
            yield return null;

            var mgr = ParkingManager.Instance;
            if (mgr == null) { Fail("no ParkingManager in the scene"); yield return Finish(); yield break; }

            var car = mgr.car;
            var lot = mgr.lot;
            if (car == null || lot == null) { Fail("manager is not wired to a car/lot"); yield return Finish(); yield break; }

            // the briefing pause is presentation, not the rule under test — shorten it
            mgr.briefingSeconds = 0.2f;
            float hold = mgr.holdSeconds;
            _log.AppendLine(string.Format("levels={0}  holdSeconds={1:0.00}  maxHits={2}",
                                          mgr.LevelCount, hold, mgr.maxHits));

            // ---------------------------------------------------------------- A: every lot accepts a neat park
            for (int i = 0; i < mgr.LevelCount; i++)
            {
                if (Watchdog()) { yield return Finish(); yield break; }

                mgr.LoadLevel(i);
                yield return WaitFor(mgr, ParkingManager.Phase.Playing, 5f);

                var bay = lot.TargetBay;
                if (bay == null) { Fail("lot " + (i + 1) + ": no target bay was built"); continue; }

                PoseNeat(car, bay);
                yield return Wait(hold + 0.5f);

                string detail = string.Format("phase={0} align={1:0.00} cornerScore={2:0.00} heading={3:0}° worstCorner={4:0.00}m",
                                              mgr.phase, mgr.alignment, mgr.cornerScore, mgr.headingError,
                                              WorstCorner(car, bay));
                Check(mgr.phase == ParkingManager.Phase.Parked,
                      "lot " + (i + 1) + " '" + mgr.LevelName + "' accepts a neat park", detail);
            }

            // ---------------------------------------------------------------- B/C: the rule rejects near misses (lot 1)
            mgr.LoadLevel(0);
            yield return WaitFor(mgr, ParkingManager.Phase.Playing, 5f);
            var b = lot.TargetBay;

            // aligned but three metres to the side of the bay
            car.Teleport(b.transform.position + b.transform.right * 3f + Vector3.up * 0.35f, b.transform.rotation);
            yield return Wait(hold + 0.5f);
            Check(mgr.phase == ParkingManager.Phase.Playing,
                  "rejects a car 3 m outside the bay",
                  string.Format("phase={0} worstCorner={1:0.00}m heading={2:0}° (alignment is fine, only position fails)",
                                mgr.phase, WorstCorner(car, b), b.HeadingErrorDeg(car)));

            // in the bay but 60 degrees off the bay axis
            mgr.LoadLevel(0);
            yield return WaitFor(mgr, ParkingManager.Phase.Playing, 5f);
            b = lot.TargetBay;
            car.Teleport(b.transform.position + Vector3.up * 0.35f, b.transform.rotation * Quaternion.Euler(0f, 60f, 0f));
            yield return Wait(hold + 0.5f);
            Check(mgr.phase == ParkingManager.Phase.Playing,
                  "rejects a car 60° off the bay axis",
                  string.Format("phase={0} headingError={1:0}°", mgr.phase, b.HeadingErrorDeg(car)));

            // tail-in is as valid as nose-in
            mgr.LoadLevel(0);
            yield return WaitFor(mgr, ParkingManager.Phase.Playing, 5f);
            b = lot.TargetBay;
            car.Teleport(b.transform.position + Vector3.up * 0.35f, b.transform.rotation * Quaternion.Euler(0f, 180f, 0f));
            yield return Wait(hold + 0.5f);
            Check(mgr.phase == ParkingManager.Phase.Parked,
                  "accepts a tail-in park",
                  string.Format("phase={0} headingError={1:0}°", mgr.phase, b.HeadingErrorDeg(car)));

            // ---------------------------------------------------------------- D: scrapes fail the lot
            mgr.LoadLevel(0);
            yield return WaitFor(mgr, ParkingManager.Phase.Playing, 5f);
            Check(mgr.phase == ParkingManager.Phase.Playing, "lot starts in Playing after the briefing", "phase=" + mgr.phase);

            mgr.OnCollision(null, 5f, Vector3.zero);
            yield return null;
            Check(mgr.hits == 1 && mgr.phase == ParkingManager.Phase.Playing,
                  "one scrape is counted but does not fail the lot", "hits=" + mgr.hits + " phase=" + mgr.phase);

            mgr.OnCollision(null, 5f, Vector3.zero);
            mgr.OnCollision(null, 5f, Vector3.zero);
            yield return null;
            Check(mgr.phase == ParkingManager.Phase.Failed,
                  "three scrapes fail the lot", "hits=" + mgr.hits + " phase=" + mgr.phase);

            // ---------------------------------------------------------------- E: the clock fails the lot
            mgr.LoadLevel(0);
            yield return WaitFor(mgr, ParkingManager.Phase.Playing, 5f);
            if (mgr.phase == ParkingManager.Phase.Playing) { }
            yield return Wait(0.1f);
            var f = typeof(ParkingManager).GetField("timeLeft");
            if (f != null) f.SetValue(mgr, 0.05f);
            yield return Wait(0.5f);
            Check(mgr.phase == ParkingManager.Phase.Failed,
                  "running out of time fails the lot", "phase=" + mgr.phase);

            // ---------------------------------------------------------------- F: the engine actually pulls
            // Every check above moves the car with Teleport, which bypasses AddForce entirely — and
            // that is exactly how a real bug slipped through: the car has no wheels, so it slides on
            // a flat box collider, and ground friction decelerated it faster than the engine could
            // accelerate. The car sat pinned at full throttle while every check here still passed.
            {
                mgr.LoadLevel(0);
                yield return WaitFor(mgr, ParkingManager.Phase.Playing, 5f);

                var def = Levels.All[0];
                car.Teleport(new Vector3(def.spawnX, 0.35f, def.spawnZ), Quaternion.Euler(0f, def.spawnYaw, 0f));
                yield return Wait(1.0f);                 // let it settle onto the lot floor

                bool wasLocked = car.ControlsLocked;
                car.ControlsLocked = true;               // silence the controller so only the engine force acts
                float z0 = car.transform.position.z;
                const int steps = 25;                    // 0.5 s at the fixed step
                for (int i = 0; i < steps; i++)
                {
                    car.Body.WakeUp();
                    car.Body.AddForce(car.transform.forward * car.acceleration, ForceMode.Acceleration);
                    yield return new WaitForFixedUpdate();
                }
                float travelled = car.transform.position.z - z0;
                car.ControlsLocked = wasLocked;

                Check(travelled > 1.0f,
                      "the engine pulls the car away from rest on the lot floor (not pinned by friction)",
                      string.Format("moved {0:0.00} m in 0.50 s at acceleration {1:0.0} -> {2:0.00} m/s",
                                    travelled, car.acceleration, car.SpeedMps));
            }

            // ---------------------------------------------------------------- G: every lot is physically usable
            // A prop authored on the wrong axis is invisible to a coordinate check but shows up as a
            // wall through the target bay — lot 5 shipped with a 36 m kerb bisecting it. Overlap the
            // bay and the spawn and assert nothing solid is inside them.
            for (int i = 0; i < mgr.LevelCount; i++)
            {
                mgr.LoadLevel(i);
                yield return WaitFor(mgr, ParkingManager.Phase.Playing, 5f);

                var bay = lot.TargetBay;
                if (bay == null) continue;

                var half = new Vector3(bay.size.x * 0.5f - 0.05f, 0.6f, bay.size.y * 0.5f - 0.05f);
                var blocked = BlockersIn(bay.transform.position + Vector3.up * 0.4f, half, bay.transform.rotation);
                Check(blocked.Length == 0,
                      "lot " + (i + 1) + " '" + mgr.LevelName + "' target bay is clear of obstacles",
                      blocked.Length == 0 ? "nothing overlaps the bay" : "blocked by: " + blocked);

                var dl = Levels.All[i];
                var spawnHalf = new Vector3(0.90f - 0.05f, 0.6f, 2.05f - 0.05f);
                var spawnBlocked = BlockersIn(new Vector3(dl.spawnX, 0.4f, dl.spawnZ), spawnHalf,
                                              Quaternion.Euler(0f, dl.spawnYaw, 0f));
                Check(spawnBlocked.Length == 0,
                      "lot " + (i + 1) + " '" + mgr.LevelName + "' spawn is clear of obstacles",
                      spawnBlocked.Length == 0 ? "nothing overlaps the spawn" : "blocked by: " + spawnBlocked);
            }

            // ---------------------------------------------------------------- H: the bay is reachable
            // Clearance is invisible to a coordinate check. Lot 6 shipped with a 1.53 m aisle for a
            // 1.9 m car; every number in the level definition looked fine. Flood-fill the lot with a
            // car-width probe from the spawn to the bay and assert a path exists.
            {
                const float cell = 0.6f;
                const float carHalfWidth = 0.95f;         // half of the car's 1.9 m width
                for (int i = 0; i < mgr.LevelCount; i++)
                {
                    mgr.LoadLevel(i);
                    yield return WaitFor(mgr, ParkingManager.Phase.Playing, 5f);
                    var bay = lot.TargetBay;
                    if (bay == null) continue;

                    var dl = Levels.All[i];
                    float hx = dl.lotW * 0.5f - 0.35f, hz = dl.lotL * 0.5f - 0.35f;
                    int nx = Mathf.Max(2, Mathf.FloorToInt(hx * 2f / cell));
                    int nz = Mathf.Max(2, Mathf.FloorToInt(hz * 2f / cell));
                    var free = new bool[nx * nz];

                    for (int gx = 0; gx < nx; gx++)
                        for (int gz = 0; gz < nz; gz++)
                            free[gz * nx + gx] = IsClear(-hx + (gx + 0.5f) * cell,
                                                         -hz + (gz + 0.5f) * cell, carHalfWidth);

                    int sx = Mathf.Clamp(Mathf.FloorToInt((dl.spawnX + hx) / cell), 0, nx - 1);
                    int sz = Mathf.Clamp(Mathf.FloorToInt((dl.spawnZ + hz) / cell), 0, nz - 1);
                    int bx = Mathf.Clamp(Mathf.FloorToInt((bay.transform.position.x + hx) / cell), 0, nx - 1);
                    int bz = Mathf.Clamp(Mathf.FloorToInt((bay.transform.position.z + hz) / cell), 0, nz - 1);

                    bool reachable = FloodFilled(free, nx, nz, sz * nx + sx, bz * nx + bx);
                    Check(reachable,
                          "lot " + (i + 1) + " '" + mgr.LevelName + "' bay is reachable from the spawn",
                          reachable
                            ? string.Format("car-width path exists over a {0}x{1} grid of {2:0.0} m cells", nx, nz, cell)
                            : string.Format("NO PATH from spawn cell ({0},{1}) to bay cell ({2},{3}) with a {4:0.0} m probe",
                                            sx, sz, bx, bz, carHalfWidth * 2f));
                }
            }

            // ---------------------------------------------------------------- I: every car loads
            // A missing model, one still at 1/100 scale, or one whose stray helper mesh dwarfs the
            // body all look perfectly fine in the catalog and fail at runtime.
            {
                int startIndex = CarCatalog.IndexOf(mgr.CurrentCar != null ? mgr.CurrentCar.key : CarCatalog.All[0].key);

                for (int c = 0; c < CarCatalog.All.Length; c++)
                {
                    var spec = CarCatalog.All[c];
                    mgr.SelectCar(c);
                    yield return WaitFor(mgr, ParkingManager.Phase.Playing, 6f);

                    var model = car.transform.Find("Model");
                    if (model == null)
                    {
                        Check(false, "car '" + spec.key + "' loads a model", "no Model child under the car");
                        continue;
                    }

                    Bounds mb;
                    bool measured = CarFactory.TryMeasureLocal(model.gameObject, car.transform, out mb);
                    float wid = measured ? mb.size.x : 0f;
                    float len = measured ? mb.size.z : 0f;
                    int renderers = model.GetComponentsInChildren<Renderer>(true).Length;

                    Check(measured && len > 3.4f && len < 5.6f && wid > 1.4f && wid < 2.6f,
                          "car '" + spec.key + "' loads at a sane car size",
                          string.Format("{0:0.00} w x {1:0.00} l x {2:0.00} h m, {3} renderers",
                                        wid, len, measured ? mb.size.y : 0f, renderers));

                    Check(spec.footprint.x > 1.2f && spec.footprint.x < 2.6f &&
                          spec.footprint.y > 3.0f && spec.footprint.y < 5.6f,
                          "car '" + spec.key + "' derives a usable bay footprint",
                          string.Format("{0:0.00} x {1:0.00} m, collider {2:0.00} x {3:0.00} x {4:0.00}",
                                        spec.footprint.x, spec.footprint.y,
                                        spec.bodySize.x, spec.bodySize.y, spec.bodySize.z));
                }

                // leave the player on whatever they had chosen, not on the last entry tested
                mgr.SelectCar(startIndex);
            }

            // ---------------------------------------------------------------- J: it drives nose-first
            // Nothing above can catch a model facing the wrong way: every test moves the car with
            // physics, which does not care which end of the mesh is the nose. That is how a fully
            // backwards car passed the whole suite. The stock car names its wheels, so measure them:
            // the front pair must sit ahead of the rear pair along the axis the controller drives.
            {
                mgr.SelectCar(CarCatalog.IndexOf("stock"));
                yield return WaitFor(mgr, ParkingManager.Phase.Playing, 5f);

                var cc = car.GetComponent<CarController>();
                if (cc != null && cc.allWheels != null && cc.allWheels.Length >= 4 &&
                    cc.allWheels[0] != null && cc.allWheels[1] != null &&
                    cc.allWheels[2] != null && cc.allWheels[3] != null)
                {
                    float flZ = car.transform.InverseTransformPoint(cc.allWheels[0].position).z;
                    float frZ = car.transform.InverseTransformPoint(cc.allWheels[1].position).z;
                    float rlZ = car.transform.InverseTransformPoint(cc.allWheels[2].position).z;
                    float rrZ = car.transform.InverseTransformPoint(cc.allWheels[3].position).z;

                    Check(flZ > rlZ && frZ > rrZ,
                          "the car drives nose-first, not in reverse",
                          string.Format("front FL z={0:+0.00;-0.00} FR z={1:+0.00;-0.00} must be ahead of rear RL z={2:+0.00;-0.00} RR z={3:+0.00;-0.00}",
                                        flZ, frZ, rlZ, rrZ));
                }
                else
                {
                    Check(false, "the car drives nose-first, not in reverse",
                          "CarController has no named wheels to measure");
                }
            }

            yield return Finish();
        }

        /// <summary>True when a car-width probe centred here touches nothing but the lot floor.</summary>
        static bool IsClear(float x, float z, float halfWidth)
        {
            var hits = Physics.OverlapBox(new Vector3(x, 0.4f, z),
                                          new Vector3(halfWidth, 0.4f, halfWidth),
                                          Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
            foreach (var h in hits)
            {
                if (h == null) continue;
                if (h.name == "Ground") continue;                              // the one slab every lot shares
                if (h.GetComponentInParent<CarController>() != null) continue;  // the car we are steering
                return false;                                                  // walls, parked cars, cone, pillar, barrier
            }
            return true;
        }

        /// <summary>4-connected flood fill across the free cells.</summary>
        static bool FloodFilled(bool[] free, int nx, int nz, int start, int goal)
        {
            if (start < 0 || start >= free.Length || goal < 0 || goal >= free.Length) return false;
            if (!free[start] || !free[goal]) return false;

            var seen = new bool[free.Length];
            var queue = new System.Collections.Generic.Queue<int>();
            seen[start] = true;
            queue.Enqueue(start);

            while (queue.Count > 0)
            {
                int c = queue.Dequeue();
                if (c == goal) return true;
                int cx = c % nx, cz = c / nx;

                if (cx > 0) TryPush(free, seen, queue, c - 1);
                if (cx < nx - 1) TryPush(free, seen, queue, c + 1);
                if (cz > 0) TryPush(free, seen, queue, c - nx);
                if (cz < nz - 1) TryPush(free, seen, queue, c + nx);
            }
            return false;
        }

        static void TryPush(bool[] free, bool[] seen, System.Collections.Generic.Queue<int> q, int idx)
        {
            if (idx < 0 || idx >= free.Length) return;
            if (seen[idx] || !free[idx]) return;
            seen[idx] = true;
            q.Enqueue(idx);
        }

        /// <summary>Every Obstacle-marked collider overlapping the box, for the reachability checks.</summary>
        static string BlockersIn(Vector3 centre, Vector3 half, Quaternion rot)
        {
            var hits = Physics.OverlapBox(centre, half, rot, ~0, QueryTriggerInteraction.Ignore);
            var sb = new StringBuilder();
            foreach (var h in hits)
            {
                if (h == null) continue;
                if (h.GetComponentInParent<Obstacle>() == null) continue;
                if (sb.Length > 0) sb.Append(", ");
                sb.Append(h.name);
            }
            return sb.ToString();
        }

        // ------------------------------------------------------------------ helpers
        static void PoseNeat(CarController car, ParkingBay bay)
        {
            car.Teleport(bay.transform.position + Vector3.up * 0.35f, bay.transform.rotation);
        }

        /// <summary>
        /// The corner test needs the car's corners refilled first — reading <c>_corners</c> without
        /// calling FootprintCorners measures the world origin instead and reports a plausible-looking
        /// number that means nothing.
        /// </summary>
        float WorstCorner(CarController car, ParkingBay bay)
        {
            car.FootprintCorners(_corners);
            return bay.WorstCornerOutside(car, _corners);
        }

        IEnumerator WaitFor(ParkingManager mgr, ParkingManager.Phase phase, float timeout)
        {
            float t = 0f;
            while (mgr.phase != phase && t < timeout) { t += Time.deltaTime; yield return null; }
        }

        IEnumerator Wait(float seconds)
        {
            float t = 0f;
            while (t < seconds) { t += Time.deltaTime; yield return null; }
        }

        bool Watchdog()
        {
            if (Time.realtimeSinceStartup - _started < WatchdogSeconds) return false;
            Fail("watchdog: exceeded " + WatchdogSeconds + "s");
            return true;
        }

        void Check(bool ok, string what, string detail)
        {
            if (ok) _pass++; else _fail++;
            _log.AppendLine((ok ? "PASS  " : "FAIL  ") + what + "   [" + detail + "]");
        }

        void Fail(string what)
        {
            _fail++;
            _log.AppendLine("FAIL  " + what);
        }

        IEnumerator Finish()
        {
            _log.AppendLine("=== RESULT: " + (_fail == 0 ? "ALL PASS" : "FAILURES") +
                            " — " + _pass + " passed, " + _fail + " failed ===");
            Debug.Log(_log.ToString());
            yield return null;
            Application.Quit(_fail == 0 ? 0 : 1);
        }
    }
}
