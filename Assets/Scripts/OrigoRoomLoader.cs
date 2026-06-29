// OrigoRoomLoader.cs  — v1.2, 25 Jun 2026
// Full-3D room from Origo_MeasurementSpec_v1.2.json (StreamingAssets). URP project.
// Renders: shell (extruded walls + floor), 3D furniture (extruded desk/topper + boxes),
//          openings (recessed panels), desktop objects (boxes).
// Materials are INJECTED via Inspector (build-safe) with a URP/Lit runtime fallback.
//
// SETUP:
//   1. Put Origo_MeasurementSpec_v1.2.json in Assets/StreamingAssets/.
//   2. Attach this to an empty GameObject "OrigoRoom".
//   3. Create 5 Material assets (URP/Lit) and drag them into the 5 material slots, OR leave them
//      empty to use the runtime fallback (then add URP/Lit to Project Settings > Graphics >
//      Always Included Shaders so the build keeps it).
//   4. Also attach OrigoFlyCamera.cs to Main Camera.
//
// TEST-CUBE-FIRST: buildFurniture off on first run; confirm cube at door corner + shell shape; then on.

using System;
using System.Collections.Generic;
using UnityEngine;

public class OrigoRoomLoader : MonoBehaviour
{
    [Header("Data source")]
    public string jsonFileName = "Origo_MeasurementSpec_v1.2.json";

    [Header("Build toggles")]
    public bool buildTestCube     = true;
    public bool buildShell        = true;
    public bool buildFurniture    = false;
    public bool buildOpenings     = false;
    public bool buildDesktopObjs  = false;

    [Header("Materials (assign URP/Lit assets; null = runtime fallback)")]
    public Material floorMat;
    public Material wallMat;
    public Material deskMat;
    public Material topperMat;
    public Material boxMat;
    public Material openingMat;

    [Header("Fallback colours (used only if a material slot is null)")]
    public Color floorColor   = new(0.85f, 0.85f, 0.82f);
    public Color wallColor    = new(0.92f, 0.92f, 0.90f);
    public Color deskColor    = new(0.55f, 0.50f, 0.78f);
    public Color topperColor  = new(0.20f, 0.62f, 0.50f);
    public Color boxColor     = new(0.45f, 0.44f, 0.42f);
    public Color openingColor = new(0.55f, 0.75f, 0.95f);

    public float wallThickness = 0.05f;

    OrigoSpec spec;

    void Start()
    {
        if (!LoadSpec()) return;
        if (buildTestCube)    MakeTestCube();
        if (buildShell)       BuildShell();
        if (buildFurniture)   BuildFurniture();
        if (buildOpenings)    BuildOpenings();
        if (buildDesktopObjs) BuildDesktopObjects();
    }

    // ---------- JSON ----------
    bool LoadSpec()
    {
        string path = System.IO.Path.Combine(Application.streamingAssetsPath, jsonFileName);
        if (!System.IO.File.Exists(path)) { Debug.LogError($"[Origo] JSON not found at {path}"); return false; }
        try
        {
            spec = JsonUtility.FromJson<OrigoSpec>(System.IO.File.ReadAllText(path));
            if (spec == null || spec.room == null || spec.room.shell_xz == null)
            { Debug.LogError("[Origo] JSON parsed but room/shell empty."); return false; }
            Debug.Log($"[Origo] Loaded {spec.spec_version}: {spec.room.shell_xz.Length} corners, " +
                      $"{Count(spec.furniture)} furniture, {Count(spec.room.openings)} openings, " +
                      $"{Count(spec.desktop_objects)} desktop objs.");
            return true;
        }
        catch (Exception e) { Debug.LogError($"[Origo] JSON parse failed: {e.Message}"); return false; }
    }
    int Count<T>(T[] a) => a == null ? 0 : a.Length;

    // ---------- material resolve ----------
    Material Resolve(Material assigned, Color fallback)
    {
        if (assigned != null) return assigned;
        var s = Shader.Find("Universal Render Pipeline/Lit");
        if (s == null) s = Shader.Find("Standard"); // editor safety
        return new Material(s) { color = fallback };
    }

    // ---------- test cube ----------
    void MakeTestCube()
    {
        var c = GameObject.CreatePrimitive(PrimitiveType.Cube);
        c.name = "TEST_CUBE_origin"; c.transform.SetParent(transform);
        c.transform.localScale = Vector3.one * 0.2f;
        c.transform.localPosition = new Vector3(0f, 0.1f, 0f);
        c.GetComponent<Renderer>().material = Resolve(null, Color.red);
    }

    // ---------- shell ----------
    void BuildShell()
    {
        var shell = spec.room.shell_xz;
        var xz = new Vector2[shell.Length];
        for (int i = 0; i < shell.Length; i++) xz[i] = new Vector2(shell[i].x, shell[i].z);

        var floor = ExtrudePolygon(xz, 0f, 0f, Resolve(floorMat, floorColor), "Floor"); // flat cap
        floor.transform.SetParent(transform, false);

        var wm = Resolve(wallMat, wallColor);
        for (int i = 0; i < xz.Length; i++)
            MakeWall(xz[i], xz[(i + 1) % xz.Length], spec.room.height, wm, $"Wall_{i}");
    }

    void MakeWall(Vector2 a, Vector2 b, float h, Material m, string name)
    {
        Vector3 a3 = new(a.x, 0, a.y); Vector3 b3 = new(b.x, 0, b.y);
        float len = Vector3.Distance(a3, b3);
        var w = GameObject.CreatePrimitive(PrimitiveType.Cube);
        w.name = name; w.transform.SetParent(transform, false);
        w.transform.localScale = new Vector3(len, h, wallThickness);
        w.transform.localPosition = (a3 + b3) * 0.5f + Vector3.up * h * 0.5f;
        w.transform.localRotation = Quaternion.LookRotation((b3 - a3).normalized, Vector3.up)
                                  * Quaternion.Euler(0, 90, 0);
        w.GetComponent<Renderer>().material = m;
    }

    // ---------- furniture ----------
    void BuildFurniture()
    {
        foreach (var f in spec.furniture)
        {
            Vector2 anchor = ResolveAnchor(f.parent_anchor);
            bool negX = f.anchor_x_runs_toward_origin;

            if (f.type == "polygon")
            {
                var world = new Vector2[f.vertices.Length];
                for (int i = 0; i < f.vertices.Length; i++)
                {
                    float wx = negX ? anchor.x - f.vertices[i].x : anchor.x + f.vertices[i].x;
                    world[i] = new Vector2(wx, anchor.y + f.vertices[i].z);
                }
                bool isDesk = f.object_id == "2D";
                float yTop = f.top_y > 0 ? f.top_y : (f.base_y + f.thickness);
                float yBot = isDesk ? f.carcass_y_min : f.base_y; // desk extrudes floor->top; topper its slab
                var mat = Resolve(isDesk ? deskMat : topperMat, isDesk ? deskColor : topperColor);
                var go = ExtrudePolygon(world, yBot, yTop, mat, $"{f.object_id}_{f.name}");
                go.transform.SetParent(transform, false);
            }
            else MakeBoxFromFurniture(f);
        }
    }

    void MakeBoxFromFurniture(Furniture f)
    {
        Vector2 anchor = ResolveAnchor(f.parent_anchor);
        bool negX = f.anchor_x_runs_toward_origin;
        float xMin = negX ? anchor.x - f.box_x_max : anchor.x + f.box_x_min;
        float xMax = negX ? anchor.x - f.box_x_min : anchor.x + f.box_x_max;
        float zMin = anchor.y + f.box_z_min, zMax = anchor.y + f.box_z_max;
        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = $"{f.object_id}_{f.name}"; cube.transform.SetParent(transform, false);
        cube.transform.localScale = new Vector3(Mathf.Abs(xMax - xMin), f.y_max - f.y_min, Mathf.Abs(zMax - zMin));
        cube.transform.localPosition = new Vector3((xMin + xMax) * 0.5f, (f.y_min + f.y_max) * 0.5f, (zMin + zMax) * 0.5f);
        cube.GetComponent<Renderer>().material = Resolve(boxMat, boxColor);
    }

    // ---------- openings (recessed panels) ----------
    void BuildOpenings()
    {
        if (spec.room.openings == null) return;
        var om = Resolve(openingMat, openingColor);
        foreach (var o in spec.room.openings)
        {
            // Find parent wall endpoints.
            Wall w = FindWall(o.wall);
            if (w == null) continue;
            Vector2 from = CornerXZ(w.from), to = CornerXZ(w.to);
            Vector2 dir = (to - from); float wallLen = dir.magnitude; dir /= wallLen;
            Vector2 normal = new(-dir.y, dir.x); // inward/outward; recess pulls panel into room

            // offset measured from o.offset_from corner. If offset_from == w.to, measure from 'to'.
            float along = o.offset;
            Vector2 basePt = (o.offset_from == w.to) ? to - dir * (along + o.width) : from + dir * along;
            Vector2 centerXZ = basePt + dir * (o.width * 0.5f);
            // recess: push panel toward room interior by recess amount along normal pointing inward.
            // Heuristic: interior is toward room centroid.
            Vector2 centroid = RoomCentroid();
            if (Vector2.Dot(centroid - centerXZ, normal) < 0) normal = -normal;
            centerXZ += normal * o.recess;

            float h = o.head_y - o.sill_y, yc = (o.head_y + o.sill_y) * 0.5f;
            var p = GameObject.CreatePrimitive(PrimitiveType.Cube);
            p.name = $"{o.id}_{o.name}"; p.transform.SetParent(transform, false);
            p.transform.localScale = new Vector3(o.width, h, 0.02f);
            p.transform.localPosition = new Vector3(centerXZ.x, yc, centerXZ.y);
            p.transform.localRotation = Quaternion.LookRotation(new Vector3(normal.x, 0, normal.y), Vector3.up);
            p.GetComponent<Renderer>().material = om;
        }
    }

    // ---------- desktop objects ----------
    void BuildDesktopObjects()
    {
        if (spec.desktop_objects == null) return;
        foreach (var d in spec.desktop_objects)
        {
            // Parent frame: desk/topper resolve to P5 corner with negX; wall F resolves along F; mouse mat to its own pos.
            Vector2 anchor; bool negX = true;
            switch (d.parent)
            {
                case "2D": case "2E": anchor = ResolveAnchor("P5"); negX = true;  break;
                case "4F": anchor = ResolveAnchor("P5"); negX = true;  break; // wall F runs P5->P0
                case "1G": anchor = ResolveAnchor("P5"); negX = true;  break; // approx: on desk
                default:   anchor = Vector2.zero; negX = false; break;
            }
            float wx = negX ? anchor.x - d.pos_x : anchor.x + d.pos_x;
            float wz = anchor.y + d.pos_z;
            float yc;
            if (d.parent == "4F") yc = 1.43f; // 1b wall screen mount height (assumed; TBD)
            else if (d.parent == "2E") yc = 0.170f + d.height * 0.5f; // on topper
            else yc = 0.740f + d.height * 0.5f; // on desk
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = $"{d.object_id}_{d.name}"; cube.transform.SetParent(transform, false);
            cube.transform.localScale = new Vector3(d.width, d.height, d.depth);
            cube.transform.localPosition = new Vector3(wx, yc, wz);
            cube.transform.localRotation = Quaternion.Euler(0, d.rotation_y, 0);
            cube.GetComponent<Renderer>().material = Resolve(boxMat, boxColor);
        }
    }

    // ---------- helpers ----------
    Wall FindWall(string id) { foreach (var w in spec.room.walls) if (w.id == id) return w; return null; }
    Vector2 CornerXZ(string corner) { foreach (var c in spec.room.shell_xz) if (c.corner == corner) return new Vector2(c.x, c.z); return Vector2.zero; }
    Vector2 RoomCentroid()
    {
        Vector2 s = Vector2.zero; foreach (var c in spec.room.shell_xz) s += new Vector2(c.x, c.z);
        return s / spec.room.shell_xz.Length;
    }

    Vector2 ResolveAnchor(string a)
    {
        if (string.IsNullOrEmpty(a)) return Vector2.zero;
        foreach (var c in spec.room.shell_xz) if (c.corner == a) return new Vector2(c.x, c.z);
        switch (a)
        {
            case "room_origin": return Vector2.zero;
            case "desk_V3":     return ResolveAnchor("P5");
            case "shelf_8A":    return new Vector2(0.935f, 2.475f);
            default:            return Vector2.zero;
        }
    }

    // Extrude an XZ polygon from yBottom to yTop. If yTop==yBottom, builds a single flat cap (floor).
    GameObject ExtrudePolygon(Vector2[] xz, float yBottom, float yTop, Material mat, string name)
    {
        var go = new GameObject(name);
        var mf = go.AddComponent<MeshFilter>();
        var mr = go.AddComponent<MeshRenderer>();
        var mesh = new Mesh { name = name + "_mesh" };
        int n = xz.Length;
        bool flat = Mathf.Abs(yTop - yBottom) < 1e-5f;

        var verts = new List<Vector3>();
        var tris  = new List<int>();

        if (flat)
        {
            for (int i = 0; i < n; i++) verts.Add(new Vector3(xz[i].x, yTop, xz[i].y));
            for (int i = 1; i < n - 1; i++) { tris.Add(0); tris.Add(i); tris.Add(i + 1); }
        }
        else
        {
            // top cap
            int topStart = verts.Count;
            for (int i = 0; i < n; i++) verts.Add(new Vector3(xz[i].x, yTop, xz[i].y));
            for (int i = 1; i < n - 1; i++) { tris.Add(topStart); tris.Add(topStart + i); tris.Add(topStart + i + 1); }
            // bottom cap (reverse winding)
            int botStart = verts.Count;
            for (int i = 0; i < n; i++) verts.Add(new Vector3(xz[i].x, yBottom, xz[i].y));
            for (int i = 1; i < n - 1; i++) { tris.Add(botStart); tris.Add(botStart + i + 1); tris.Add(botStart + i); }
            // sides (two tris per edge)
            for (int i = 0; i < n; i++)
            {
                int a = i, b = (i + 1) % n;
                Vector3 ta = new(xz[a].x, yTop, xz[a].y), tb = new(xz[b].x, yTop, xz[b].y);
                Vector3 ba = new(xz[a].x, yBottom, xz[a].y), bb = new(xz[b].x, yBottom, xz[b].y);
                int s = verts.Count;
                verts.Add(ta); verts.Add(tb); verts.Add(bb); verts.Add(ba);
                tris.Add(s); tris.Add(s + 1); tris.Add(s + 2);
                tris.Add(s); tris.Add(s + 2); tris.Add(s + 3);
            }
        }

        mesh.vertices = verts.ToArray();
        mesh.triangles = tris.ToArray();
        mesh.RecalculateNormals();
        mf.mesh = mesh;
        mr.material = mat;
        return go;
    }

    // ---------- JSON model ----------
    [Serializable] public class OrigoSpec {
        public string schema_version, spec_version, date;
        public Room room; public Furniture[] furniture; public DesktopObj[] desktop_objects;
    }
    [Serializable] public class Room {
        public string name; public float height;
        public ShellCorner[] shell_xz; public Wall[] walls; public Opening[] openings;
    }
    [Serializable] public class ShellCorner { public string corner; public float x, z; public string note; }
    [Serializable] public class Wall { public string id, from, to; public float length; public string method, note; }
    [Serializable] public class Opening {
        public string id, name, wall, offset_from; public float offset, width, sill_y, head_y, recess; public string method, note;
    }
    [Serializable] public class Furniture {
        public string object_id, name, type, parent, parent_anchor;
        public bool anchor_x_runs_toward_origin;
        public float top_y, base_y, thickness, carcass_y_min;
        public float box_x_min, box_x_max, box_z_min, box_z_max, y_min, y_max;
        public Vertex[] vertices;
    }
    [Serializable] public class Vertex { public string id; public float x, z; public string note; }
    [Serializable] public class DesktopObj {
        public string object_id, name, parent;
        public float pos_x, pos_z, width, depth, height, rotation_y;
        public string pos_method, dims_method, note;
    }
}
