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

            yield return Finish();
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
