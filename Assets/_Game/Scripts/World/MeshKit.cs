using System.Collections.Generic;
using UnityEngine;

namespace Vykhod
{
    /// <summary>Генерация простой геометрии: стены, плиты из полигонов, дороги, земля. Цветные и текстурированные варианты.</summary>
    public static class MeshKit
    {
        public static GameObject Box(Transform parent, Vector3 center, Vector3 size, Color c, bool collider = true, bool isStatic = true, Quaternion rot = default)
            => Box(parent, center, size, AssetCatalog.Mat(c), collider, isStatic, rot);

        public static GameObject Box(Transform parent, Vector3 center, Vector3 size, Material m, bool collider = true, bool isStatic = true, Quaternion rot = default)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = center; go.transform.localScale = size;
            if (rot != default) go.transform.localRotation = rot;
            go.GetComponent<Renderer>().sharedMaterial = m;
            if (!collider) Object.Destroy(go.GetComponent<Collider>());
            if (isStatic) go.isStatic = true;
            return go;
        }

        /// <summary>Стена между точками a и b (XZ), высота h, толщина t, основание на yBase.</summary>
        public static GameObject Wall(Transform parent, Vector2 a, Vector2 b, float h, float t, Color c, float yBase = 0, bool collider = true)
            => Wall(parent, a, b, h, t, AssetCatalog.Mat(c), yBase, collider);

        public static GameObject Wall(Transform parent, Vector2 a, Vector2 b, float h, float t, Material m, float yBase = 0, bool collider = true)
        {
            var d = b - a; float len = d.magnitude; if (len < 0.05f) return null;
            var mid = (a + b) * 0.5f;
            float ang = Mathf.Atan2(d.x, d.y) * Mathf.Rad2Deg;
            return Box(parent, new Vector3(mid.x, yBase + h * 0.5f, mid.y), new Vector3(t, h, len), m, collider, true, Quaternion.Euler(0, ang, 0));
        }

        public static float SignedArea(List<Vector2> p)
        {
            float a = 0; for (int i = 0; i < p.Count; i++) { var j = p[(i + 1) % p.Count]; a += p[i].x * j.y - j.x * p[i].y; }
            return a * 0.5f;
        }

        public static Vector2 Centroid(List<Vector2> p) { var c = Vector2.zero; foreach (var v in p) c += v; return p.Count > 0 ? c / p.Count : c; }

        public static bool PointInPoly(Vector2 pt, List<Vector2> poly)
        {
            bool inside = false;
            for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
                if ((poly[i].y > pt.y) != (poly[j].y > pt.y) && pt.x < (poly[j].x - poly[i].x) * (pt.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x) inside = !inside;
            return inside;
        }

        /// <summary>Триангуляция простого полигона методом отсечения ушей. Возвращает индексы (CCW).</summary>
        public static List<int> Triangulate(List<Vector2> poly)
        {
            var idx = new List<int>();
            int n = poly.Count; if (n < 3) return idx;
            var V = new List<int>(n);
            if (SignedArea(poly) > 0) for (int i = 0; i < n; i++) V.Add(i); else for (int i = 0; i < n; i++) V.Add(n - 1 - i);
            int guard = 0;
            while (V.Count > 3 && guard++ < 5000)
            {
                bool cut = false;
                for (int i = 0; i < V.Count; i++)
                {
                    int i0 = V[(i + V.Count - 1) % V.Count], i1 = V[i], i2 = V[(i + 1) % V.Count];
                    Vector2 a = poly[i0], b = poly[i1], c = poly[i2];
                    if (Cross(b - a, c - a) <= 0.00001f) continue;
                    bool anyInside = false;
                    foreach (var k in V) { if (k == i0 || k == i1 || k == i2) continue; if (InTri(poly[k], a, b, c)) { anyInside = true; break; } }
                    if (anyInside) continue;
                    idx.Add(i0); idx.Add(i1); idx.Add(i2);
                    V.RemoveAt(i); cut = true; break;
                }
                if (!cut) { for (int i = 1; i + 1 < V.Count; i++) { idx.Add(V[0]); idx.Add(V[i]); idx.Add(V[i + 1]); } return idx; }
            }
            if (V.Count == 3) { idx.Add(V[0]); idx.Add(V[1]); idx.Add(V[2]); }
            return idx;
        }

        static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;
        static bool InTri(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float d1 = Cross(b - a, p - a), d2 = Cross(c - b, p - b), d3 = Cross(a - c, p - c);
            bool neg = d1 < 0 || d2 < 0 || d3 < 0, pos = d1 > 0 || d2 > 0 || d3 > 0;
            return !(neg && pos);
        }

        public static GameObject Slab(Transform parent, List<Vector2> poly, float y, float t, Color c, bool collider = true, string name = "slab")
            => Slab(parent, poly, y, t, AssetCatalog.Mat(c), collider, name);

        /// <summary>Плита из полигона (XZ) толщиной t с основанием на y: крыши, полы, газоны, вода.</summary>
        public static GameObject Slab(Transform parent, List<Vector2> poly, float y, float t, Material m, bool collider = true, string name = "slab")
        {
            var tri = Triangulate(poly); if (tri.Count < 3) return null;
            int n = poly.Count;
            var verts = new List<Vector3>(); var tris = new List<int>(); var norms = new List<Vector3>(); var uvs = new List<Vector2>();
            for (int i = 0; i < n; i++) { verts.Add(new Vector3(poly[i].x, y + t, poly[i].y)); norms.Add(Vector3.up); uvs.Add(poly[i] * 0.2f); }
            for (int i = 0; i < tri.Count; i += 3) { tris.Add(tri[i]); tris.Add(tri[i + 2]); tris.Add(tri[i + 1]); }
            if (t > 0.001f)
            {
                int o = verts.Count;
                for (int i = 0; i < n; i++) { verts.Add(new Vector3(poly[i].x, y, poly[i].y)); norms.Add(Vector3.down); uvs.Add(poly[i] * 0.2f); }
                for (int i = 0; i < tri.Count; i += 3) { tris.Add(o + tri[i]); tris.Add(o + tri[i + 1]); tris.Add(o + tri[i + 2]); }
                bool ccw = SignedArea(poly) > 0;
                for (int i = 0; i < n; i++)
                {
                    int j = (i + 1) % n; int b = verts.Count;
                    Vector3 p0 = new Vector3(poly[i].x, y, poly[i].y), p1 = new Vector3(poly[j].x, y, poly[j].y);
                    var nrm = Vector3.Cross(Vector3.up, p1 - p0).normalized; if (ccw) nrm = -nrm;
                    verts.Add(p0); verts.Add(p1); verts.Add(p1 + Vector3.up * t); verts.Add(p0 + Vector3.up * t);
                    for (int k = 0; k < 4; k++) norms.Add(nrm);
                    float len = (p1 - p0).magnitude; uvs.Add(new Vector2(0, 0)); uvs.Add(new Vector2(len, 0)); uvs.Add(new Vector2(len, t)); uvs.Add(new Vector2(0, t));
                    if (ccw) { tris.Add(b); tris.Add(b + 1); tris.Add(b + 2); tris.Add(b); tris.Add(b + 2); tris.Add(b + 3); }
                    else { tris.Add(b); tris.Add(b + 2); tris.Add(b + 1); tris.Add(b); tris.Add(b + 3); tris.Add(b + 2); }
                }
            }
            return MakeMesh(parent, verts, tris, norms, uvs, m, collider, name);
        }

        public static GameObject Strip(Transform parent, List<Vector2> pts, float width, float y, Color c, string name = "road")
            => Strip(parent, pts, width, y, AssetCatalog.Mat(c), name);

        /// <summary>Лента (дорога/тропа) вдоль ломаной.</summary>
        public static GameObject Strip(Transform parent, List<Vector2> pts, float width, float y, Material m, string name = "road")
        {
            if (pts.Count < 2) return null;
            var verts = new List<Vector3>(); var tris = new List<int>(); var norms = new List<Vector3>(); var uvs = new List<Vector2>();
            float hw = width * 0.5f; float dist = 0;
            for (int i = 0; i < pts.Count; i++)
            {
                Vector2 dir = i == 0 ? (pts[1] - pts[0]) : i == pts.Count - 1 ? (pts[i] - pts[i - 1]) : ((pts[i] - pts[i - 1]).normalized + (pts[i + 1] - pts[i]).normalized);
                if (dir.sqrMagnitude < 1e-6f) dir = Vector2.up; dir.Normalize();
                var nrm = new Vector2(-dir.y, dir.x) * hw;
                if (i > 0) dist += (pts[i] - pts[i - 1]).magnitude;
                verts.Add(new Vector3(pts[i].x + nrm.x, y, pts[i].y + nrm.y)); verts.Add(new Vector3(pts[i].x - nrm.x, y, pts[i].y - nrm.y));
                norms.Add(Vector3.up); norms.Add(Vector3.up); uvs.Add(new Vector2(0, dist * 0.1f)); uvs.Add(new Vector2(1, dist * 0.1f));
                if (i > 0) { int b = (i - 1) * 2; tris.Add(b); tris.Add(b + 2); tris.Add(b + 1); tris.Add(b + 1); tris.Add(b + 2); tris.Add(b + 3); }
            }
            return MakeMesh(parent, verts, tris, norms, uvs, m, false, name);
        }

        public static GameObject MakeMesh(Transform parent, List<Vector3> verts, List<int> tris, List<Vector3> norms, List<Vector2> uvs, Color c, bool collider, string name)
            => MakeMesh(parent, verts, tris, norms, uvs, AssetCatalog.Mat(c), collider, name);

        public static GameObject MakeMesh(Transform parent, List<Vector3> verts, List<int> tris, List<Vector3> norms, List<Vector2> uvs, Material m, bool collider, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var mesh = new Mesh { indexFormat = verts.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16 };
            mesh.SetVertices(verts); mesh.SetTriangles(tris, 0); mesh.SetNormals(norms); mesh.SetUVs(0, uvs);
            mesh.RecalculateBounds();
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = m;
            if (collider) go.AddComponent<MeshCollider>().sharedMesh = mesh;
            go.isStatic = true;
            return go;
        }

        static Texture2D gridTex;
        public static Texture2D GridTexture()
        {
            if (gridTex != null) return gridTex;
            int s = 64; gridTex = new Texture2D(s, s, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear };
            for (int y = 0; y < s; y++) for (int x = 0; x < s; x++) { bool line = x == 0 || y == 0; float v = line ? 0.78f : 0.9f + 0.05f * (((x / 8 + y / 8) & 1) == 0 ? 1 : 0); gridTex.SetPixel(x, y, new Color(v, v, v, 1)); }
            gridTex.Apply();
            return gridTex;
        }

        /// <summary>Земля: текстура по ключу (grass/asphalt/...) или цвет.</summary>
        public static GameObject Ground(Transform parent, float size, string texKey, float scale = 3f, Color? tint = null)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Plane);
            go.name = "Ground"; go.transform.SetParent(parent, false);
            go.transform.localScale = new Vector3(size / 10f, 1, size / 10f);
            go.GetComponent<Renderer>().sharedMaterial = AssetCatalog.TexMat(texKey, scale, tint, tint ?? new Color(0.4f, 0.55f, 0.3f));
            Object.Destroy(go.GetComponent<MeshCollider>());
            var bc = go.AddComponent<BoxCollider>(); bc.size = new Vector3(10, 0.2f, 10); bc.center = new Vector3(0, -0.1f, 0);
            go.isStatic = true;
            return go;
        }

        public static GameObject Ground(Transform parent, float size, Color c, float tile = 4f)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Plane);
            go.name = "Ground"; go.transform.SetParent(parent, false);
            go.transform.localScale = new Vector3(size / 10f, 1, size / 10f);
            Color.RGBToHSV(c, out var hh, out var ss, out var vv); c = Color.HSVToRGB(hh, Mathf.Clamp01(ss * 1.35f + 0.1f), Mathf.Clamp01(vv * 1.15f + 0.05f));
            var mat = new Material(Shader.Find("Standard")) { color = c, mainTexture = GridTexture(), mainTextureScale = new Vector2(size / tile, size / tile) };
            mat.SetFloat("_Glossiness", 0.02f);
            go.GetComponent<Renderer>().sharedMaterial = mat;
            Object.Destroy(go.GetComponent<MeshCollider>());
            var bc = go.AddComponent<BoxCollider>(); bc.size = new Vector3(10, 0.2f, 10); bc.center = new Vector3(0, -0.1f, 0);
            go.isStatic = true;
            return go;
        }

        public static GameObject Tree(Transform parent, Vector3 pos, System.Random rng)
        {
            var path = AssetCatalog.FindModel("tree", "nature|suburban|survival");
            GameObject go = path != null ? AssetCatalog.Spawn(path, parent, 5f + (float)rng.NextDouble() * 4f, false) : null;
            if (go == null)
            {
                go = new GameObject("tree"); go.transform.SetParent(parent, false);
                float h = 4 + (float)rng.NextDouble() * 4;
                Box(go.transform, new Vector3(0, h * 0.25f, 0), new Vector3(0.4f, h * 0.5f, 0.4f), new Color(0.35f, 0.22f, 0.1f));
                var crown = GameObject.CreatePrimitive(PrimitiveType.Sphere); crown.transform.SetParent(go.transform, false);
                crown.transform.localPosition = new Vector3(0, h * 0.7f, 0); crown.transform.localScale = new Vector3(h * 0.6f, h * 0.7f, h * 0.6f);
                crown.GetComponent<Renderer>().sharedMaterial = AssetCatalog.Mat(new Color(0.15f, 0.45f + (float)rng.NextDouble() * 0.2f, 0.15f));
                Object.Destroy(crown.GetComponent<Collider>()); crown.isStatic = true;
            }
            else { var cc = go.AddComponent<CapsuleCollider>(); cc.radius = 0.4f; cc.height = 5; cc.center = Vector3.up * 2.5f; }
            go.transform.position = pos; go.transform.rotation = Quaternion.Euler(0, (float)rng.NextDouble() * 360, 0);
            go.isStatic = true;
            return go;
        }
    }
}
