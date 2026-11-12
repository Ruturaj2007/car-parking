using UnityEngine;

namespace CarParking
{
    /// <summary>
    /// Runs the challenge: builds a lot, times the attempt, counts scrapes against the scenery and
    /// decides when the car is actually parked. All the numbers the HUD shows are read-only
    /// properties here, so the HUD never has to guess at state.
    /// </summary>
    public class ParkingManager : MonoBehaviour
    {
        public static ParkingManager Instance { get; private set; }

        public enum Phase { Briefing, Playing, Parked, Failed, Complete }

        [Header("Scene wiring")]
        public CarController car;
        public LotBuilder lot;
        public CameraRig rig;

        [Header("Rules")]
        public int maxHits = 3;
        public float holdSeconds = 0.7f;         // the car must sit in the bay this long to count
        public float briefingSeconds = 2.5f;

        [Header("State (read-only)")]
        public int levelIndex;
        public Phase phase = Phase.Briefing;
        public float phaseTimer;
        public float timeLeft;
        public int hits;
        public float holdTimer;
        public float alignment;
        public float cornerScore;
        public float headingScore;
        public float headingError;
        public float distanceToBay;
        public string centreMessage = "";
        public float messageTimer;
        public float hitFlashUntil;
        public float lastResultTime = -1f;
        public float totalTime;
        public float[] bestTimes;

        LevelDef _def;
        readonly Vector3[] _corners = new Vector3[4];
        const string BestKeyPrefix = "CarParking.Best.";

        public LevelDef CurrentLevel => _def;
        public int LevelCount => Levels.All.Length;
        public string LevelName => _def != null ? _def.name : "";
        public string LevelHint => _def != null ? _def.hint : "";
        public float TimeLimit => _def != null ? _def.timeLimit : 60f;
        public float BestTime => bestTimes != null && levelIndex < bestTimes.Length ? bestTimes[levelIndex] : -1f;
        public bool IsHitFlashing => Time.time < hitFlashUntil;

        void Awake()
        {
            Instance = this;
            bestTimes = new float[Levels.All.Length];
            for (int i = 0; i < bestTimes.Length; i++)
                bestTimes[i] = PlayerPrefs.GetFloat(BestKeyPrefix + i, -1f);
        }

        void Start()
        {
            LoadLevel(0);
        }

        void Update()
        {
            switch (phase)
            {
                case Phase.Briefing:
                    phaseTimer -= Time.deltaTime;
                    if (phaseTimer <= 0f) StartAttempt();
                    break;

                case Phase.Playing:
                    TickPlaying();
                    break;
            }

            if (messageTimer > 0f)
            {
                messageTimer -= Time.deltaTime;
                if (messageTimer <= 0f) centreMessage = "";
            }

            HandleKeys();
        }

        void HandleKeys()
        {
            if (Input.GetKeyDown(KeyCode.R)) LoadLevel(levelIndex);

            if (phase == Phase.Parked && (Input.GetKeyDown(KeyCode.N) || Input.GetKeyDown(KeyCode.Return)))
            {
                if (levelIndex + 1 < LevelCount) LoadLevel(levelIndex + 1);
                else { phase = Phase.Complete; SetMessage("ALL SIX LOTS CLEARED", 999f); }
            }

            // number keys jump straight to a lot — handy when demonstrating
            for (int i = 0; i < LevelCount && i < 9; i++)
                if (Input.GetKeyDown(KeyCode.Alpha1 + i)) LoadLevel(i);
        }

        // ------------------------------------------------------------------ flow
        public void LoadLevel(int index)
        {
            levelIndex = Mathf.Clamp(index, 0, LevelCount - 1);
            _def = Levels.All[levelIndex];

            lot.Build(_def);

            var spawn = new Vector3(_def.spawnX, 0.35f, _def.spawnZ);
            car.Teleport(spawn, Quaternion.Euler(0f, _def.spawnYaw, 0f));
            car.ControlsLocked = true;
            car.Freeze();

            if (rig) rig.transform.position = spawn + Vector3.up * 6f;

            phase = Phase.Briefing;
            phaseTimer = briefingSeconds;
            timeLeft = _def.timeLimit;
            hits = 0;
            holdTimer = 0f;
            alignment = 0f;
            cornerScore = 0f;
            headingScore = 0f;
            headingError = 0f;
            lastResultTime = -1f;

            SetMessage("LOT " + (levelIndex + 1) + " — " + _def.name.ToUpperInvariant(), briefingSeconds);

            Debug.Log(string.Format("[Parking] lot {0} '{1}' built — target bay {2}",
                      levelIndex + 1, _def.name, lot.TargetBay ? lot.TargetBay.transform.position.ToString() : "MISSING"));
        }

        void StartAttempt()
        {
            phase = Phase.Playing;
            car.ControlsLocked = false;
            SetMessage("GO", 0.8f);
        }

        void TickPlaying()
        {
            timeLeft -= Time.deltaTime;
            if (timeLeft <= 0f)
            {
                timeLeft = 0f;
                Fail("OUT OF TIME");
                return;
            }

            EvaluateBay();

            if (lot.TargetBay != null && lot.TargetBay.IsNeatlyParked(car, _corners))
            {
                holdTimer += Time.deltaTime;
                if (holdTimer >= holdSeconds)
                {
                    Succeed();
                    return;
                }
            }
            else
            {
                holdTimer = 0f;
            }

            // a nudge out of the bay while the "hold" is running must show on the meter
            if (holdTimer > 0f)
                SetMessage("HOLD…", 0.3f);
        }

        void EvaluateBay()
        {
            var bay = lot.TargetBay;
            if (bay == null) return;

            car.FootprintCorners(_corners);
            alignment = bay.Score(car, _corners, out cornerScore, out headingScore);
            headingError = bay.HeadingErrorDeg(car);
            distanceToBay = bay.DistanceToCentre(car);
        }

        void Succeed()
        {
            phase = Phase.Parked;
            car.ControlsLocked = true;
            car.Freeze();

            lastResultTime = _def.timeLimit - timeLeft;
            totalTime += lastResultTime;
            if (bestTimes[levelIndex] < 0f || lastResultTime < bestTimes[levelIndex])
            {
                bestTimes[levelIndex] = lastResultTime;
                PlayerPrefs.SetFloat(BestKeyPrefix + levelIndex, lastResultTime);
                PlayerPrefs.Save();
            }

            bool last = levelIndex + 1 >= LevelCount;
            SetMessage("PARKED — " + Format(lastResultTime), 999f);
            Debug.Log(string.Format("[Parking] lot {0} parked in {1} with {2} scrape(s)",
                      levelIndex + 1, Format(lastResultTime), hits));
        }

        void Fail(string why)
        {
            phase = Phase.Failed;
            car.ControlsLocked = true;
            car.Freeze();
            SetMessage(why + " — press  R", 999f);
            Debug.Log("[Parking] lot " + (levelIndex + 1) + " failed: " + why);
        }

        public void OnCollision(Obstacle ob, float impact, Vector3 point)
        {
            if (phase != Phase.Playing) return;
            if (impact < 0.6f) return;                  // brushing the scenery does not count

            hits++;
            hitFlashUntil = Time.time + 0.35f;
            Debug.Log(string.Format("[Parking] scrape {0}/{1} on {2} at {3} (impact {4:0.0})",
                      hits, maxHits, ob != null ? ob.label : "?", point.ToString("F1"), impact));

            if (hits >= maxHits) Fail("TOO MANY SCRAPES");
            else SetMessage("SCRAPE " + hits + "/" + maxHits, 1.1f);
        }

        void SetMessage(string text, float seconds)
        {
            centreMessage = text;
            messageTimer = seconds;
        }

        public static string Format(float seconds)
        {
            if (seconds < 0f) return "--:--.--";
            int m = Mathf.FloorToInt(seconds / 60f);
            float s = seconds - m * 60f;
            return string.Format("{0:0}:{1:00.00}", m, s);
        }
    }
}
