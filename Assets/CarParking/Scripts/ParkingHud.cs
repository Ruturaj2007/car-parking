using UnityEngine;

namespace CarParking
{
    /// <summary>
    /// IMGUI HUD — no Canvas, no packages, so the scene stays a handful of objects. Shows the
    /// countdown, the scrape tally, a live alignment meter (corner fit + how square the car is),
    /// and an arrow pointing at the target bay.
    /// </summary>
    public class ParkingHud : MonoBehaviour
    {
        public CarController car;
        public ParkingManager game;

        GUIStyle _label, _big, _mid, _small, _tiny;
        Texture2D _white, _arrow;

        void EnsureStyles()
        {
            if (_white == null)
            {
                _white = new Texture2D(1, 1);
                _white.SetPixel(0, 0, Color.white);
                _white.Apply();
            }
            if (_arrow == null) _arrow = ArrowTexture(64);

            if (_label == null)
            {
                _label = new GUIStyle(GUI.skin.label) { fontSize = 21, fontStyle = FontStyle.Bold };
                _label.normal.textColor = Color.white;

                _mid = new GUIStyle(GUI.skin.label) { fontSize = 26, fontStyle = FontStyle.Bold };
                _mid.normal.textColor = Color.white;

                _big = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 60,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };
                _big.normal.textColor = Color.white;

                _small = new GUIStyle(GUI.skin.label) { fontSize = 15 };
                _small.normal.textColor = new Color(1f, 1f, 1f, 0.78f);

                _tiny = new GUIStyle(GUI.skin.label) { fontSize = 13 };
                _tiny.normal.textColor = new Color(1f, 1f, 1f, 0.55f);
            }
        }

        static Texture2D ArrowTexture(int size)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var clear = new Color(0f, 0f, 0f, 0f);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float nx = (x + 0.5f) / size * 2f - 1f;          // -1..1
                    float ny = (y + 0.5f) / size * 2f - 1f;
                    // a triangle pointing "up" (i.e. -y in texture space is the tip)
                    bool inTri = Mathf.Abs(nx) <= (0.5f - ny * 0.5f) * 0.85f && ny > -0.85f;
                    t.SetPixel(x, y, inTri ? Color.white : clear);
                }
            t.Apply();
            return t;
        }

        void OnGUI()
        {
            EnsureStyles();
            if (!car || !game) return;

            DrawStatusPanel();
            DrawAlignmentPanel();
            DrawSpeed();
            DrawCentreMessage();
            DrawHint();
            DrawHitFlash();
        }

        // ------------------------------------------------------------------ panels
        void DrawStatusPanel()
        {
            var panel = new Rect(18, 18, 348, 196);
            Fill(panel, new Color(0f, 0f, 0f, 0.48f));

            GUI.Label(new Rect(32, 26, 322, 28),
                      "LOT " + (game.levelIndex + 1) + "/" + game.LevelCount + "   " + game.LevelName.ToUpperInvariant(),
                      _label);

            // countdown turns amber then red as it runs out
            float t = game.timeLeft;
            var timeColour = t > 20f ? Color.white : (t > 10f ? new Color(1f, 0.82f, 0.35f) : new Color(1f, 0.38f, 0.34f));
            GUI.color = timeColour;
            GUI.Label(new Rect(32, 56, 322, 30), "TIME  " + ParkingManager.Format(Mathf.Max(0f, t)), _mid);
            GUI.color = Color.white;

            GUI.Label(new Rect(32, 90, 322, 24), "SCRAPES  " + game.hits + " / " + game.maxHits, _small);
            GUI.Label(new Rect(32, 112, 322, 24), "BEST     " + ParkingManager.Format(game.BestTime), _small);

            // the garage line — '[' and ']' cycle it
            string carName = game.CurrentCar != null ? game.CurrentCar.displayName : "—";
            GUI.Label(new Rect(32, 136, 322, 24), "CAR      ‹ " + carName + " ›", _small);
            GUI.Label(new Rect(32, 156, 322, 24), "         [  ]  change car", _tiny);

            GUI.Label(new Rect(32, 176, 322, 22),
                      game.phase == ParkingManager.Phase.Parked
                          ? "TIME TAKEN  " + ParkingManager.Format(game.lastResultTime)
                          : game.LevelHint,
                      _tiny);
        }

        void DrawAlignmentPanel()
        {
            var bay = game.lot != null ? game.lot.TargetBay : null;
            var panel = new Rect(18, Screen.height - 150, 380, 132);
            Fill(panel, new Color(0f, 0f, 0f, 0.48f));

            GUI.Label(new Rect(32, Screen.height - 144, 360, 24), "ALIGNMENT", _label);

            // the meter: 60% corner fit, 40% squareness, matches ParkingBay.Score
            var bar = new Rect(32, Screen.height - 114, 352, 16);
            Fill(bar, new Color(1f, 1f, 1f, 0.14f));
            float a = Mathf.Clamp01(game.alignment);
            var fillColour = a > 0.93f ? new Color(0.35f, 1f, 0.45f)
                           : a > 0.6f ? new Color(1f, 0.85f, 0.3f)
                                      : new Color(0.35f, 0.8f, 1f);
            GUI.color = fillColour;
            Fill(new Rect(bar.x, bar.y, bar.width * a, bar.height), fillColour);
            GUI.color = Color.white;

            GUI.Label(new Rect(32, Screen.height - 94, 360, 22),
                      string.Format("{0:0}%   ·   corners {1:0}%  ·  {2:0}° off axis",
                                    a * 100f, game.cornerScore * 100f, game.headingError), _small);
            GUI.Label(new Rect(32, Screen.height - 72, 360, 22),
                      "BAY  " + game.distanceToBay.ToString("0.0") + " m", _small);

            // arrow pointing from the car to the bay, rotated into the car's frame
            if (bay != null)
            {
                Vector3 from = car.transform.position;
                Vector3 to = bay.transform.position;
                Vector3 flat = to - from; flat.y = 0f;
                float bearing = Vector3.SignedAngle(car.transform.forward, flat, Vector3.up);

                var pivot = new Vector2(336f, Screen.height - 56f);
                var rect = new Rect(pivot.x - 20f, pivot.y - 20f, 40f, 40f);
                var prev = GUI.matrix;
                GUIUtility.RotateAroundPivot(-bearing, pivot);
                GUI.color = new Color(0.35f, 0.9f, 1f, 0.95f);
                GUI.DrawTexture(rect, _arrow);
                GUI.color = Color.white;
                GUI.matrix = prev;
            }

            // hold progress once the car is actually in the bay
            if (game.holdTimer > 0f && game.phase == ParkingManager.Phase.Playing)
            {
                float h = Mathf.Clamp01(game.holdTimer / Mathf.Max(0.001f, game.holdSeconds));
                GUI.Label(new Rect(32, Screen.height - 52, 240, 22), "HOLDING…", _small);
                GUI.color = new Color(0.35f, 1f, 0.45f);
                Fill(new Rect(140, Screen.height - 50, 220f * h, 16f), GUI.color);
                GUI.color = Color.white;
            }
        }

        void DrawSpeed()
        {
            var r = new Rect(Screen.width - 250, Screen.height - 122, 232, 104);
            Shadowed(r, Mathf.RoundToInt(car.SpeedKph) + " km/h", _big, TextAnchor.LowerRight);

            string gear = car.SpeedMps < -0.3f ? "R" : (Mathf.Abs(car.SpeedMps) < 0.3f ? "N" : "D");
            Shadowed(new Rect(Screen.width - 250, Screen.height - 44, 232, 30), "gear " + gear,
                     _small, TextAnchor.LowerRight);
        }

        void DrawCentreMessage()
        {
            if (!string.IsNullOrEmpty(game.centreMessage))
                Shadowed(new Rect(0, Screen.height * 0.30f, Screen.width, 120f),
                         game.centreMessage, _big, TextAnchor.MiddleCenter);

            if (game.phase == ParkingManager.Phase.Parked)
            {
                string next = game.levelIndex + 1 < game.LevelCount
                    ? "press  N  for lot " + (game.levelIndex + 2)
                    : "press  N  to finish";
                Shadowed(new Rect(0, Screen.height * 0.48f, Screen.width, 34f), next, _mid, TextAnchor.MiddleCenter);
            }
            else if (game.phase == ParkingManager.Phase.Complete)
            {
                Shadowed(new Rect(0, Screen.height * 0.44f, Screen.width, 34f),
                         "total  " + ParkingManager.Format(game.totalTime), _mid, TextAnchor.MiddleCenter);
            }
        }

        void DrawHint()
        {
            if (game.phase == ParkingManager.Phase.Complete) return;
            GUI.Label(new Rect(0, Screen.height - 26, Screen.width, 22),
                      "WASD / arrows drive   ·   SPACE handbrake   ·   C camera   ·   R retry lot   ·   1-6 jump   ·   [ ] change car",
                      new GUIStyle(_small) { alignment = TextAnchor.MiddleCenter });
        }

        void DrawHitFlash()
        {
            if (!game.IsHitFlashing) return;
            float k = 1f - Mathf.Clamp01((game.hitFlashUntil - Time.time) / 0.35f);
            GUI.color = new Color(1f, 0.15f, 0.1f, 0.30f * (1f - k));
            Fill(new Rect(0, 0, Screen.width, Screen.height), GUI.color);
            GUI.color = Color.white;
        }

        // ------------------------------------------------------------------ helpers
        void Fill(Rect r, Color c)
        {
            var prev = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, _white);
            GUI.color = prev;
        }

        void Shadowed(Rect r, string text, GUIStyle style, TextAnchor anchor)
        {
            var s = new GUIStyle(style) { alignment = anchor };
            var prev = GUI.color;

            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.Label(new Rect(r.x + 3f, r.y + 3f, r.width, r.height), text, s);
            GUI.color = prev;
            GUI.Label(r, text, s);
        }
    }
}
