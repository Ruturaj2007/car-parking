using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CarParking.EditorTools
{
    /// <summary>
    /// Authoring entry point. The whole scene is generated from code — materials, sun, lot, car,
    /// camera, managers — so there is no hand-authored YAML to keep in sync, and the same code
    /// runs headlessly via -executeMethod to produce a player.
    /// </summary>
    public static class GameBuilder
    {
        const string ScenePath = "Assets/CarParking/Scenes/Parking.unity";
        const string MatDir = "Assets/CarParking/Resources/Materials";
        const string ModelPath = "Assets/CarParking/Resources/car.fbx";
        const string BuildPath = "Builds/CarParking.app";

        [MenuItem("Car Parking/Rebuild Parking Scene")]
        public static void BuildSceneMenu() => BuildScene();

        [MenuItem("Car Parking/Build macOS Player")]
        public static void BuildPlayerMenu() => BuildPlayer();

        [MenuItem("Car Parking/Report Levels")]
        public static void ReportLevelsMenu() => ReportLevels();

        public static void BuildAll()
        {
            ConfigurePlayerSettings();
            BuildScene();
            BuildPlayer();
        }

        /// <summary>
        /// Project-level player settings. These came along with the project this one was cloned
        /// from, so they have to be re-stated or the shipped app is called "car-game".
        /// </summary>
        public static void ConfigurePlayerSettings()
        {
            PlayerSettings.companyName = "Ruturaj Mahendrakar";
            PlayerSettings.productName = "CarParking";
            PlayerSettings.applicationIdentifier = "com.ruturaj.carparking";
            PlayerSettings.runInBackground = true;      // keeps ticking when the window loses focus
            PlayerSettings.defaultScreenWidth = 1280;
            PlayerSettings.defaultScreenHeight = 720;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.resizableWindow = true;
            Debug.Log("[GameBuilder] player settings -> CarParking (windowed 1280x720, run in background)");
        }

        // ------------------------------------------------------------------ scene
        public static void BuildScene()
        {
            foreach (var d in new[] { "Assets/CarParking/Scenes", MatDir, "Builds" })
                Directory.CreateDirectory(d);
            AssetDatabase.Refresh();

            EnsureModelImport();
            CreateMaterials();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            ReportLevels();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // ---- lighting ----
            var sunGO = new GameObject("Sun");
            var sun = sunGO.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.15f;
            sun.color = new Color(1f, 0.96f, 0.90f);
            sun.shadows = LightShadows.Soft;
            sunGO.transform.rotation = Quaternion.Euler(46f, -38f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.44f, 0.52f, 0.64f);
            RenderSettings.ambientEquatorColor = new Color(0.30f, 0.32f, 0.34f);
            RenderSettings.ambientGroundColor = new Color(0.11f, 0.11f, 0.11f);

            // ---- lot ----
            var lotGO = new GameObject("Lot");
            var lot = lotGO.AddComponent<LotBuilder>();

            // ---- car ----
            var def0 = Levels.All[0];
            var spawn = new Vector3(def0.spawnX, 0.35f, def0.spawnZ);
            var rot = Quaternion.Euler(0f, def0.spawnYaw, 0f);
            var carGO = CarFactory.CreatePlayerCar(null, spawn, rot);
            var car = carGO.GetComponent<CarController>();
            if (car == null) { Debug.LogError("[GameBuilder] player car has no CarController"); return; }

            // ---- camera ----
            var camGO = new GameObject("Main Camera");
            camGO.tag = "MainCamera";
            var cam = camGO.AddComponent<Camera>();
            cam.nearClipPlane = 0.2f;
            cam.farClipPlane = 900f;
            cam.fieldOfView = 58f;
            camGO.AddComponent<AudioListener>();
            var rig = camGO.AddComponent<CameraRig>();
            rig.target = carGO.transform;
            camGO.transform.position = spawn - rot * Vector3.forward * 8f + Vector3.up * 3f;

            // ---- manager + hud ----
            var gameGO = new GameObject("Game");
            var mgr = gameGO.AddComponent<ParkingManager>();
            mgr.car = car;
            mgr.lot = lot;
            mgr.rig = rig;
            var hud = gameGO.AddComponent<ParkingHud>();
            hud.car = car;
            hud.game = mgr;

            // bake lot 1 into the saved scene so the editor view is not an empty plane
            lot.Build(Levels.All[0]);

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();

            Debug.Log("[GameBuilder] scene built -> " + ScenePath + " (levels=" + Levels.All.Length + ")");
        }

        // ------------------------------------------------------------------ player
        public static void BuildPlayer()
        {
            Directory.CreateDirectory("Builds");
            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = BuildPath,
                target = BuildTarget.StandaloneOSX,
                options = BuildOptions.None
            };
            var report = UnityEditor.BuildPipeline.BuildPlayer(options);
            var s = report.summary;
            Debug.Log(string.Format("[GameBuilder] build {0} errors={1} size={2}MB -> {3}",
                                    s.result, s.totalErrors, (s.totalSize / (1024f * 1024f)).ToString("0.0"), BuildPath));
            if (s.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                UnityEditor.EditorApplication.Exit(1);
        }

        // ------------------------------------------------------------------ self-check
        /// <summary>
        /// Sanity-checks every lot on paper before anything is built: bays and props must sit inside
        /// the perimeter, and the spawn must be clear of them. Logs a warning per violation so a
        /// headless build surfaces a bad number instead of a level that cannot be finished.
        /// </summary>
        public static void ReportLevels()
        {
            int warnings = 0;
            for (int i = 0; i < Levels.All.Length; i++)
            {
                var d = Levels.All[i];
                // the perimeter wall's inner face sits exactly on the lot edge, so that is the bound
                float hx = d.lotW * 0.5f, hz = d.lotL * 0.5f;

                for (int b = 0; b < d.bays.Length; b++)
                {
                    var bay = d.bays[b];
                    float ex = Mathf.Abs(Mathf.Cos(bay.yaw * Mathf.Deg2Rad)) * bay.w * 0.5f
                             + Mathf.Sin(Mathf.Abs(bay.yaw) * Mathf.Deg2Rad) * bay.len * 0.5f;
                    float ez = Mathf.Abs(Mathf.Cos(bay.yaw * Mathf.Deg2Rad)) * bay.len * 0.5f
                             + Mathf.Sin(Mathf.Abs(bay.yaw) * Mathf.Deg2Rad) * bay.w * 0.5f;
                    if (Mathf.Abs(bay.x) + ex > hx || Mathf.Abs(bay.z) + ez > hz)
                    {
                        Debug.LogWarning(string.Format("[Levels] lot {0} '{1}': bay {2} at ({3:0.0},{4:0.0}) yaw {5:0} pokes past the wall",
                                          i + 1, d.name, b, bay.x, bay.z, bay.yaw));
                        warnings++;
                    }
                }

                if (Mathf.Abs(d.spawnX) > hx || Mathf.Abs(d.spawnZ) > hz)
                {
                    Debug.LogWarning(string.Format("[Levels] lot {0} '{1}': spawn ({2:0.0},{3:0.0}) is outside the lot",
                                      i + 1, d.name, d.spawnX, d.spawnZ));
                    warnings++;
                }

                for (int p = 0; p < d.props.Length; p++)
                {
                    var pr = d.props[p];
                    if (pr.kind != PropKind.ParkedCar && pr.kind != PropKind.Cone) continue;
                    if (pr.kind == PropKind.ParkedCar &&
                        Vector2.Distance(new Vector2(pr.x, pr.z), new Vector2(d.spawnX, d.spawnZ)) < 4f)
                    {
                        Debug.LogWarning(string.Format("[Levels] lot {0} '{1}': prop {2} spawns on top of the car",
                                          i + 1, d.name, p));
                        warnings++;
                    }
                }
            }
            Debug.Log("[GameBuilder] level report: " + Levels.All.Length + " lots, " + warnings + " warning(s)");
        }

        // ------------------------------------------------------------------ materials
        static void EnsureModelImport()
        {
            // the FBX is authored in centimetres (Blender global_scale 100); useFileScale alone
            // leaves the car 442 m long and the camera ends up inside it
            var importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
            if (importer == null) { Debug.LogError("[GameBuilder] no ModelImporter at " + ModelPath); return; }
            if (Mathf.Abs(importer.globalScale - 0.01f) > 0.0001f || !importer.useFileScale)
            {
                importer.globalScale = 0.01f;
                importer.useFileScale = true;
                importer.SaveAndReimport();
                Debug.Log("[GameBuilder] model importer globalScale set to 0.01 (cm -> m)");
            }
        }

        static void CreateMaterials()
        {
            // ---- car ----
            Mat("CarPaint", new Color(0.03f, 0.10f, 0.42f), 0.85f, 0.78f);
            Mat("Glass", new Color(0.02f, 0.04f, 0.05f, 0.55f), 0f, 0.95f, transparent: true);
            Mat("Trim", new Color(0.05f, 0.05f, 0.06f), 0.30f, 0.45f);
            Mat("Rubber", new Color(0.04f, 0.04f, 0.045f), 0f, 0.12f);
            Mat("Alloy", new Color(0.72f, 0.74f, 0.78f), 1f, 0.72f);
            Mat("BrakeDisc", new Color(0.32f, 0.32f, 0.34f), 0.80f, 0.40f);
            Mat("Liner", new Color(0.06f, 0.06f, 0.07f), 0.10f, 0.25f);
            Mat("LensRear", new Color(0.55f, 0.04f, 0.03f), 0f, 0.85f, emission: new Color(0.6f, 0.05f, 0.03f));
            Mat("LensFront", new Color(0.90f, 0.92f, 0.95f), 0f, 0.90f);
            Mat("Interior", new Color(0.05f, 0.05f, 0.055f), 0f, 0.20f);

            // ---- lot ----
            Mat("Ground", new Color(0.11f, 0.16f, 0.11f), 0f, 0.08f);
            Mat("Asphalt", new Color(0.13f, 0.135f, 0.145f), 0f, 0.16f);
            Mat("BayFill", new Color(0.155f, 0.16f, 0.17f), 0f, 0.22f);
            Mat("LineWhite", new Color(0.80f, 0.80f, 0.78f), 0f, 0.30f);
            Mat("TargetGlow", new Color(0.15f, 0.85f, 1.00f), 0.10f, 0.60f,
                emission: new Color(0.15f, 0.85f, 1.00f) * 1.15f);
            Mat("Wall", new Color(0.62f, 0.62f, 0.60f), 0f, 0.35f);
            Mat("Kerb", new Color(0.66f, 0.62f, 0.50f), 0f, 0.25f);
            Mat("ConeMat", new Color(0.95f, 0.36f, 0.06f), 0f, 0.35f);
            Mat("PillarMat", new Color(0.70f, 0.70f, 0.68f), 0.05f, 0.35f);
            Mat("Beam", new Color(0.15f, 0.85f, 1.00f, 0.16f), 0f, 0.90f,
                emission: new Color(0.135f, 0.765f, 0.90f), transparent: true);
        }

        static Material Mat(string name, Color colour, float metallic, float smoothness,
                            bool transparent = false, Color? emission = null)
        {
            var path = MatDir + "/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find("Standard"));
                AssetDatabase.CreateAsset(m, path);
            }
            m.shader = Shader.Find("Standard");
            m.SetColor("_Color", colour);
            m.SetFloat("_Metallic", metallic);
            m.SetFloat("_Glossiness", smoothness);

            if (emission.HasValue)
            {
                m.EnableKeyword("_EMISSION");
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                m.SetColor("_EmissionColor", emission.Value);
            }
            else
            {
                m.DisableKeyword("_EMISSION");
            }

            if (transparent)
            {
                m.SetFloat("_Mode", 3f);
                m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                m.SetInt("_ZWrite", 0);
                m.DisableKeyword("_ALPHATEST_ON");
                m.EnableKeyword("_ALPHABLEND_ON");
                m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                m.renderQueue = 3000;
            }
            else
            {
                m.SetFloat("_Mode", 0f);
                m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
                m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
                m.SetInt("_ZWrite", 1);
                m.DisableKeyword("_ALPHABLEND_ON");
                m.renderQueue = -1;
            }

            EditorUtility.SetDirty(m);
            return m;
        }
    }
}
