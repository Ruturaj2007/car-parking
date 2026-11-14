using System.Collections.Generic;
using UnityEngine;

namespace CarParking
{
    /// <summary>
    /// Generates a whole level's static geometry from a <see cref="LevelDef"/>: the slab, the
    /// perimeter, every painted bay, the highlighted target bay, and the props. Build() is
    /// idempotent — it clears its own children first — so the same call runs from the editor
    /// builder and again at runtime when a level loads.
    /// </summary>
    public class LotBuilder : MonoBehaviour
    {
        public ParkingBay TargetBay { get; private set; }

        // ------------------------------------------------------------------ colours
        static readonly Color C_Ground = new Color(0.11f, 0.16f, 0.11f);
        static readonly Color C_Asphalt = new Color(0.13f, 0.135f, 0.145f);
        static readonly Color C_BayFill = new Color(0.155f, 0.16f, 0.17f);
        static readonly Color C_Line = new Color(0.80f, 0.80f, 0.78f);
        static readonly Color C_Target = new Color(0.15f, 0.85f, 1.00f);
        static readonly Color C_Wall = new Color(0.62f, 0.62f, 0.60f);
        static readonly Color C_Kerb = new Color(0.66f, 0.62f, 0.50f);
        static readonly Color C_Cone = new Color(0.95f, 0.36f, 0.06f);
        static readonly Color C_Pillar = new Color(0.70f, 0.70f, 0.68f);

        // ------------------------------------------------------------------ materials
        Material _ground, _asphalt, _bayFill, _line, _target, _wall, _kerb, _cone, _pillar, _beam;

        void LoadMaterials()
        {
            _ground = M("Ground", C_Ground, 0f, 0.08f);
            _asphalt = M("Asphalt", C_Asphalt, 0f, 0.16f);
            _bayFill = M("BayFill", C_BayFill, 0f, 0.22f);
            _line = M("LineWhite", C_Line, 0f, 0.30f);
            _target = M("TargetGlow", C_Target, 0.1f, 0.6f, C_Target * 1.15f);
            _wall = M("Wall", C_Wall, 0f, 0.35f);
            _kerb = M("Kerb", C_Kerb, 0f, 0.25f);
            _cone = M("ConeMat", C_Cone, 0f, 0.35f);
            _pillar = M("PillarMat", C_Pillar, 0.05f, 0.35f);
            _beam = M("Beam", new Color(C_Target.r, C_Target.g, C_Target.b, 0.16f), 0f, 0.9f,
                      C_Target * 0.9f, transparent: true);
        }

        static Material M(string res, Color fallback, float metallic, float smooth,
                          Color? emission = null, bool transparent = false)
        {
            var m = CarFactory.LoadMaterial(res);
            if (m != null) return m;

            var mat = new Material(Shader.Find("Standard"));
            mat.color = fallback;
            mat.SetFloat("_Metallic", metallic);
            mat.SetFloat("_Glossiness", smooth);
            if (emission.HasValue)
            {
                mat.EnableKeyword("_EMISSION");
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                mat.SetColor("_EmissionColor", emission.Value);
            }
            if (transparent)
            {
                mat.SetFloat("_Mode", 3f);
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.DisableKeyword("_ALPHATEST_ON");
                mat.EnableKeyword("_ALPHABLEND_ON");
                mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                mat.renderQueue = 3000;
            }
            return mat;
        }

        // ------------------------------------------------------------------ build
        public void Build(LevelDef def)
        {
            ClearChildren();
            LoadMaterials();

            BuildFloor(def);
            BuildPerimeter(def);
            BuildAsphalt(def);

            for (int i = 0; i < def.bays.Length; i++) BuildPaintedBay(def.bays[i]);
            BuildTargetBay(def.target);
            BuildProps(def);
        }

        void ClearChildren()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
                Kill(transform.GetChild(i).gameObject);
            TargetBay = null;
        }

        void BuildFloor(LevelDef def)
        {
            // slab: a plane 40 m outside the lot so the world does not end at the wall
            var slab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            slab.name = "Ground";
            slab.transform.SetParent(transform, false);
            slab.transform.position = new Vector3(0f, -0.5f, 0f);
            slab.transform.localScale = new Vector3(def.lotW + 80f, 1f, def.lotL + 80f);
            slab.GetComponent<MeshRenderer>().sharedMaterial = _ground;
        }

        void BuildAsphalt(LevelDef def)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "LotSurface";
            go.transform.SetParent(transform, false);
            go.transform.position = new Vector3(0f, -0.04f, 0f);
            go.transform.localScale = new Vector3(def.lotW, 0.1f, def.lotL);
            go.GetComponent<MeshRenderer>().sharedMaterial = _asphalt;
            Strip(go);
        }

        void BuildPerimeter(LevelDef def)
        {
            float h = 1.5f, t = 0.8f;
            float hw = def.lotW * 0.5f, hl = def.lotL * 0.5f;
            var root = new GameObject("Perimeter").transform;
            root.SetParent(transform, false);

            Box(root, "WallN", new Vector3(0f, h * 0.5f, hl + t * 0.5f), Quaternion.identity,
                new Vector3(def.lotW + t * 2f, h, t), _wall);
            Box(root, "WallS", new Vector3(0f, h * 0.5f, -hl - t * 0.5f), Quaternion.identity,
                new Vector3(def.lotW + t * 2f, h, t), _wall);
            Box(root, "WallE", new Vector3(hw + t * 0.5f, h * 0.5f, 0f), Quaternion.identity,
                new Vector3(t, h, def.lotL + t * 2f), _wall);
            Box(root, "WallW", new Vector3(-hw - t * 0.5f, h * 0.5f, 0f), Quaternion.identity,
                new Vector3(t, h, def.lotL + t * 2f), _wall);
        }

        /// <summary>Painted markings only — a filled rectangle plus four edge lines.</summary>
        void BuildPaintedBay(BayDef b)
        {
            var root = new GameObject("Bay");
            root.transform.SetParent(transform, false);
            root.transform.position = new Vector3(b.x, 0f, b.z);
            root.transform.rotation = Quaternion.Euler(0f, b.yaw, 0f);

            Box(root.transform, "Fill", new Vector3(0f, 0.012f, 0f), Quaternion.identity,
                new Vector3(b.w, 0.024f, b.len), _bayFill, collider: false);

            float lw = 0.12f;
            Box(root.transform, "LineL", new Vector3(-b.w * 0.5f, 0.03f, 0f), Quaternion.identity,
                new Vector3(lw, 0.03f, b.len), _line, collider: false);
            Box(root.transform, "LineR", new Vector3(b.w * 0.5f, 0.03f, 0f), Quaternion.identity,
                new Vector3(lw, 0.03f, b.len), _line, collider: false);
            Box(root.transform, "LineB", new Vector3(0f, 0.03f, -b.len * 0.5f), Quaternion.identity,
                new Vector3(b.w, 0.03f, lw), _line, collider: false);
            Box(root.transform, "LineF", new Vector3(0f, 0.03f, b.len * 0.5f), Quaternion.identity,
                new Vector3(b.w, 0.03f, lw), _line, collider: false);
        }

        /// <summary>The bay the player has to reach: same markings plus a glow, a beam and posts.</summary>
        void BuildTargetBay(BayDef t)
        {
            var go = new GameObject("TargetBay");
            go.transform.SetParent(transform, false);
            go.transform.position = new Vector3(t.x, 0f, t.z);
            go.transform.rotation = Quaternion.Euler(0f, t.yaw, 0f);

            var bay = go.AddComponent<ParkingBay>();
            bay.size = new Vector2(t.w, t.len);
            bay.isTarget = true;
            TargetBay = bay;

            var glow = Box(go.transform, "Glow", new Vector3(0f, 0.022f, 0f), Quaternion.identity,
                new Vector3(t.w * 0.94f, 0.02f, t.len * 0.94f), _target, collider: false);
            glow.AddComponent<BayPulse>().rend = glow.GetComponent<Renderer>();

            for (int sx = -1; sx <= 1; sx += 2)
                for (int sz = -1; sz <= 1; sz += 2)
                {
                    var post = Box(go.transform, "Post",
                        new Vector3(sx * t.w * 0.5f, 0.5f, sz * t.len * 0.5f),
                        Quaternion.identity, new Vector3(0.12f, 1.0f, 0.12f), _target, collider: false);
                    post.AddComponent<BayPulse>().rend = post.GetComponent<Renderer>();
                }

            var beam = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            beam.name = "Beam";
            beam.transform.SetParent(go.transform, false);
            beam.transform.localPosition = new Vector3(0f, 3.2f, 0f);
            beam.transform.localScale = new Vector3(t.w * 0.55f, 3.2f, t.w * 0.55f);
            beam.GetComponent<MeshRenderer>().sharedMaterial = _beam;
            Strip(beam);
            var bob = beam.AddComponent<Bobber>();
            bob.amplitude = 0.35f;
            bob.speed = 1.1f;
        }

        void BuildProps(LevelDef def)
        {
            var root = new GameObject("Props").transform;
            root.SetParent(transform, false);

            for (int i = 0; i < def.props.Length; i++)
            {
                var p = def.props[i];
                switch (p.kind)
                {
                    case PropKind.ParkedCar:
                        CarFactory.CreateParkedCar(root, new Vector3(p.x, 0f, p.z), p.yaw, p.paint);
                        break;

                    case PropKind.Cone:
                    {
                        var go = new GameObject("Cone");
                        go.transform.SetParent(root, false);
                        go.transform.position = new Vector3(p.x, 0f, p.z);
                        var mf = go.AddComponent<MeshFilter>();
                        mf.sharedMesh = ConeMesh(0.22f, 0.72f, 14);
                        go.AddComponent<MeshRenderer>().sharedMaterial = _cone;
                        var bc = go.AddComponent<BoxCollider>();
                        bc.center = new Vector3(0f, 0.36f, 0f);
                        bc.size = new Vector3(0.44f, 0.72f, 0.44f);
                        go.AddComponent<Obstacle>().label = "cone";
                        break;
                    }

                    case PropKind.Pillar:
                        Box(root, "Pillar", new Vector3(p.x, p.sy * 0.5f, p.z), Quaternion.identity,
                            new Vector3(p.sx, p.sy, p.sz), _pillar)
                            .AddComponent<Obstacle>().label = "pillar";
                        break;

                    case PropKind.Block:
                    {
                        var b = Box(root, "Barrier", new Vector3(p.x, p.sy * 0.5f, p.z),
                            Quaternion.Euler(0f, p.yaw, 0f), new Vector3(p.sx, p.sy, p.sz), _wall);
                        b.AddComponent<Obstacle>().label = "barrier";
                        break;
                    }

                    case PropKind.Kerb:
                    {
                        // the kerb is solid, so it must also report as an obstacle — otherwise the
                        // level validator walks straight through it and signs off a lot you cannot drive
                        var k = Box(root, "Kerb", new Vector3(p.x, p.sy * 0.5f, p.z), Quaternion.identity,
                            new Vector3(p.sx, p.sy, p.sz), _kerb);
                        k.AddComponent<Obstacle>().label = "kerb";
                        break;
                    }
                }
            }
        }

        // ------------------------------------------------------------------ helpers
        static GameObject Box(Transform parent, string name, Vector3 localPos, Quaternion rot,
                              Vector3 scale, Material mat, bool collider = true)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = rot;
            go.transform.localScale = scale;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            if (!collider) Strip(go);
            return go;
        }

        static void Strip(GameObject go)
        {
            var c = go.GetComponent<Collider>();
            if (c == null) return;
            if (Application.isPlaying) Object.Destroy(c); else Object.DestroyImmediate(c);
        }

        static void Kill(GameObject go)
        {
            if (Application.isPlaying) Object.Destroy(go); else Object.DestroyImmediate(go);
        }

        /// <summary>
        /// A traffic-cone cone. Winding is verified against the geometry after the fact and
        /// flipped if the side normals came out pointing at the axis instead of away from it,
        /// which is cheaper than reasoning about Unity's face direction.
        /// </summary>
        public static Mesh ConeMesh(float radius, float height, int segments)
        {
            var verts = new List<Vector3>();
            var tris = new List<int>();

            verts.Add(new Vector3(0f, height, 0f));                       // 0 = apex
            for (int i = 0; i <= segments; i++)
            {
                float a = i / (float)segments * Mathf.PI * 2f;
                verts.Add(new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius));   // 1..segments+1
            }
            for (int i = 1; i <= segments; i++) { tris.Add(0); tris.Add(i + 1); tris.Add(i); }

            int centre = verts.Count;
            verts.Add(Vector3.zero);
            int ring = verts.Count;
            for (int i = 0; i <= segments; i++)
            {
                float a = i / (float)segments * Mathf.PI * 2f;
                verts.Add(new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius));
            }
            for (int i = 0; i < segments; i++) { tris.Add(centre); tris.Add(ring + i); tris.Add(ring + i + 1); }

            var mesh = new Mesh { name = "ConeMesh" };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            var n = mesh.normals;
            float sum = 0f;
            for (int i = 1; i <= segments; i++)
            {
                Vector3 radial = new Vector3(verts[i].x, 0f, verts[i].z).normalized;
                sum += Vector3.Dot(n[i], radial);
            }
            if (sum < 0f)
            {
                for (int t = 0; t < tris.Count; t += 3)
                {
                    int tmp = tris[t + 1]; tris[t + 1] = tris[t + 2]; tris[t + 2] = tmp;
                }
                mesh.SetTriangles(tris, 0);
                mesh.RecalculateNormals();
            }
            return mesh;
        }
    }
}
