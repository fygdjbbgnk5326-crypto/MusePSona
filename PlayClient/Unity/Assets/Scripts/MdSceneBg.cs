using System;
using System.Collections.Generic;
using System.IO;
using MusePSona.Play;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>对局背景：gamescene scene_XX 分层 + Animator 循环平移。素材仅本机。</summary>
public static class MdSceneBg
{
    [Serializable]
    public class Layout
    {
        public string scene;
        public Group[] groups;
        public Item[] items;
        public Spr[] sprites;
        public SpineInst[] spines;
        public PopMove[] moves;
        public ParticleInst[] particles;
        public float movePeriod;
        public float[] moveT, moveX, moveY;
        public int moveStep;
        public float movePeriod2;
        public float[] moveT2, moveX2, moveY2;
        public float spinPeriod;
        public float[] spinT, spinZ;
    }

    [Serializable]
    public class PopMove
    {
        public float period;
        public float[] ts, xs, ys;
    }

    [Serializable]
    public class Group
    {
        public string name;
        public float x, y, loop, period;
        public float loopW, periodS, phase;
        public int belt;
        public string mode; // "once" = 单次行程动画（如 scene_08 的 08_ice），默认 "scroll" = 循环滚动
        public Item[] items;
    }

    [Serializable]
    public class Item
    {
        public string spr;
        public float x, y, w, h, r, g, b, a;
        public int order;
        public int gi;
        public bool flipX, flipY;
        public float topW;
        public float sliceL, sliceR, capL, capR;
        public float rotZ;
        public float bobAmp, bobPeriod, bobPhase, blinkPeriod;
        public float scrollLoop, scrollPeriod, scrollY, scrollAngle;
        public float mx, my, mw, mh;
        public float popAt, popPeriod, popOff, popDelay;
        public int moveId;
        public int spinId;
        public int orbit;
        public float pivotX, pivotY;
        public int band;
        public int spinFlip;
    }

    [Serializable]
    public class SpineInst
    {
        public string pack, clip;
        public float x, y, sx, sy;
        public int order, gi;
    }

    [Serializable]
    public class ParticleInst
    {
        public string tex;
        public float x, y, rotZ;
        public int gi, order;
        public float dur, delay;
        public float rate, burstMin, burstMax;
        public float life, life2, speed, speed2, size, size2, grav;
        public int shape;
        public float radius, shapeSX;
        public float cr, cg, cb, ca;
        public float lr, lg, lb, la;
        public int colorLife, sizeLife, noise;
        public float noiseStr, noiseFreq;
        public int maxP;
    }

    [Serializable]
    public class Spr
    {
        public string name, file;
        public int w, h;
    }

    public sealed class View
    {
        public Transform Root;
        public readonly List<Layer> Layers = new();
        public readonly List<Texture2D> Textures = new();
        public readonly List<Prop> Props = new();
        public readonly List<Mover> Movers = new();
        public readonly List<Clipper> Clippers = new();
        public readonly List<Popper> Poppers = new();
        public readonly List<Follower> Followers = new();
        public readonly List<Spinner> Spinners = new();
        public float SpinPeriod;
        public float[] SpinT, SpinZ;
        public readonly List<BandBar> Bands = new();
        public PopMove[] Moves;
        public float MovePeriod;
        public float[] MoveT, MoveX, MoveY;
        public bool MoveStep;
        public float MovePeriod2;
        public float[] MoveT2, MoveX2, MoveY2;
        public float WorldScale = 1f;
        public float ScrollY;
        public bool BeltReady;
        public float BeltOrigin;
        public float BeltChart;
        public float BeltHeld;
    }

    public sealed class Mover
    {
        public Transform T;
        public Renderer Rend;
        public Vector3 Base;
        public Color Tint;
        public float BobAmp, BobPeriod, BobPhase, BlinkPeriod;
        public float ScrollLoop, ScrollPeriod, ScrollY, ScrollAngle;
        public int MoveId;
    }

    public sealed class Follower
    {
        public Transform T;
        public Vector3 Base;
        public int MoveId;
    }

    public sealed class Spinner
    {
        public Transform T;
        public float Base;
        public int Channel;
        public bool Orbit;
        public Vector3 Pivot;
        public Vector3 Offset;
        public bool FlipSpin;
    }

    public sealed class BandBar
    {
        public Material Mat;
        public int Band;
        public float Clip;
    }

    // 水平镜面：轴在方块墙中线。亮带贴彩虹实心条两侧，宽度相同。
    // 一格约 0.0334 UV。安静 3 格，最响约 8 格，外沿对齐整格，避免只露出半格。
    const int BandCount = 32;
    const int SpectrumSamples = 512;
    const float BandPower = 100f;
    const float BandUp = 0.004f;
    const float BandDown = 1.35f;
    const float BandGain = 1.117f;
    const float ClipMin = 0.268f;
    const float ClipLerp = 0.32f;
    const float ClipAxis = 0.5f;
    const float ClipInner = 0.0403f;
    const float ClipCell = 0.0334f;
    const float ClipCellsMin = 3f;
    const float ClipCellsMax = 8f;
    static readonly float[] BlockTop =
    {
        0.0280f, 0.0613f, 0.0946f, 0.1278f, 0.1611f, 0.1944f, 0.2277f, 0.2627f, 0.2960f, 0.3292f,
        0.3625f, 0.3958f, 0.4291f, 0.4623f, 0.4956f, 0.5306f, 0.5639f, 0.5972f, 0.6305f, 0.6637f,
        0.6970f, 0.7303f, 0.7653f, 0.7986f, 0.8319f, 0.8651f, 0.8984f, 0.9317f, 0.9650f, 0.9982f
    };
    static readonly float[] BandSample = new float[SpectrumSamples];
    static readonly float[] BandFreq = new float[BandCount];
    static readonly float[] BandBuf = new float[BandCount];
    static readonly float[] BandDec = new float[BandCount];
    static readonly float[] BandHigh = new float[BandCount];

    public sealed class Clipper
    {
        public Material Mat;
        public Transform Layer;
        public float LocalX, LocalY, HalfW, HalfH;
        public int MoveId;
    }

    public sealed class Popper
    {
        public GameObject Go;
        public float At, Off, Period, Delay;
    }

    public sealed class Prop
    {
        public MdSpineView Spine;
        public string Clip;
    }

    public sealed class Layer
    {
        public Transform Root;
        public float Loop;
        public float Period;
        public float Speed;
        public float BaseX;
        public float Shift;
        public float Phase;
        public bool AnchorZero; // 开局位移是 0，播完一圈再接回开头
        public string Mode; // "once" = 单次行程，默认 "scroll"
    }

    public const float ScreenScrollSpeed = 1.6f;

    static bool ConveyorName(string nm)
    {
        if (string.IsNullOrEmpty(nm)) return false;
        nm = nm.ToLowerInvariant();
        if (nm.Contains("color")) return false;
        return nm.Contains("floor") || nm.Contains("near") || nm.Contains("mid") || nm.Contains("road");
    }

    static bool Skip08MidCopy(string nm)
        => nm == "Mid_A_0" || nm == "Mid_A_all" || nm == "Mid_B_1" || nm == "Mid_B_all";

    static void SpriteCopyRange(string scene, string nm, float loop, Item[] items, out int k0, out int k1)
    {
        k0 = 0;
        k1 = 0;
        if (loop <= 0.05f) return;
        if (scene == "scene_08" && Skip08MidCopy(nm)) return;
        k1 = 1;
        if (scene != "scene_08") k0 = -1;
        const float viewHalf = 8f;
        var leftEdge = float.PositiveInfinity;
        var rightEdge = float.NegativeInfinity;
        if (items != null)
        {
            foreach (var it in items)
            {
                if (it == null) continue;
                var hw = Mathf.Max(0.01f, it.w) * 0.5f;
                leftEdge = Mathf.Min(leftEdge, it.x - hw);
                rightEdge = Mathf.Max(rightEdge, it.x + hw);
            }
        }
        if (leftEdge >= rightEdge) return;
        if (scene != "scene_08")
        {
            while (k0 > -16 && leftEdge + (k0 + 1) * loop > -viewHalf)
                k0--;
        }
        while (k1 < 16 && rightEdge + k1 * loop < viewHalf)
            k1++;
    }

    static void LoopCopyRange(string scene, float loopW, float worldScale, bool particles, out int kMin, out int kMax)
    {
        kMin = 0;
        kMax = 0;
        if (loopW <= 0.05f) return;
        if (scene == "scene_08") return;
        if (particles && scene == "scene_07") return;
        var extra = Mathf.Max(1, Mathf.CeilToInt(8f * worldScale / loopW));
        kMin = -extra;
        kMax = extra;
    }

    static float BeltFromItems(Item[] items)
    {
        if (items == null || items.Length < 4) return 0f;
        var xs = new List<float>();
        foreach (var it in items)
        {
            if (it == null) continue;
            if (it.spr != null && (it.spr.IndexOf("floor", StringComparison.OrdinalIgnoreCase) >= 0
                || it.spr.StartsWith("road", StringComparison.OrdinalIgnoreCase)))
                xs.Add(it.x);
        }
        if (xs.Count < 4)
        {
            xs.Clear();
            foreach (var it in items)
                if (it != null) xs.Add(it.x);
        }
        if (xs.Count < 4) return 0f;
        xs.Sort();
        var diffs = new List<float>();
        for (var i = 1; i < xs.Count; i++)
        {
            var d = xs[i] - xs[i - 1];
            if (d >= 0.35f && d <= 12f) diffs.Add(Mathf.Round(d * 1000f) / 1000f);
        }
        if (diffs.Count < 3) return Mathf.Clamp(xs[xs.Count - 1] - xs[0], 6f, 24f);
        diffs.Sort();
        return Mathf.Max(0.5f, diffs[diffs.Count / 2]);
    }

    static string _layoutScene = "";
    static long _layoutWrite;

    static string LayoutPath(string scene)
    {
        return Path.Combine(Application.streamingAssetsPath, "MdLooks", "sprites", scene, "bg", "layout.json");
    }

    public static void InvalidateLayout()
    {
        _layoutScene = "";
        _layoutWrite = 0;
    }

    public static bool LayoutStale(string scene)
    {
        scene = SceneTimeline.Norm(scene);
        if (scene.Length == 0) scene = "scene_01";
        var path = LayoutPath(scene);
        if (!File.Exists(path)) return false;
        var write = File.GetLastWriteTimeUtc(path).Ticks;
        return scene != _layoutScene || write != _layoutWrite;
    }

    public static Layout Load(string scene)
    {
        scene = SceneTimeline.Norm(scene);
        if (scene.Length == 0) scene = "scene_01";
        var path = LayoutPath(scene);
        if (!File.Exists(path))
        {
            Debug.LogWarning("背景没有 layout " + path);
            return null;
        }
        try
        {
            _layoutScene = scene;
            _layoutWrite = File.GetLastWriteTimeUtc(path).Ticks;
            var layout = JsonUtility.FromJson<Layout>(File.ReadAllText(path));
            if (layout == null) return null;
            BindItems(layout);
            if (layout.groups == null || layout.groups.Length == 0) return null;
            var n = 0;
            foreach (var g in layout.groups)
                if (g?.items != null) n += g.items.Length;
            if (n == 0 && (layout.spines == null || layout.spines.Length == 0)
                && (layout.particles == null || layout.particles.Length == 0))
            {
                Debug.LogWarning("背景 layout 没有片 " + scene);
                return null;
            }
            return layout;
        }
        catch (Exception ex)
        {
            Debug.LogWarning("背景 layout 读失败 " + scene + " " + ex.Message);
            return null;
        }
    }

    static Mesh Slice3Mesh(float w, float h, float capL, float capR, float uL, float uR)
    {
        // 官机 SpriteDrawMode.Sliced：左右端帽保持原图像素，中间横切。本地空间仍是 -0.5~0.5 的四边形。
        var fl = Mathf.Clamp(capL / Mathf.Max(w, 0.01f), 0.01f, 0.49f);
        var fr = Mathf.Clamp(capR / Mathf.Max(w, 0.01f), 0.01f, 0.49f);
        if (fl + fr > 0.95f)
        {
            fl = 0.45f;
            fr = 0.45f;
        }
        uL = Mathf.Clamp01(uL);
        uR = Mathf.Clamp01(uR);
        var xs = new[] { -0.5f, -0.5f + fl, 0.5f - fr, 0.5f };
        var us = new[] { 0f, uL, 1f - uR, 1f };
        var verts = new Vector3[8];
        var uv = new Vector2[8];
        for (var i = 0; i < 4; i++)
        {
            verts[i] = new Vector3(xs[i], -0.5f, 0f);
            verts[i + 4] = new Vector3(xs[i], 0.5f, 0f);
            uv[i] = new Vector2(us[i], 0f);
            uv[i + 4] = new Vector2(us[i], 1f);
        }
        var tris = new int[18];
        var t = 0;
        for (var i = 0; i < 3; i++)
        {
            tris[t++] = i;
            tris[t++] = i + 4;
            tris[t++] = i + 1;
            tris[t++] = i + 1;
            tris[t++] = i + 4;
            tris[t++] = i + 5;
        }
        var mesh = new Mesh { name = "Slice3" };
        mesh.vertices = verts;
        mesh.uv = uv;
        mesh.triangles = tris;
        mesh.RecalculateBounds();
        return mesh;
    }

    static Mesh TrapezoidMesh(float w, float h, float topW)
    {
        var hw = Mathf.Max(0.01f, w) * 0.5f;
        var hh = Mathf.Max(0.01f, h) * 0.5f;
        var tw = Mathf.Clamp(topW, 0.05f, w) * 0.5f;
        var mesh = new Mesh { name = "Trapezoid" };
        mesh.vertices = new[]
        {
            new Vector3(-hw, -hh, 0f),
            new Vector3(hw, -hh, 0f),
            new Vector3(tw, hh, 0f),
            new Vector3(-tw, hh, 0f)
        };
        mesh.uv = new[]
        {
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(1f, 1f),
            new Vector2(0f, 1f)
        };
        mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
        mesh.RecalculateBounds();
        return mesh;
    }

    static void BindItems(Layout layout)
    {
        if (layout.groups == null || layout.items == null || layout.items.Length == 0) return;
        var buckets = new List<Item>[layout.groups.Length];
        for (var i = 0; i < buckets.Length; i++) buckets[i] = new List<Item>();
        foreach (var it in layout.items)
        {
            if (it == null || it.gi < 0 || it.gi >= buckets.Length) continue;
            buckets[it.gi].Add(it);
        }
        for (var i = 0; i < layout.groups.Length; i++)
        {
            if (layout.groups[i] == null) continue;
            layout.groups[i].items = buckets[i].ToArray();
        }
    }

    static bool IsSpotlight(Item it)
    {
        if (it?.spr == null) return false;
        return it.spr.IndexOf("spotlight", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    static bool IsSpotlightHousing(Item it)
    {
        if (it?.spr == null) return false;
        return it.spr.EndsWith("_3", StringComparison.Ordinal);
    }

    public static void DropStrayEditRoots()
    {
        var sc = SceneManager.GetActiveScene();
        if (!sc.IsValid()) return;
        foreach (var go in sc.GetRootGameObjects())
        {
            if (go == null) continue;
            var n = go.name ?? "";
            if (n.StartsWith("场景编辑_") || (n.Length >= 6
                && char.IsDigit(n[0]) && char.IsDigit(n[1]) && char.IsDigit(n[2]) && char.IsDigit(n[3]) && n[4] == '_'))
                UnityEngine.Object.Destroy(go);
        }
    }

    public static View Spawn(Transform parent, string scene, Shader shader, float worldScale)
    {
        DropStrayEditRoots();
        var layout = Load(scene);
        if (layout == null) return null;
        var dir = Path.Combine(Application.streamingAssetsPath, "MdLooks", "sprites", scene, "bg");
        var tex = new Dictionary<string, Texture2D>(StringComparer.Ordinal);
        if (layout.sprites != null)
        {
            foreach (var s in layout.sprites)
            {
                if (s == null || string.IsNullOrEmpty(s.file)) continue;
                var png = Path.Combine(dir, s.file);
                if (!File.Exists(png)) continue;
                var t = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!t.LoadImage(File.ReadAllBytes(png))) continue;
                t.filterMode = FilterMode.Bilinear;
                t.wrapMode = TextureWrapMode.Clamp;
                t.name = s.name;
                tex[s.name] = t;
            }
        }

        var root = new GameObject("SceneBg").transform;
        root.SetParent(parent, false);
        root.localPosition = Vector3.zero;
        root.SetAsFirstSibling();
        var view = new View
        {
            Root = root,
            Moves = layout.moves,
            MovePeriod = layout.movePeriod,
            MoveT = layout.moveT,
            MoveX = layout.moveX,
            MoveY = layout.moveY,
            MoveStep = layout.moveStep != 0,
            MovePeriod2 = layout.movePeriod2,
            MoveT2 = layout.moveT2,
            MoveX2 = layout.moveX2,
            MoveY2 = layout.moveY2,
            SpinPeriod = layout.spinPeriod,
            SpinT = layout.spinT,
            SpinZ = layout.spinZ,
            WorldScale = worldScale
        };
        ResetBands();
        foreach (var t in tex.Values) view.Textures.Add(t);
        var shSprite = Shader.Find("MusePSona/MdSprite") ?? shader;
        var shDefault = Shader.Find("Sprites/Default");
        var shColor = Shader.Find("Unlit/Color");
        var shTrans = Shader.Find("Unlit/Transparent");
        var shTex = Shader.Find("Unlit/Texture");
        foreach (var g in layout.groups)
        {
            if (g == null) continue;
            var layer = new GameObject(string.IsNullOrEmpty(g.name) ? "layer" : g.name).transform;
            layer.SetParent(root, false);
            layer.localPosition = new Vector3(g.x * worldScale, g.y * worldScale, 2.4f);
            var loop = g.loopW > 0.05f ? g.loopW : g.loop;
            var period = g.periodS > 0.01f ? g.periodS : g.period;
            if (loop < 0.05f) loop = 0f;
            var speed = loop > 0.05f && period > 0.01f ? loop / period : 0f;
            var nm = g.name ?? "";
            if (g.belt == 0 && speed < 2.5f && ConveyorName(nm))
            {
                if (loop < 0.05f) loop = BeltFromItems(g.items);
                if (loop < 0.05f) loop = 8f;
                speed = 5f;
                period = loop / speed;
            }
            // scene_08 开局 AnchorZero，避免 Repeat 负位移跳一整圈。
            // Mid_A/B 圈 127 已显式铺开局到月亮，不再 k 拷。Near/Ice/Far 只向右拷。
            var anchor = scene == "scene_08" && speed > 0.01f && loop > 1f;
            view.Layers.Add(new Layer
            {
                Root = layer,
                Loop = loop * worldScale,
                Period = period,
                Speed = speed * worldScale,
                BaseX = g.x * worldScale,
                Phase = g.phase * worldScale,
                AnchorZero = anchor,
                Mode = g.mode ?? "scroll"
            });
            SpriteCopyRange(scene, nm, loop, g.items, out var k0, out var k1);
            // 只改 scene_01 地面：AAB 密铺 overlap，官机往右 order 递减。全场景按 k 改 order 会把 08/12 中景叠错。
            var orderSpan = 0;
            if (scene == "scene_01" && nm == "Near" && g.items != null)
            {
                var mn = int.MaxValue;
                var mx = int.MinValue;
                foreach (var it in g.items)
                {
                    if (it == null) continue;
                    if (it.order < mn) mn = it.order;
                    if (it.order > mx) mx = it.order;
                }
                if (mx >= mn && mn != int.MaxValue) orderSpan = Mathf.Max(1, mx - mn + 1);
            }
            for (var k = k0; k <= k1; k++)
            {
                var ox = k * loop;
                if (g.items == null) continue;
                foreach (var it in g.items)
                {
                    if (it == null || !tex.TryGetValue(it.spr, out var t) || t == null) continue;
                    var ord = it.order - k * orderSpan;
                    var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    quad.name = it.spr;
                    quad.transform.SetParent(layer, false);
                    var col = quad.GetComponent<Collider>();
                    if (col) UnityEngine.Object.Destroy(col);
                    quad.transform.localPosition = new Vector3((it.x - g.x + ox) * worldScale, (it.y - g.y) * worldScale, ord * -0.002f);
                    var sx = Mathf.Max(0.01f, it.w * worldScale) * (it.flipX ? -1f : 1f);
                    var sy = Mathf.Max(0.01f, it.h * worldScale) * (it.flipY ? -1f : 1f);
                    if (IsSpotlight(it) && !IsSpotlightHousing(it) && Mathf.Abs(it.rotZ) > 0.5f)
                        sx = Mathf.Abs(sx);
                    quad.transform.localScale = new Vector3(sx, sy, 1f);
                    var mf = quad.GetComponent<MeshFilter>();
                    if (mf != null && it.capL + it.capR > 0.02f && it.w > it.capL + it.capR)
                        mf.sharedMesh = Slice3Mesh(it.w, it.h, it.capL, it.capR, it.sliceL, it.sliceR);
                    else if (mf != null && it.topW > 0.01f && it.w > 0.01f)
                    {
                        mf.sharedMesh = TrapezoidMesh(it.w, it.h, it.topW);
                        quad.transform.localScale = new Vector3(
                            (it.flipX ? -1f : 1f) * worldScale,
                            (it.flipY ? -1f : 1f) * worldScale,
                            1f);
                    }
                    if (Mathf.Abs(it.rotZ) > 0.01f)
                        quad.transform.localRotation = Quaternion.Euler(0f, 0f, it.rotZ);
                    if (it.spinId > 0)
                    {
                        var spinner = new Spinner
                        {
                            T = quad.transform,
                            Base = it.rotZ,
                            Channel = it.spinId,
                            FlipSpin = it.spinFlip != 0
                        };
                        if (it.orbit != 0)
                        {
                            var pivot = new Vector3((it.pivotX - g.x + ox) * worldScale, (it.pivotY - g.y) * worldScale, quad.transform.localPosition.z);
                            var off = quad.transform.localPosition - pivot;
                            off.z = 0f;
                            spinner.Pivot = pivot;
                            spinner.Offset = off;
                            spinner.Orbit = true;
                        }
                        view.Spinners.Add(spinner);
                    }
                    var sh = shSprite;
                    if (it.topW > 0.01f)
                        sh = shDefault ?? shColor ?? sh;
                    if (it.mw <= 0.05f && (sh == null || sh.name.IndexOf("Color", StringComparison.OrdinalIgnoreCase) >= 0))
                        sh = shDefault ?? shTrans ?? shTex ?? shader;
                    var mat = new Material(sh);
                    MdMat.SetTex(mat, t);
                    var c = new Color(it.r, it.g, it.b, it.a);
                    mat.color = c;
                    if (mat.HasProperty("_Color")) mat.SetColor("_Color", c);
                    if (mat.HasProperty("_UseClip") && it.mw <= 0.05f)
                        mat.SetFloat("_UseClip", 0f);
                    if (mat.HasProperty("_ClipValue") && it.spr != "AudioClip")
                        mat.SetFloat("_ClipValue", 1f);
                    mat.renderQueue = 2450;
                    var r = quad.GetComponent<Renderer>();
                    if (r != null)
                    {
                        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                        r.receiveShadows = false;
                        r.material = mat;
                        r.sortingOrder = ord;
                        if (it.mw > 0.05f)
                        {
                            var clip = new Clipper
                            {
                                Mat = mat,
                                Layer = layer,
                                LocalX = (it.mx - g.x + ox) * worldScale,
                                LocalY = (it.my - g.y) * worldScale,
                                HalfW = it.mw * 0.5f * worldScale,
                                HalfH = it.mh * 0.5f * worldScale,
                                MoveId = it.moveId
                            };
                            view.Clippers.Add(clip);
                            ApplyClip(clip, view, 0f);
                        }
                        if (it.popPeriod > 0.05f && it.popAt > 0.02f)
                        {
                            view.Poppers.Add(new Popper { Go = quad, At = it.popAt, Off = it.popOff, Period = it.popPeriod, Delay = it.popDelay });
                            quad.SetActive(false);
                        }
                        if (it.spr == "AudioClip")
                        {
                            mat.SetFloat("_ClipMirror", 1f);
                            mat.SetFloat("_ClipAxis", ClipAxis);
                            ExtentOf(ClipMin, out var up, out var down);
                            mat.SetFloat("_ClipUp", up);
                            mat.SetFloat("_ClipDown", down);
                            view.Bands.Add(new BandBar { Mat = mat, Band = it.band, Clip = ClipMin });
                        }
                        // 人字纹 scrollY=±0.18，门槛 0.5 会整列不注册 Mover。
                        if (it.bobAmp > 0.001f || it.blinkPeriod > 0.05f || Mathf.Abs(it.scrollLoop) > 0.05f || Mathf.Abs(it.scrollY) > 0.05f)
                        {
                            view.Movers.Add(new Mover
                            {
                                T = quad.transform,
                                Rend = r,
                                Base = quad.transform.localPosition,
                                Tint = c,
                                BobAmp = it.bobAmp * worldScale,
                                BobPeriod = it.bobPeriod > 0.05f ? it.bobPeriod : 1.2f,
                                BobPhase = it.bobPhase,
                                BlinkPeriod = it.blinkPeriod,
                                ScrollLoop = it.scrollLoop * worldScale,
                                ScrollPeriod = it.scrollPeriod > 0.05f ? it.scrollPeriod : 2f,
                                ScrollAngle = it.scrollAngle,
                                ScrollY = it.scrollY * worldScale,
                                MoveId = it.moveId
                            });
                        }
                        else if (it.moveId > 0)
                        {
                            view.Followers.Add(new Follower
                            {
                                T = quad.transform,
                                Base = quad.transform.localPosition,
                                MoveId = it.moveId
                            });
                        }
                    }
                }
            }
        }
        var rolling = 0;
        foreach (var layer in view.Layers)
            if (layer.Speed > 0.01f) rolling++;
        Debug.Log("背景 " + scene + " 层 " + view.Layers.Count + " 滚动 " + rolling + " 骨 " + (layout.spines == null ? 0 : layout.spines.Length)
            + " 弹窗键 " + (view.MoveT == null ? 0 : view.MoveT.Length));
        if (layout.spines != null)
        {
            foreach (var sp in layout.spines)
            {
                if (sp == null || string.IsNullOrEmpty(sp.pack)) continue;
                var pack = MdSpine.LoadNamed(scene, sp.pack);
                if (pack == null) continue;
                var holder = root;
                float loopW = 0f;
                if (sp.gi >= 0 && sp.gi < view.Layers.Count && view.Layers[sp.gi].Root != null)
                {
                    holder = view.Layers[sp.gi].Root;
                    loopW = view.Layers[sp.gi].Loop;
                }
                var gx = sp.gi >= 0 && sp.gi < layout.groups.Length ? layout.groups[sp.gi].x : 0f;
                var gy = sp.gi >= 0 && sp.gi < layout.groups.Length ? layout.groups[sp.gi].y : 0f;
                var ghost = !string.IsNullOrEmpty(sp.pack)
                    && sp.pack.IndexOf("ghost", StringComparison.OrdinalIgnoreCase) >= 0;
                LoopCopyRange(scene, loopW, worldScale, false, out var kMin, out var kMax);
                if (ghost) { kMin = 0; kMax = 0; }
                for (var k = kMin; k <= kMax; k++)
                {
                    var v = MdSpineView.Attach(holder, pack);
                    v.transform.localPosition = new Vector3(
                        (sp.x - gx) * worldScale + k * loopW,
                        (sp.y - gy) * worldScale,
                        ghost ? 0.35f : sp.order * -0.002f);
                    var sx = (Mathf.Abs(sp.sx) < 0.01f ? 1f : sp.sx) * worldScale;
                    var sy = (Mathf.Abs(sp.sy) < 0.01f ? 1f : sp.sy) * worldScale;
                    v.transform.localScale = new Vector3(sx, sy, 1f);
                    var queue = ghost ? 2500 : 2550;
                    // 鸟和 mid_far 猫必须和紫叶同一 2450，只靠 sortingOrder 钻进树里。2550 会整只盖在叶子前面。
                    if (!ghost && (sp.pack == "sky wheel" || sp.pack == "s08_moon"
                        || (!string.IsNullOrEmpty(sp.pack) && (sp.pack.StartsWith("s12_bird") || sp.pack == "s12_middle_cat"))))
                        queue = 2450;
                    v.SetLayer(queue, ghost ? -25 : (sp.order != 0 ? sp.order : -35));
                    view.Props.Add(new Prop
                    {
                        Spine = v,
                        Clip = sp.clip ?? ""
                    });
                }
            }
        }
        if (layout.particles != null && layout.particles.Length > 0)
            SpawnParticles(view, layout, tex, worldScale, scene);
        return view;
    }

    static Shader ParticleShader()
    {
        return Shader.Find("Mobile/Particles/Additive")
            ?? Shader.Find("Particles/Additive")
            ?? Shader.Find("Legacy Shaders/Particles/Additive")
            ?? Shader.Find("Sprites/Default");
    }

    static void SpawnParticles(View view, Layout layout, Dictionary<string, Texture2D> tex, float worldScale, string scene)
    {
        var sh = ParticleShader();
        if (sh == null) return;
        var root = view.Root;
        foreach (var p in layout.particles)
        {
            if (p == null) continue;
            Texture2D t = null;
            if (!string.IsNullOrEmpty(p.tex))
                tex.TryGetValue(p.tex, out t);
            if (t == null)
                tex.TryGetValue("Default-ParticleSystem", out t);
            var holder = root;
            var loopW = 0f;
            var gx = 0f;
            var gy = 0f;
            if (p.gi >= 0 && p.gi < view.Layers.Count && view.Layers[p.gi].Root != null)
            {
                holder = view.Layers[p.gi].Root;
                loopW = view.Layers[p.gi].Loop;
            }
            if (p.gi >= 0 && p.gi < layout.groups.Length && layout.groups[p.gi] != null)
            {
                gx = layout.groups[p.gi].x;
                gy = layout.groups[p.gi].y;
            }
            LoopCopyRange(scene, loopW, worldScale, true, out var kMin, out var kMax);
            for (var k = kMin; k <= kMax; k++)
            {
                var go = new GameObject(string.IsNullOrEmpty(p.tex) ? "ps" : p.tex);
                go.transform.SetParent(holder, false);
                go.transform.localPosition = new Vector3(
                    (p.x - gx) * worldScale + k * loopW,
                    (p.y - gy) * worldScale,
                    p.order * -0.002f);
                if (Mathf.Abs(p.rotZ) > 0.05f)
                    go.transform.localRotation = Quaternion.Euler(0f, 0f, p.rotZ);
                var ps = go.AddComponent<ParticleSystem>();
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                var main = ps.main;
                main.playOnAwake = false;
                main.loop = true;
                main.duration = Mathf.Max(0.2f, p.dur);
                main.startDelay = Mathf.Max(0f, p.delay);
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                main.maxParticles = p.maxP > 0 ? Mathf.Min(p.maxP, 400) : 200;
                main.startLifetime = ApproxRange(p.life, p.life2);
                main.startSpeed = ApproxRange(p.speed, p.speed2);
                main.startSize = ApproxRange(p.size, p.size2);
                main.startColor = new Color(p.cr, p.cg, p.cb, p.ca);
                main.gravityModifier = p.grav;
                main.simulationSpeed = 1f;
                var emission = ps.emission;
                emission.enabled = true;
                emission.rateOverTime = p.rate;
                if (p.burstMax > 0.5f || p.burstMin > 0.5f)
                {
                    var lo = (short)Mathf.Max(1, Mathf.RoundToInt(p.burstMin > 0.5f ? p.burstMin : p.burstMax));
                    var hi = (short)Mathf.Max(lo, Mathf.RoundToInt(Mathf.Max(p.burstMin, p.burstMax)));
                    emission.SetBursts(new[] { new ParticleSystem.Burst(0f, lo, hi) });
                }
                var shMod = ps.shape;
                shMod.enabled = true;
                if (p.shape == 12)
                    shMod.shapeType = ParticleSystemShapeType.SingleSidedEdge;
                else if (p.shape == 4)
                    shMod.shapeType = ParticleSystemShapeType.Cone;
                else if (p.shape == 5)
                    shMod.shapeType = ParticleSystemShapeType.Box;
                else
                    shMod.shapeType = ParticleSystemShapeType.Circle;
                if (p.shape == 5)
                    shMod.scale = new Vector3(Mathf.Max(0.05f, p.shapeSX), Mathf.Max(0.05f, p.radius), 1f);
                else
                    shMod.radius = Mathf.Max(0.001f, p.radius) * Mathf.Max(0.01f, p.shapeSX > 0.01f ? p.shapeSX : 1f);
                if (p.shape == 4)
                    shMod.angle = 25f;
                if (p.colorLife != 0)
                {
                    var col = ps.colorOverLifetime;
                    col.enabled = true;
                    var grad = new Gradient();
                    grad.SetKeys(
                        new[] { new GradientColorKey(new Color(p.lr, p.lg, p.lb, 1f), 0f), new GradientColorKey(new Color(p.lr, p.lg, p.lb, 1f), 1f) },
                        new[] { new GradientAlphaKey(p.la, 0f), new GradientAlphaKey(p.la, 1f) });
                    col.color = grad;
                }
                if (p.sizeLife != 0)
                {
                    var sol = ps.sizeOverLifetime;
                    sol.enabled = true;
                    sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                        new Keyframe(0f, 0.25f),
                        new Keyframe(0.4f, 1f),
                        new Keyframe(0.79f, 1f),
                        new Keyframe(1f, 0f)));
                }
                if (p.noise != 0)
                {
                    var n = ps.noise;
                    n.enabled = true;
                    n.strength = p.noiseStr > 0.01f ? p.noiseStr : 0.7f;
                    n.frequency = p.noiseFreq > 0.01f ? p.noiseFreq : 0.8f;
                    n.scrollSpeed = 0.1f;
                    n.damping = true;
                }
                var rend = go.GetComponent<ParticleSystemRenderer>();
                if (rend != null)
                {
                    var mat = new Material(sh);
                    if (t != null)
                    {
                        MdMat.SetTex(mat, t);
                    }
                    var tint = new Color(p.cr, p.cg, p.cb, p.ca);
                    if (mat.HasProperty("_TintColor")) mat.SetColor("_TintColor", tint * 0.5f);
                    if (mat.HasProperty("_Color")) mat.SetColor("_Color", tint);
                    mat.renderQueue = 3000;
                    rend.material = mat;
                    rend.sortingOrder = p.order;
                    rend.renderMode = ParticleSystemRenderMode.Billboard;
                }
                ps.Play(true);
            }
        }
    }

    static ParticleSystem.MinMaxCurve ApproxRange(float a, float b)
    {
        if (Mathf.Abs(a - b) < 0.0001f)
            return a;
        var lo = Mathf.Min(a, b);
        var hi = Mathf.Max(a, b);
        return new ParticleSystem.MinMaxCurve(lo, hi);
    }

    public static void Tick(View view, float chartNow, BgTimeline.State st, float dt)
    {
        if (view == null || view.Root == null) return;
        view.Root.gameObject.SetActive(!st.Hidden);
        if (st.Hidden) return;
        if (!view.BeltReady)
        {
            view.BeltOrigin = Time.unscaledTime;
            view.BeltChart = chartNow;
            view.BeltHeld = 0f;
            view.BeltReady = true;
        }
        // 音频时间卡住时 chartNow 不变，横移会冻住。皮带按进入这一景后的真实时间走。
        if (st.Frozen || dt <= 0f)
            view.BeltOrigin = Time.unscaledTime - view.BeltHeld;
        else
            view.BeltHeld = Time.unscaledTime - view.BeltOrigin;
        var beltNow = view.BeltChart + view.BeltHeld;
        if (!st.Frozen)
            view.ScrollY += st.Scroll * ScreenScrollSpeed * dt;
        view.Root.localPosition = new Vector3(0f, view.ScrollY, 0f);
        foreach (var layer in view.Layers)
        {
            if (layer.Root == null) continue;
            if (layer.Loop > 0.01f && layer.Speed > 0.01f)
            {
                if (layer.Mode == "once")
                {
                    var progress = Mathf.Clamp01(beltNow / layer.Period);
                    layer.Shift = -progress * layer.Loop - layer.Phase;
                }
                else if (layer.AnchorZero)
                {
                    // 开头停在 0。负号再 Repeat 会在开局跳成一整圈，镜头就落到空天上。
                    layer.Shift = -Mathf.Repeat(beltNow * layer.Speed + layer.Phase, layer.Loop);
                }
                else
                {
                    // 循环滚动
                    layer.Shift = Mathf.Repeat(-beltNow * layer.Speed - layer.Phase, layer.Loop);
                }
            }
            var p = layer.Root.localPosition;
            p.x = layer.BaseX + layer.Shift;
            layer.Root.localPosition = p;
        }
        foreach (var prop in view.Props)
        {
            if (prop?.Spine == null) continue;
            prop.Spine.PoseLoop(prop.Clip, chartNow);
        }
        foreach (var pop in view.Poppers)
        {
            if (pop?.Go == null || pop.Period < 0.05f) continue;
            var t = Mathf.Repeat(chartNow - pop.Delay, pop.Period);
            var on = t >= pop.At;
            if (pop.Off > 0.05f) on = on && t < pop.Off;
            pop.Go.SetActive(on);
        }
        if (!st.Frozen)
        {
        foreach (var m in view.Movers)
        {
            if (m?.T == null) continue;
            var p = m.Base;
            if (Mathf.Abs(m.ScrollLoop) > 0.01f && m.ScrollPeriod > 0.05f)
            {
                var u = chartNow / m.ScrollPeriod;
                u -= Mathf.Floor(u);
                var shift = u * m.ScrollLoop;
                var rad = m.ScrollAngle * Mathf.Deg2Rad;
                p.x += shift * Mathf.Cos(rad);
                p.y += shift * Mathf.Sin(rad);
            }
            if (Mathf.Abs(m.ScrollY) > 0.01f && m.ScrollPeriod > 0.05f)
            {
                var dist = Mathf.Abs(m.ScrollY);
                var dir = Mathf.Sign(m.ScrollY);
                p.y += dir * Mathf.Repeat(chartNow / m.ScrollPeriod, 1f) * dist;
            }
            // 方块墙是整列竖滚。单块再 bob 会把井拆散，和官机 scene_01 对不上。
            if (m.BobAmp > 0.001f && m.BobPeriod > 0.05f && Mathf.Abs(m.ScrollY) < 0.01f)
                p.y += Mathf.Sin(chartNow * (Mathf.PI * 2f) / m.BobPeriod + m.BobPhase) * m.BobAmp;
            var d = SampleMove(view, m.MoveId, chartNow);
            p.x += d.x;
            p.y += d.y;
            m.T.localPosition = p;
            if (m.BlinkPeriod > 0.05f && m.Rend != null && m.Rend.material != null)
            {
                var a = 0.35f + 0.65f * (0.5f + 0.5f * Mathf.Sin(chartNow * (Mathf.PI * 2f) / m.BlinkPeriod + m.BobPhase));
                var col = m.Tint;
                col.a = m.Tint.a * a;
                m.Rend.material.color = col;
                if (m.Rend.material.HasProperty("_Color")) m.Rend.material.SetColor("_Color", col);
            }
        }
        }
        foreach (var f in view.Followers)
        {
            if (f?.T == null) continue;
            var p = f.Base;
            var d = SampleMove(view, f.MoveId, chartNow);
            p.x += d.x;
            p.y += d.y;
            f.T.localPosition = p;
        }
        foreach (var s in view.Spinners)
        {
            if (s?.T == null) continue;
            var delta = SampleSpin(view, s.Channel, chartNow);
            if (s.FlipSpin) delta = -delta;
            if (s.Orbit)
            {
                var rad = delta * Mathf.Deg2Rad;
                var c = Mathf.Cos(rad);
                var sn = Mathf.Sin(rad);
                var o = s.Offset;
                var p = s.Pivot;
                p.x += c * o.x - sn * o.y;
                p.y += sn * o.x + c * o.y;
                s.T.localPosition = p;
                s.T.localRotation = Quaternion.Euler(0f, 0f, s.Base + delta);
            }
            else
                s.T.localRotation = Quaternion.Euler(0f, 0f, s.Base + delta);
        }
        foreach (var c in view.Clippers)
            ApplyClip(c, view, chartNow);
        if (dt > 0f && view.Bands.Count > 0)
            DriveBands(view);
    }

    static void ResetBands()
    {
        for (var i = 0; i < BandCount; i++)
        {
            BandFreq[i] = 0f;
            BandBuf[i] = 0f;
            BandDec[i] = BandUp;
            BandHigh[i] = 0f;
        }
    }

    static void DriveBands(View view)
    {
        AudioListener.GetSpectrumData(BandSample, 0, FFTWindow.Blackman);
        var cursor = 0;
        var per = SpectrumSamples / BandCount;
        for (var i = 0; i < BandCount; i++)
        {
            var avg = 0f;
            for (var j = 0; j < per; j++)
            {
                avg += BandSample[cursor] * (cursor + 1);
                cursor++;
            }
            avg /= per;
            BandFreq[i] = avg * BandPower;
            if (BandFreq[i] > BandBuf[i])
            {
                BandBuf[i] = BandFreq[i];
                BandDec[i] = BandUp;
            }
            else
            {
                BandBuf[i] -= BandDec[i];
                if (BandBuf[i] < 0f) BandBuf[i] = 0f;
                BandDec[i] *= BandDown;
            }
            if (BandFreq[i] > BandHigh[i]) BandHigh[i] = BandFreq[i];
        }
        foreach (var bar in view.Bands)
        {
            if (bar?.Mat == null) continue;
            var idx = bar.Band;
            if (idx < 0 || idx >= BandCount) idx = 0;
            var norm = BandHigh[idx] > 1e-6f ? BandBuf[idx] / BandHigh[idx] : 0f;
            var target = Mathf.Lerp(ClipMin, 1f, Mathf.Clamp01(norm * BandGain));
            bar.Clip = Mathf.Lerp(bar.Clip, target, ClipLerp);
            ExtentOf(bar.Clip, out var up, out var down);
            bar.Mat.SetFloat("_ClipUp", up);
            bar.Mat.SetFloat("_ClipDown", down);
        }
    }

    static void ExtentOf(float clip01, out float inner, out float outer)
    {
        inner = ClipInner;
        var edge = ClipAxis + ClipInner;
        var t = Mathf.InverseLerp(ClipMin, 1f, Mathf.Clamp(clip01, ClipMin, 1f));
        var want = edge + Mathf.Lerp(ClipCellsMin, ClipCellsMax, t) * ClipCell;
        var best = edge + ClipCellsMax * ClipCell;
        for (var i = 0; i < BlockTop.Length; i++)
        {
            var top = BlockTop[i];
            if (top + 0.0001f >= want && top < best) best = top;
        }
        outer = best - ClipAxis;
    }

    static Vector2 SampleMove(View view, int moveId, float chartNow)
    {
        if (view == null || moveId <= 0) return Vector2.zero;
        var ts = moveId == 2 ? view.MoveT2 : view.MoveT;
        var xs = moveId == 2 ? view.MoveX2 : view.MoveX;
        var ys = moveId == 2 ? view.MoveY2 : view.MoveY;
        var period = moveId == 2 ? view.MovePeriod2 : view.MovePeriod;
        if (ts == null || xs == null || ys == null || ts.Length < 2)
        {
            if (view.Moves == null || moveId > view.Moves.Length) return Vector2.zero;
            var packed = view.Moves[moveId - 1];
            if (packed == null) return Vector2.zero;
            ts = packed.ts;
            xs = packed.xs;
            ys = packed.ys;
            period = packed.period;
        }
        if (ts == null || xs == null || ys == null || ts.Length < 1)
            return Vector2.zero;
        var n = Mathf.Min(ts.Length, Mathf.Min(xs.Length, ys.Length));
        if (n < 1) return Vector2.zero;
        if (period < 0.05f) period = ts[n - 1];
        if (period < 0.05f) return Vector2.zero;
        var t = chartNow - period * Mathf.Floor(chartNow / period);
        var x0 = xs[0];
        var y0 = ys[0];
        float x = xs[0], y = ys[0];
        if (view.MoveStep)
        {
            for (var i = 0; i < n; i++)
            {
                if (t + 0.0001f < ts[i]) break;
                x = xs[i];
                y = ys[i];
            }
        }
        else if (t <= ts[0])
        {
            x = xs[0];
            y = ys[0];
        }
        else
        {
            for (var i = 1; i < n; i++)
            {
                if (t > ts[i]) continue;
                var u = Mathf.InverseLerp(ts[i - 1], ts[i], t);
                x = Mathf.Lerp(xs[i - 1], xs[i], u);
                y = Mathf.Lerp(ys[i - 1], ys[i], u);
                break;
            }
        }
        return new Vector2((x - x0) * view.WorldScale, (y - y0) * view.WorldScale);
    }

    static float SampleSpin(View view, int spinId, float chartNow)
    {
        if (view == null || spinId <= 0) return 0f;
        var ts = view.SpinT;
        var zs = view.SpinZ;
        if (ts == null || zs == null || ts.Length < 2) return 0f;
        var n = ts.Length;
        var ch = spinId - 1;
        if ((ch + 1) * n > zs.Length) return 0f;
        var o = ch * n;
        var period = view.SpinPeriod > 0.05f ? view.SpinPeriod : ts[n - 1];
        if (period < 0.05f) return 0f;
        var t = chartNow - period * Mathf.Floor(chartNow / period);
        var z0 = zs[o];
        var z = z0;
        if (t > ts[0])
        {
            for (var i = 1; i < n; i++)
            {
                if (t > ts[i]) continue;
                var u = Mathf.InverseLerp(ts[i - 1], ts[i], t);
                z = Mathf.Lerp(zs[o + i - 1], zs[o + i], u);
                break;
            }
        }
        return z - z0;
    }

    static void ApplyClip(Clipper c, View view, float chartNow)
    {
        if (c?.Mat == null || c.Layer == null) return;
        var d = SampleMove(view, c.MoveId, chartNow);
        var p = c.Layer.TransformPoint(new Vector3(c.LocalX + d.x, c.LocalY + d.y, 0f));
        c.Mat.SetVector("_ClipRect", new Vector4(p.x - c.HalfW, p.y - c.HalfH, p.x + c.HalfW, p.y + c.HalfH));
        c.Mat.SetFloat("_UseClip", 1f);
    }

    public static void Drop(View view)
    {
        if (view == null) return;
        if (view.Root != null) UnityEngine.Object.Destroy(view.Root.gameObject);
        foreach (var t in view.Textures)
            if (t != null) UnityEngine.Object.Destroy(t);
        view.Textures.Clear();
        view.Props.Clear();
        view.Movers.Clear();
        view.Clippers.Clear();
        view.Poppers.Clear();
        view.Followers.Clear();
        view.Bands.Clear();
        view.Layers.Clear();
    }
}
