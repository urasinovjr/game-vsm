using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace GameVSM.Editor
{
    // Softer near-field shapes: chamfered boxes and smooth round columns. Same metre-UV convention
    // and material table as Box(); meshes are cached per dimension and saved under Generated/.
    public static partial class BuildEnvironmentArt
    {
        static readonly Dictionary<(Vector3, float), Mesh> bevels = new();
        static readonly Dictionary<(float, float, int), Mesh> columns = new();
        static readonly Dictionary<int, Mesh> foliage = new();

        // Shared 120-triangle crowns replace a 768-triangle Unity sphere per cluster.
        // Silhouettes stay irregular; there is no new texture or transparent foliage pass.
        static Mesh Foliage(int variant)
        {
            if (foliage.TryGetValue(variant, out var cached)) return cached;
            const int sides=10, rings=7;
            var vertices=new List<Vector3>(); var uv=new List<Vector2>(); var triangles=new List<int>();
            for(int y=0;y<=rings;y++) for(int x=0;x<=sides;x++)
            {
                float latitude=Mathf.PI*y/rings, longitude=2*Mathf.PI*x/sides;
                var direction=new Vector3(Mathf.Sin(latitude)*Mathf.Cos(longitude),Mathf.Cos(latitude),Mathf.Sin(latitude)*Mathf.Sin(longitude));
                float radius=.5f*(.85f+Mathf.PerlinNoise(direction.x*2+variant*3,direction.y*2+direction.z)*.3f);
                vertices.Add(direction*radius); uv.Add(new Vector2((float)x/sides,(float)y/rings));
                if(y<rings && x<sides)
                {
                    int a=y*(sides+1)+x,b=a+sides+1;
                    if(y>0)triangles.AddRange(new[]{a,a+1,b});
                    if(y<rings-1)triangles.AddRange(new[]{a+1,b+1,b});
                }
            }
            var mesh=new Mesh {name="Shared organic crown"};
            mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);
            mesh.RecalculateNormals();mesh.RecalculateBounds();mesh.RecalculateTangents();
            string path=Art+"/Foliage"+variant+".asset";
            Save(mesh,path);return foliage[variant]=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        }

        // Chamfered box: 6 faces, 12 edge chamfers, 8 corner triangles. bevel is clamped to the smallest half-size.
        static GameObject Bevel(string name, Vector3 p, Vector3 size, string material, float bevel = .04f, bool collision = false)
        {
            Vector3 h = size / 2; float b = Mathf.Clamp(bevel, 0, Mathf.Min(h.x, Mathf.Min(h.y, h.z)) * .9f);
            if (b <= .001f) return Box(name, p, size, material, collision);
            if (!bevels.TryGetValue((size, b), out var mesh))
            {
                var faces = new List<Vector3[]>();
                Vector3 P(int sx, int sy, int sz, int axis) => new Vector3(
                    sx * (axis == 0 ? h.x : h.x - b), sy * (axis == 1 ? h.y : h.y - b), sz * (axis == 2 ? h.z : h.z - b));
                int[] s = { -1, 1 };
                // Main faces.
                for (int axis = 0; axis < 3; axis++) foreach (int d in s)
                {
                    var f = new List<Vector3>();
                    foreach (int u in s) foreach (int v in s)
                        f.Add(axis == 0 ? P(d, u, v, 0) : axis == 1 ? P(u, d, v, 1) : P(u, v, d, 2));
                    faces.Add(f.ToArray());
                }
                // Edge chamfers between axis pairs, running along the third axis.
                for (int a = 0; a < 3; a++) for (int c = a + 1; c < 3; c++)
                {
                    int run = 3 - a - c;
                    foreach (int da in s) foreach (int dc in s)
                    {
                        var f = new List<Vector3>();
                        foreach (int r in s)
                        {
                            var sign = new int[3]; sign[a] = da; sign[c] = dc; sign[run] = r;
                            f.Add(P(sign[0], sign[1], sign[2], a)); f.Add(P(sign[0], sign[1], sign[2], c));
                        }
                        faces.Add(f.ToArray());
                    }
                }
                // Corners.
                foreach (int x in s) foreach (int y in s) foreach (int z in s)
                    faces.Add(new[] { P(x, y, z, 0), P(x, y, z, 1), P(x, y, z, 2) });
                mesh = Polyhedron(faces, "Bevel UV module");
                string path = Art + "/Module" + (++meshId).ToString("D4") + ".asset";
                Save(mesh, path); mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path); bevels[(size, b)] = mesh;
            }
            return Place(name, p, mesh, material, collision ? size : (Vector3?)null);
        }

        // Smooth vertical column standing on 'bottom' (centre of its base).
        static GameObject Column(string name, Vector3 bottom, float height, float radius, string material, int segments = 16, bool collision = false)
        {
            if (!columns.TryGetValue((height, radius, segments), out var mesh))
            {
                var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
                float circumference = 2 * Mathf.PI * radius;
                for (int i = 0; i <= segments; i++)
                {
                    float a = i * 2 * Mathf.PI / segments; var dir = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                    v.Add(dir * radius); v.Add(dir * radius + Vector3.up * height); n.Add(dir); n.Add(dir);
                    uv.Add(new Vector2(circumference * i / segments, 0)); uv.Add(new Vector2(circumference * i / segments, height));
                    if (i < segments) { int k = i * 2; t.AddRange(new[] { k, k + 1, k + 2, k + 2, k + 1, k + 3 }); }
                }
                int top = v.Count; v.Add(Vector3.up * height); n.Add(Vector3.up); uv.Add(Vector2.zero);
                for (int i = 0; i <= segments; i++)
                {
                    float a = i * 2 * Mathf.PI / segments; var dir = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                    v.Add(dir * radius + Vector3.up * height); n.Add(Vector3.up); uv.Add(new Vector2(dir.x, dir.z) * radius);
                    if (i < segments) t.AddRange(new[] { top, top + i + 2, top + i + 1 });
                }
                int baseCenter = v.Count; v.Add(Vector3.zero); n.Add(Vector3.down); uv.Add(Vector2.zero);
                for (int i = 0; i <= segments; i++)
                {
                    float a = i * 2 * Mathf.PI / segments; var dir = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                    v.Add(dir * radius); n.Add(Vector3.down); uv.Add(new Vector2(dir.x, dir.z) * radius);
                    if (i < segments) t.AddRange(new[] { baseCenter, baseCenter + i + 1, baseCenter + i + 2 });
                }
                mesh = new Mesh { name = "Column UV module" };
                mesh.SetVertices(v); mesh.SetNormals(n); mesh.SetUVs(0, uv); mesh.SetTriangles(t, 0);
                mesh.RecalculateBounds(); mesh.RecalculateTangents();
                string path = Art + "/Module" + (++meshId).ToString("D4") + ".asset";
                Save(mesh, path); mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path); columns[(height, radius, segments)] = mesh;
            }
            var go = Place(name, bottom, mesh, material, null);
            if (collision)
            {
                var c = go.AddComponent<CapsuleCollider>(); c.radius = radius; c.height = Mathf.Max(height, radius * 2);
                c.center = Vector3.up * height / 2;
            }
            return go;
        }

        static GameObject Place(string name, Vector3 p, Mesh mesh, string material, Vector3? boxCollider)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(root, false); go.transform.localPosition = p;
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.GetComponent<MeshRenderer>(); renderer.sharedMaterial = mats[material];
            renderer.shadowCastingMode = material is "Glass" or "Lamp" ? ShadowCastingMode.Off : ShadowCastingMode.On;
            if (boxCollider.HasValue) go.AddComponent<BoxCollider>().size = boxCollider.Value;
            go.isStatic = true; return go;
        }

        // Flat-shaded convex polyhedron from planar faces (points in any order); outward winding by centroid.
        static Mesh Polyhedron(List<Vector3[]> faces, string name)
        {
            var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
            foreach (var face in faces)
            {
                Vector3 c = Vector3.zero; foreach (var q in face) c += q; c /= face.Length;
                Vector3 normal = Vector3.Cross(face[1] - face[0], face[2] - face[0]);
                if (normal.sqrMagnitude < 1e-10f && face.Length > 3) normal = Vector3.Cross(face[2] - face[0], face[3] - face[0]);
                normal.Normalize(); if (Vector3.Dot(normal, c) < 0) normal = -normal;
                Vector3 axisU = (face[0] - c).normalized, axisV = Vector3.Cross(normal, axisU);
                var ordered = new List<Vector3>(face);
                ordered.Sort((p, q) => Mathf.Atan2(Vector3.Dot(p - c, axisV), Vector3.Dot(p - c, axisU))
                    .CompareTo(Mathf.Atan2(Vector3.Dot(q - c, axisV), Vector3.Dot(q - c, axisU))));
                int start = v.Count;
                foreach (var q in ordered)
                {
                    v.Add(q); n.Add(normal);
                    uv.Add(Mathf.Abs(normal.y) > .5f ? new Vector2(q.x, q.z) : Mathf.Abs(normal.x) > .5f ? new Vector2(q.z, q.y) : new Vector2(q.x, q.y));
                }
                for (int i = 1; i + 1 < ordered.Count; i++)
                {
                    // Unity front faces: cross(b - a, c - a) points toward the viewer.
                    Vector3 a = ordered[0], b2 = ordered[i], c2 = ordered[i + 1];
                    if (Vector3.Dot(Vector3.Cross(b2 - a, c2 - a), normal) > 0) t.AddRange(new[] { start, start + i, start + i + 1 });
                    else t.AddRange(new[] { start, start + i + 1, start + i });
                }
            }
            var mesh = new Mesh { name = name };
            mesh.SetVertices(v); mesh.SetNormals(n); mesh.SetUVs(0, uv); mesh.SetTriangles(t, 0);
            mesh.RecalculateBounds(); mesh.RecalculateTangents();
            return mesh;
        }
    }
}
