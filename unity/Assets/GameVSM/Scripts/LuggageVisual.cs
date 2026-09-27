using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace GameVSM
{
    public enum LuggageKind { RollingCase, Holdall, TaskCase }

    // Metre-scaled visual props. The three envelopes come from the existing game,
    // not from a claimed manufacturer specification. Movement belongs to the actor.
    public sealed class LuggageVisual : MonoBehaviour
    {
        [SerializeField] LuggageKind kind;
        [SerializeField] Transform grip, ground, stow;
        [SerializeField] MeshFilter near, far;
        [SerializeField] Bounds localBounds;
        public LuggageKind Kind => kind;
        public Transform Grip => grip;
        public Transform Ground => ground;
        public Transform Stow => stow;
        public Bounds LocalBounds => localBounds;

        sealed class Geometry
        {
            public Mesh Near, Far;
            public Material[] Materials;
        }
        static readonly Dictionary<LuggageKind, Geometry> cache = new();
        static Material trim, metal;

        void Awake()
        {
            // A saved passenger prefab seeds the same cache used by runtime props.
            // No renderer/material clones are created for each passenger.
            if (near != null && far != null && near.sharedMesh != null && far.sharedMesh != null)
                cache[kind] = new Geometry { Near = near.sharedMesh, Far = far.sharedMesh,
                    Materials = near.GetComponent<Renderer>().sharedMaterials };
        }

        public static LuggageVisual CreateCarryBag(Transform parent) => Create(parent, LuggageKind.Holdall);
        public static LuggageVisual CreateTaskCase(Transform parent) => Create(parent, LuggageKind.TaskCase);
        public static LuggageVisual CreateRollingCase(Transform parent) => Create(parent, LuggageKind.RollingCase);

        public static LuggageVisual Create(Transform parent, LuggageKind kind)
        {
            var go = new GameObject(kind == LuggageKind.Holdall ? "Дорожная сумка" : "Чемодан");
            go.transform.SetParent(parent, false);
            var visual = go.AddComponent<LuggageVisual>(); visual.kind = kind;
            var geometry = GetGeometry(kind);
            visual.near = Render(go.transform, "LOD0", geometry.Near, geometry.Materials, true);
            visual.far = Render(go.transform, "LOD1", geometry.Far, geometry.Materials, false);
            visual.localBounds = geometry.Near.bounds; visual.localBounds.Encapsulate(geometry.Far.bounds);
            var lod = go.AddComponent<LODGroup>();
            lod.SetLODs(new[] {
                new LOD(.08f, new[] { visual.near.GetComponent<Renderer>() }),
                new LOD(.009f, new[] { visual.far.GetComponent<Renderer>() })
            });
            lod.RecalculateBounds();
            switch (kind)
            {
                case LuggageKind.RollingCase:
                    visual.grip = Anchor(go.transform, "Grip", new Vector3(0, .95f, 0));
                    visual.ground = Anchor(go.transform, "Ground", new Vector3(0, .015f, 0));
                    visual.stow = Anchor(go.transform, "Stow", new Vector3(-.11f, .31f, 0));
                    break;
                case LuggageKind.Holdall:
                    visual.grip = Anchor(go.transform, "Grip", new Vector3(0, -.01f, 0));
                    visual.ground = Anchor(go.transform, "Ground", new Vector3(0, -.32f, 0));
                    visual.stow = Anchor(go.transform, "Stow", new Vector3(-.07f, -.20f, 0));
                    break;
                default:
                    visual.grip = Anchor(go.transform, "Grip", new Vector3(0, .505f, 0));
                    visual.ground = Anchor(go.transform, "Ground", Vector3.zero);
                    visual.stow = Anchor(go.transform, "Stow", new Vector3(-.17f, .25f, 0));
                    break;
            }
            return visual;
        }

        static Transform Anchor(Transform parent, string name, Vector3 position)
        {
            var anchor = new GameObject(name).transform;
            anchor.SetParent(parent, false); anchor.localPosition = position; return anchor;
        }
        static MeshFilter Render(Transform parent, string name, Mesh mesh, Material[] materials, bool shadow)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            var filter = go.GetComponent<MeshFilter>(); filter.sharedMesh = mesh;
            var renderer = go.GetComponent<MeshRenderer>(); renderer.sharedMaterials = materials;
            renderer.shadowCastingMode = shadow ? ShadowCastingMode.On : ShadowCastingMode.Off;
            return filter;
        }
        static Geometry GetGeometry(LuggageKind kind)
        {
            if (cache.TryGetValue(kind, out var found) && found.Near != null && found.Far != null &&
                Array.TrueForAll(found.Materials, m => m != null)) return found;
            if (trim == null) trim = PropMeshBuilder.Material("Luggage_Trim", new Color(.045f, .05f, .057f), .28f);
            if (metal == null) metal = PropMeshBuilder.Material("Luggage_Metal", new Color(.45f, .49f, .52f), .5f, .78f);
            var color = kind == LuggageKind.TaskCase ? new Color(.2f, .32f, .42f) :
                kind == LuggageKind.Holdall ? new Color(.16f, .17f, .19f) : new Color(.10f, .13f, .16f);
            var result = new Geometry {
                Near = Build(kind, true), Far = Build(kind, false),
                Materials = new[] { PropMeshBuilder.Material("Luggage_" + kind, color, kind == LuggageKind.Holdall ? .17f : .35f), trim, metal }
            };
            // Hardware is kept inside the old envelope, especially the task case:
            // its 90-degree stow pose must still clear the existing upper shelf.
            var envelope = kind == LuggageKind.TaskCase ? new Bounds(new Vector3(0, .26375f, 0), new Vector3(.34f, .5275f, .2f)) :
                kind == LuggageKind.Holdall ? new Bounds(new Vector3(0, -.16f, 0), new Vector3(.14f, .32f, .4f)) :
                new Bounds(new Vector3(0, .49125f, 0), new Vector3(.22f, .9525f, .38f));
            envelope.Expand(.0002f);
            if (!envelope.Contains(result.Near.bounds.min) || !envelope.Contains(result.Near.bounds.max) ||
                !envelope.Contains(result.Far.bounds.min) || !envelope.Contains(result.Far.bounds.max))
                throw new InvalidOperationException("Luggage geometry exceeds the existing " + kind + " envelope.");
            return cache[kind] = result;
        }

        static Mesh Build(LuggageKind kind, bool detail)
        {
            var b = new PropMeshBuilder(); int round = detail ? 2 : 1, sides = detail ? 12 : 8;
            if (kind == LuggageKind.Holdall)
            {
                b.RoundedBox(new Vector3(0, -.20f, 0), new Vector3(.14f, .24f, .4f), .035f, 0, round);
                // Two flexible handles leave an actual opening above the soft bag.
                foreach (float x in new[] { -.03f, .03f })
                    b.Path(new[] { new Vector3(x, -.10f, -.12f), new Vector3(x, -.035f, -.07f),
                        new Vector3(x, -.01f, -.045f), new Vector3(x, -.01f, .045f),
                        new Vector3(x, -.035f, .07f), new Vector3(x, -.10f, .12f) }, .009f, 1, sides);
                if (detail)
                {
                    b.Tube(new Vector3(0, -.081f, -.145f), new Vector3(0, -.081f, .145f), .001f, 1, 6);
                    b.RoundedBox(new Vector3(.005f, -.077f, .07f), new Vector3(.011f, .008f, .026f), .003f, 2, 1);
                    foreach (float x in new[] { -.066f, .066f })
                        b.Path(new[] { new Vector3(x, -.28f, -.13f), new Vector3(x, -.115f, -.13f),
                            new Vector3(x, -.115f, .13f), new Vector3(x, -.28f, .13f) }, .002f, 1, 6);
                }
            }
            else
            {
                bool rolling = kind == LuggageKind.RollingCase;
                var center = new Vector3(0, rolling ? .31f : .25f, 0);
                var size = rolling ? new Vector3(.22f, .5f, .38f) : new Vector3(.34f, .44f, .2f);
                float radius = rolling ? .032f : .025f;
                b.RoundedBox(center, detail ? size - Vector3.one * .004f : size, radius, 0, round);
                if (detail)
                {
                    // The moulded split and ribs give highlights at close range without decals.
                    b.Band(center, rolling ? new Vector2(size.z, size.y) : new Vector2(size.x, size.y),
                        radius, .007f, rolling ? 0 : 2, 1, 3);
                    foreach (float sign in new[] { -1f, 1f })
                        foreach (float across in new[] { -.105f, -.035f, .035f, .105f })
                        {
                            var a = rolling ? new Vector3(sign * .108f, .15f, across) : new Vector3(across, .13f, sign * .098f);
                            var c = a + Vector3.up * (rolling ? .32f : .24f);
                            b.Tube(a, c, .002f, 0, 6);
                        }
                }
                float wheelY = rolling ? .055f : .025f, wheelRadius = rolling ? .04f : .025f;
                foreach (float x in rolling ? new[] { -.075f, .075f } : new[] { -.125f, .125f })
                    foreach (float z in rolling ? new[] { -.14f, .14f } : new[] { -.07f, .07f })
                    {
                        b.Tube(new Vector3(x - .012f, wheelY, z), new Vector3(x + .012f, wheelY, z), wheelRadius, 1, sides);
                        if (detail) b.Tube(new Vector3(x - .013f, wheelY, z), new Vector3(x + .013f, wheelY, z), wheelRadius * .4f, 2, 8);
                    }
                if (rolling)
                {
                    foreach (float z in new[] { -.14f, .14f })
                        b.Tube(new Vector3(0, .495f, z), new Vector3(0, .95f, z), .007f, 2, sides);
                    b.Tube(new Vector3(0, .95f, -.15f), new Vector3(0, .95f, .15f), .0175f, 1, sides);
                    if (detail) b.Path(new[] { new Vector3(.101f, .25f, -.035f), new Vector3(.103f, .25f, .035f) }, .006f, 1, 8);
                }
                else
                    b.Path(new[] { new Vector3(-.075f, .46f, 0), new Vector3(-.075f, .485f, 0),
                        new Vector3(-.063f, .505f, 0), new Vector3(.063f, .505f, 0),
                        new Vector3(.075f, .485f, 0), new Vector3(.075f, .46f, 0) }, .011f, 1, sides);
            }
            return b.Build("Luggage_" + kind + (detail ? "_LOD0" : "_LOD1"));
        }
    }

    // Shared by the small carried props. Meshes are built once per preset, not per frame.
    internal sealed class PropMeshBuilder
    {
        readonly List<Vector3> vertices = new(), normals = new();
        readonly List<Vector2> uv = new();
        readonly List<int>[] indices = { new(), new(), new() };

        internal static Material Material(string name, Color color, float smoothness, float metallic = 0)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("Carried props require the project's URP Lit shader.");
            var material = new Material(shader) { name = name, enableInstancing = true };
            material.SetColor("_BaseColor", color); material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Metallic", metallic); return material;
        }

        int Vertex(Vector3 p, Vector3 n)
        {
            int result = vertices.Count; vertices.Add(p); normals.Add(n);
            uv.Add(Mathf.Abs(n.y) > .5f ? new Vector2(p.x, p.z) : Mathf.Abs(n.x) > .5f ? new Vector2(p.z, p.y) : new Vector2(p.x, p.y));
            return result;
        }
        void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 na, Vector3 nb, Vector3 nc, Vector3 nd, int material)
        {
            int first = Vertex(a, na); Vertex(b, nb); Vertex(c, nc); Vertex(d, nd);
            bool forward = Vector3.Dot(Vector3.Cross(b - a, c - a), na + nb + nc + nd) > 0;
            if (forward) indices[material].AddRange(new[] { first, first + 1, first + 2, first, first + 2, first + 3 });
            else indices[material].AddRange(new[] { first, first + 2, first + 1, first, first + 3, first + 2 });
        }
        void Triangle(Vector3 a, Vector3 b, Vector3 c, Vector3 normal, int material)
        {
            int first = Vertex(a, normal); Vertex(b, normal); Vertex(c, normal);
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), normal) > 0) indices[material].AddRange(new[] { first, first + 1, first + 2 });
            else indices[material].AddRange(new[] { first, first + 2, first + 1 });
        }
        static float[] Coordinates(float half, float radius, int segments)
        {
            var result = new List<float>(); float core = half - radius;
            for (int i = segments; i >= 0; i--) result.Add(-core - radius * Mathf.Tan(i * Mathf.PI / (4 * segments)));
            for (int i = 0; i <= segments; i++) result.Add(core + radius * Mathf.Tan(i * Mathf.PI / (4 * segments)));
            return result.ToArray();
        }
        internal void RoundedBox(Vector3 center, Vector3 size, float radius, int material, int segments = 2)
        {
            Vector3 half = size * .5f;
            radius = Mathf.Min(Mathf.Max(radius, .000001f), Mathf.Min(half.x, Mathf.Min(half.y, half.z)) * .9f);
            var core = half - Vector3.one * radius;
            for (int axis = 0; axis < 3; axis++) foreach (int sign in new[] { -1, 1 })
            {
                int u = (axis + 1) % 3, v = (axis + 2) % 3;
                var us = Coordinates(half[u], radius, segments); var vs = Coordinates(half[v], radius, segments);
                Vector3 Point(float a, float b, out Vector3 normal)
                {
                    var p = Vector3.zero; p[axis] = sign * half[axis]; p[u] = a; p[v] = b;
                    var inside = new Vector3(Mathf.Clamp(p.x, -core.x, core.x), Mathf.Clamp(p.y, -core.y, core.y), Mathf.Clamp(p.z, -core.z, core.z));
                    normal = (p - inside).normalized; return center + inside + normal * radius;
                }
                for (int i = 0; i < us.Length - 1; i++) for (int j = 0; j < vs.Length - 1; j++)
                {
                    var a = Point(us[i], vs[j], out var na); var b = Point(us[i + 1], vs[j], out var nb);
                    var c = Point(us[i + 1], vs[j + 1], out var nc); var d = Point(us[i], vs[j + 1], out var nd);
                    Quad(a, b, c, d, na, nb, nc, nd, material);
                }
            }
        }
        internal void Box(Vector3 center, Vector3 size, int material)
        {
            var half = size * .5f;
            for (int axis = 0; axis < 3; axis++) foreach (int sign in new[] { -1, 1 })
            {
                int u = (axis + 1) % 3, v = (axis + 2) % 3;
                var normal = Vector3.zero; normal[axis] = sign;
                Vector3 P(int a, int b)
                { var p = center; p[axis] += sign * half[axis]; p[u] += a * half[u]; p[v] += b * half[v]; return p; }
                Quad(P(-1, -1), P(1, -1), P(1, 1), P(-1, 1), normal, normal, normal, normal, material);
            }
        }
        internal void Tube(Vector3 from, Vector3 to, float radius, int material, int sides = 10)
        {
            var axis = (to - from).normalized;
            var u = Vector3.Cross(axis, Mathf.Abs(axis.y) < .9f ? Vector3.up : Vector3.right).normalized;
            var v = Vector3.Cross(axis, u);
            for (int i = 0; i < sides; i++)
            {
                float a = i * Mathf.PI * 2 / sides, b = (i + 1) * Mathf.PI * 2 / sides;
                var na = u * Mathf.Cos(a) + v * Mathf.Sin(a); var nb = u * Mathf.Cos(b) + v * Mathf.Sin(b);
                var p = from + na * radius; var q = from + nb * radius;
                var r = to + nb * radius; var s = to + na * radius;
                Quad(p, q, r, s, na, nb, nb, na, material);
                Triangle(from, q, p, -axis, material); Triangle(to, s, r, axis, material);
            }
        }
        internal void Path(Vector3[] points, float radius, int material, int sides = 10)
        { for (int i = 1; i < points.Length; i++) Tube(points[i - 1], points[i], radius, material, sides); }

        internal void Band(Vector3 center, Vector2 size, float radius, float thickness, int axis, int material, int segments)
        {
            var loop = new List<Vector2>(); var half = size * .5f;
            var centers = new[] { new Vector2(half.x - radius, half.y - radius), new Vector2(-half.x + radius, half.y - radius),
                new Vector2(-half.x + radius, -half.y + radius), new Vector2(half.x - radius, -half.y + radius) };
            for (int corner = 0; corner < 4; corner++) for (int step = 0; step <= segments; step++)
            {
                float angle = (corner + (float)step / segments) * Mathf.PI / 2;
                loop.Add(centers[corner] + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
            }
            Vector3 P(Vector2 point, float depth) => center + (axis == 0 ? new Vector3(depth, point.y, point.x) : new Vector3(point.x, point.y, depth));
            for (int i = 0; i < loop.Count; i++)
            {
                var a = loop[i]; var b = loop[(i + 1) % loop.Count]; var tangent = (b - a).normalized;
                var outward = new Vector2(tangent.y, -tangent.x);
                var normal = axis == 0 ? new Vector3(0, outward.y, outward.x) : new Vector3(outward.x, outward.y, 0);
                Quad(P(a, -thickness * .5f), P(b, -thickness * .5f), P(b, thickness * .5f), P(a, thickness * .5f), normal, normal, normal, normal, material);
            }
        }
        internal Mesh Build(string name)
        {
            var mesh = new Mesh { name = name };
            mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.SetUVs(0, uv); mesh.subMeshCount = indices.Length;
            for (int i = 0; i < indices.Length; i++) mesh.SetTriangles(indices[i], i);
            mesh.RecalculateBounds(); mesh.RecalculateTangents(); return mesh;
        }
    }
}
