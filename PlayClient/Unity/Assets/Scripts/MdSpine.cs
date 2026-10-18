using System.Collections.Generic;
using System.Globalization;
using System.IO;
using MusePSona.Play;
using Spine;
using UnityEngine;
using Color = UnityEngine.Color;

/// <summary>StreamingAssets/MdLooks/spine：3.8.99 .skel 完整包。按 MDM 场景换骨，播 in_*。不进 MDM。</summary>
public static class MdSpine
{
    public sealed class Pack
    {
        public SkeletonData Data;
        public Atlas Atlas;
        public bool VFlipped;
        public readonly Dictionary<Texture2D, Material> Mats = new();
    }

    static readonly Dictionary<string, Pack> Cache = new();
    static float _scale = 0.005f;
    static bool _ready;
    static Shader _shader;

    public static void Ensure()
    {
        if (_shader == null)
            _shader = Shader.Find("MusePSona/SpinePMA") ?? Shader.Find("Sprites/Default");
        if (_ready) return;
        _ready = true;
        var idx = Path.Combine(Application.streamingAssetsPath, "MdLooks", "spine", "index.json");
        if (!File.Exists(idx)) return;
        var text = File.ReadAllText(idx);
        var m = System.Text.RegularExpressions.Regex.Match(text, @"\""scale\""\s*:\s*([0-9.]+)");
        if (m.Success) _scale = float.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
        Debug.Log("MdSpine scale " + _scale);
    }

    public static Pack Load(string scene, PlayNote n)
    {
        Ensure();
        foreach (var key in Keys(scene, n))
        {
            var pack = LoadNamed(scene, key);
            if (pack != null) return pack;
        }
        return null;
    }

    public static Pack LoadNamed(string scene, string key)
    {
        Ensure();
        scene = SceneTimeline.Norm(scene);
        if (scene.Length == 0) scene = "scene_01";
        var id = scene + "/" + key;
        if (Cache.TryGetValue(id, out var hit)) return hit;
        var pack = TryRead(scene, key) ?? TryRead("common", key);
        if (pack == null && key.StartsWith("fx_combo", System.StringComparison.OrdinalIgnoreCase))
            pack = TryRead("scene_01", key) ?? TryRead("scene_05", key);
        if (pack == null) return null;
        Cache[id] = pack;
        return pack;
    }

    public static Pack LoadBoss(string scene)
    {
        foreach (var key in BossKeys(scene))
        {
            var pack = LoadNamed(scene, key);
            if (pack != null) return pack;
        }
        return null;
    }

    public static IEnumerable<string> BossKeys(string scene)
    {
        var prefix = SceneTimeline.Norm(scene);
        var num = prefix.Length >= 2 ? prefix[^2..] : "01";
        yield return num + "01";
        yield return num + "01_road";
        yield return num + "01_air";
    }

    public static string PickBossClip(SkeletonData data, string want)
    {
        if (data == null || string.IsNullOrEmpty(want)) return null;
        var hit = FindClip(data, want);
        if (hit != null) return hit;
        if (want is "hurt" or "boss_hurt")
            return FindClip(data, "hurt") ?? FindClip(data, "boss_hurt");
        if (want is "far_atk_1_air" or "far_atk_1_road")
            return FindClip(data, "far_atk_1") ?? FindClip(data, "far_atk_1_standby") ?? FindClip(data, "standby");
        if (want == "far_atk_1_start")
            return FindClip(data, "far_atk_1") ?? FindClip(data, "far_atk_1_standby") ?? FindClip(data, "standby");
        if (want == "far_atk_1_standby")
            return FindClip(data, "far_atk_1_standby") ?? FindClip(data, "far_atk_1") ?? FindClip(data, "standby");
        if (want == "far_atk_1_end" || want == "far_atk_2_end")
            return FindClip(data, "standby");
        if (want == "far_atk_2_start")
            return FindClip(data, "far_atk_2") ?? FindClip(data, "far_atk_2_standby") ?? FindClip(data, "standby");
        if (want == "far_atk_2_standby")
            return FindClip(data, "far_atk_2_standby") ?? FindClip(data, "far_atk_2") ?? FindClip(data, "standby");
        if (want == "multi_atk_hurt" || want.StartsWith("multi_atk_hurt_"))
            return PickMashHurt(data, 1);
        if (want.StartsWith("close_atk_1"))
        {
            foreach (var a in data.Animations)
            {
                var nm = a.Name ?? "";
                if (nm.StartsWith("close_atk_1")) return nm;
            }
        }
        if (want.StartsWith("multi_atk"))
        {
            foreach (var a in data.Animations)
            {
                var nm = a.Name ?? "";
                if (nm.StartsWith("multi_atk") && !nm.EndsWith("G", System.StringComparison.Ordinal)) return nm;
            }
        }
        if (want.StartsWith("close_atk_2"))
        {
            foreach (var a in data.Animations)
            {
                var nm = a.Name ?? "";
                if (nm.StartsWith("close_atk_2")) return nm;
            }
        }
        return FindClip(data, "standby") ?? (data.Animations.Count > 0 ? data.Animations.Items[0].Name : null);
    }

    /// <summary>
    /// 连打挨打。有 multi_atk_hurt 就用它；1201 拆成 hurt_1..4，按第几次连击轮换。
    /// </summary>
    public static string PickMashHurt(SkeletonData data, int hits)
    {
        if (data == null) return "multi_atk_hurt";
        if (FindClip(data, "multi_atk_hurt") != null) return "multi_atk_hurt";
        var n = 0;
        for (var i = 1; i <= 8; i++)
        {
            if (FindClip(data, "multi_atk_hurt_" + i.ToString(CultureInfo.InvariantCulture)) == null) break;
            n = i;
        }
        if (n == 0) return FindClip(data, "hurt") ?? "multi_atk_hurt";
        var idx = hits <= 0 ? 1 : ((hits - 1) % n) + 1;
        return "multi_atk_hurt_" + idx.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// 单次演出播完后的回收。攻击形态还在时，空闲停在该形态的 standby，不要落回入场待机。
    /// end 才是离开该形态，播完可以回入场待机。
    /// </summary>
    public static string PickBossRecover(SkeletonData data, string from)
    {
        if (data == null || string.IsNullOrEmpty(from)) return null;
        if (from.StartsWith("far_atk_1") && from != "far_atk_1_end" && from != "far_atk_1_standby")
            return FindClip(data, "far_atk_1_standby") ?? FindClip(data, "standby");
        if (from.StartsWith("far_atk_2") && from != "far_atk_2_end" && from != "far_atk_2_standby")
            return FindClip(data, "far_atk_2_standby") ?? FindClip(data, "far_atk_2") ?? FindClip(data, "standby");
        if (from == "hurt" || from == "boss_hurt")
            return FindClip(data, "standby");
        if (from == "out" || from == "outside" || from == "multi_atk_end" || from == "multi_atk_48_end"
            || from.StartsWith("multi_atk_hurt_end"))
            return null;
        if (from == "in" || from == "multi_atk_out" || from.StartsWith("atk_1_to_2") || from.StartsWith("atk_2_to_1")
            || from == "far_atk_1_end" || from == "far_atk_2_end")
            return FindClip(data, "standby");
        return null;
    }

    /// <summary>
    /// 冲刺片段里 attack_fx 最后熄灭的时间。0601 的刀光在 48 帧判定之后才出现，提前切 hurt 会把特效切掉。
    /// 没有这个槽时返回 0。
    /// </summary>
    public static float AttackFxEnd(SkeletonData data, string clip)
    {
        var anim = data?.FindAnimation(clip);
        if (anim == null) return 0f;
        var end = 0f;
        foreach (var tl in anim.Timelines)
        {
            if (tl is not AttachmentTimeline att) continue;
            var slot = data.Slots.Items[att.SlotIndex];
            var sn = slot?.Name ?? "";
            if (sn.IndexOf("attack_fx", System.StringComparison.OrdinalIgnoreCase) < 0) continue;
            for (var i = 0; i < att.FrameCount; i++)
            {
                var name = att.AttachmentNames[i] ?? "";
                var on = name.IndexOf("attack_fx", System.StringComparison.OrdinalIgnoreCase) >= 0;
                if (on)
                    end = Mathf.Max(end, att.Frames[i]);
            }
        }
        return end;
    }

    public static IEnumerable<string> Keys(string scene, PlayNote n)
    {
        var collect = AnimBinding.CollectPack(n);
        if (collect != null)
        {
            yield return collect;
            yield break;
        }
        var spec = AnimBinding.Of(n);
        if (spec != null)
        {
            var exact = AnimBinding.SkeletonKey(scene, spec.Value, n);
            yield return exact;
            var cut = exact.IndexOf('_');
            if (cut > 0) yield return exact[..cut];
            if (exact.EndsWith("_air")) yield return exact[..^4] + "_road";
            if (exact.EndsWith("_road")) yield return exact[..^5] + "_air";
        }
    }

    public static string PickClip(SkeletonData data, PlayNote n)
    {
        var collect = AnimBinding.CollectPack(n);
        if (collect != null)
        {
            var lane = AnimBinding.CollectInClip(n);
            if (data.FindAnimation(lane) != null) return lane;
            if (data.FindAnimation("in_mount") != null) return "in_mount";
        }
        var spec = AnimBinding.Of(n);
        var want = spec == null ? null : AnimBinding.InClip(spec.Value, n.ApproachRate);
        if (want != null && data.FindAnimation(want) != null) return want;
        if (spec != null)
        {
            var prefix = spec.Value.Group switch
            {
                "up" => "in_up_",
                "down" => "in_down_",
                "mash" => "in_down_",
                "nor" => "in_nor_",
                "boss_1" => "boss_1_nor_",
                "boss" => "in_boss_1_",
                "boss2" => "in_boss_2_",
                "num" => "in_",
                _ => "in_"
            };
            var numbered = PickNumbered(data, prefix, n.ApproachRate);
            if (numbered != null) return numbered;
            Spine.Animation best = null;
            foreach (var a in data.Animations)
            {
                if (!a.Name.StartsWith(prefix) || a.Name.Contains("hint") || a.Name.Contains("old") || a.Name.Contains("charge")) continue;
                if (best == null || a.Name.Length < best.Name.Length) best = a;
            }
            if (best != null) return best.Name;
        }
        foreach (var a in data.Animations)
        {
            if (a.Name.StartsWith("in_")) return a.Name;
        }
        return data.Animations.Count > 0 ? data.Animations.Items[0].Name : null;
    }

    /// <summary>按档位取编号入场：数字从大到小 = 档 1/2/3（scene_01 的 23/20/17，城堡 31/28/24）。</summary>
    static string PickNumbered(SkeletonData data, string prefix, int rate)
    {
        var hits = new List<(int n, string name)>();
        foreach (var a in data.Animations)
        {
            var nm = a.Name ?? "";
            if (!nm.StartsWith(prefix) || nm.Contains("hint") || nm.Contains("old") || nm.Contains("charge")) continue;
            var tail = nm.Substring(prefix.Length);
            if (tail.Length == 0 || !char.IsDigit(tail[0])) continue;
            var i = 0;
            while (i < tail.Length && char.IsDigit(tail[i])) i++;
            if (i == 0) continue;
            hits.Add((int.Parse(tail.Substring(0, i), CultureInfo.InvariantCulture), nm));
        }
        if (hits.Count == 0) return null;
        hits.Sort((a, b) => b.n.CompareTo(a.n));
        var idx = 0;
        if (rate >= 3) idx = hits.Count - 1;
        else if (rate == 2 && hits.Count > 1) idx = 1;
        return hits[idx].name;
    }

    public static string PickOut(SkeletonData data, Judge.Grade g)
    {
        if (data == null) return null;
        var first = g == Judge.Grade.Great ? "out_g" : "out_p";
        var second = g == Judge.Grade.Great ? "out_p" : "out_g";
        var hit = FindClip(data, first) ?? FindClip(data, second);
        if (hit != null) return hit;
        foreach (var a in data.Animations)
        {
            var nm = a.Name ?? "";
            if (nm.IndexOf("out_p", System.StringComparison.OrdinalIgnoreCase) >= 0
                || nm.IndexOf("out_g", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return nm;
        }
        foreach (var a in data.Animations)
        {
            var nm = a.Name ?? "";
            if (nm.StartsWith("out_nor")) return nm;
        }
        foreach (var a in data.Animations)
        {
            var nm = a.Name ?? "";
            if (nm.IndexOf("explode", System.StringComparison.OrdinalIgnoreCase) >= 0) return nm;
        }
        foreach (var a in data.Animations)
        {
            var nm = a.Name ?? "";
            if (nm.StartsWith("out")) return nm;
        }
        return null;
    }

    /// <summary>齿轮同轨碰撞使用 x03/x09 官机原生 destroy；不存在时交给显示层回退。</summary>
    public static string PickDestroy(SkeletonData data)
    {
        if (data == null) return null;
        return FindClip(data, "destroy")
            ?? FindClip(data, "explode")
            ?? FindClip(data, "broken");
    }

    public static string PickMasherOut(SkeletonData data, bool miss)
    {
        if (data == null) return null;
        if (miss)
        {
            foreach (var a in data.Animations)
            {
                var nm = a.Name ?? "";
                if (nm.StartsWith("out_nor")) return nm;
            }
            return FindClip(data, "out_nor") ?? PickOut(data, Judge.Grade.Great);
        }
        return FindClip(data, "out_g") ?? FindClip(data, "out_p") ?? PickOut(data, Judge.Grade.Great);
    }

    public static string PickHurt(SkeletonData data, int hitIndex)
    {
        if (data == null) return null;
        var names = new List<string>();
        for (var n = 1; n <= 8; n++)
        {
            var clip = FindClip(data, "hurt_" + n.ToString(CultureInfo.InvariantCulture));
            if (clip == null) break;
            if (RestoresIdleBody(data, clip)) continue;
            names.Add(clip);
        }
        if (names.Count == 0)
        {
            foreach (var a in data.Animations)
            {
                var nm = a.Name ?? "";
                if (!nm.StartsWith("hurt")) continue;
                if (RestoresIdleBody(data, nm)) continue;
                names.Add(nm);
            }
            names.Sort((x, y) => HurtIndex(x).CompareTo(HurtIndex(y)));
        }
        if (names.Count == 0) return null;
        var i = hitIndex % names.Count;
        if (i < 0) i += names.Count;
        return names[i];
    }

    /// <summary>1004 的 hurt_4 把主 body 挂回去，是待机不是挨打。</summary>
    static bool RestoresIdleBody(SkeletonData data, string clip)
    {
        var anim = data.FindAnimation(clip);
        var body = data.FindSlot("body");
        if (anim == null || body == null) return false;
        foreach (var t in anim.Timelines)
        {
            if (t is not AttachmentTimeline at || at.SlotIndex != body.Index) continue;
            foreach (var name in at.AttachmentNames)
            {
                if (string.IsNullOrEmpty(name)) continue;
                var leaf = name;
                var slash = name.LastIndexOf('/');
                if (slash >= 0) leaf = name.Substring(slash + 1);
                if (leaf == "body") return true;
            }
        }
        return false;
    }

    static int HurtIndex(string name)
    {
        var cut = name.LastIndexOf('_');
        if (cut >= 0 && int.TryParse(name[(cut + 1)..], out var n)) return n;
        return 0;
    }

    static string FindClip(SkeletonData data, string want)
    {
        if (data.FindAnimation(want) != null) return want;
        foreach (var a in data.Animations)
        {
            var nm = a.Name ?? "";
            if (string.Equals(nm, want, System.StringComparison.OrdinalIgnoreCase)) return nm;
            if (nm.EndsWith("/" + want, System.StringComparison.OrdinalIgnoreCase)) return nm;
        }
        return null;
    }

    public static string PickComboClip(SkeletonData data)
    {
        if (data == null) return null;
        foreach (var want in new[] { "start", "in", "animation", "combo" })
        {
            var hit = FindClip(data, want);
            if (hit != null) return hit;
        }
        return data.Animations.Count > 0 ? data.Animations.Items[0].Name : null;
    }

    public static string PickComboIdle(SkeletonData data)
    {
        if (data == null) return null;
        return FindClip(data, "standby") ?? FindClip(data, "stand") ?? FindClip(data, "idle");
    }

    public static string PickComboEnd(SkeletonData data)
    {
        if (data == null) return null;
        return FindClip(data, "end") ?? FindClip(data, "out");
    }

    static float ReadScale(string dir)
    {
        var p = Path.Combine(dir, "scale.json");
        if (File.Exists(p))
        {
            var m = System.Text.RegularExpressions.Regex.Match(File.ReadAllText(p), @"\""scale\""\s*:\s*([0-9.]+)");
            if (m.Success) return float.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
        }
        var key = Path.GetFileName(dir);
        if (!string.IsNullOrEmpty(key) && key.Length >= 4 && char.IsDigit(key[0])) return 0.005f;
        return _scale;
    }

    static Pack TryRead(string scene, string key)
    {
        var dir = Path.Combine(Application.streamingAssetsPath, "MdLooks", "spine", scene, key);
        if (!Directory.Exists(dir)) return null;
        string skel = null, atlas = null;
        foreach (var f in Directory.GetFiles(dir))
        {
            var n = Path.GetFileName(f).ToLowerInvariant();
            if (n.EndsWith(".meta") || n == "scale.json") continue;
            if (n.EndsWith(".skel")) skel = f;
            else if (skel == null && n.EndsWith(".json") && n != "scale.json") skel = f;
            if (n.EndsWith(".atlas")) atlas = f;
        }
        if (skel == null || atlas == null) return null;
        try
        {
            var loader = new FileTex(dir);
            Atlas at;
            var atlasText = File.ReadAllText(atlas).TrimStart();
            using (var r = new StringReader(atlasText))
                at = new Atlas(r, dir, loader);
            // Boss 本体和角色图集先 FlipV；音符仍走绘制时的 1-v。
            var flip = BossPackKey(key) || CharPackKey(key);
            if (flip) at.FlipV();
            var attach = new AtlasAttachmentLoader(at);
            var scale = ReadScale(dir);
            SkeletonData data;
            if (skel.EndsWith(".json", System.StringComparison.OrdinalIgnoreCase))
            {
                var json = new SkeletonJson(attach) { Scale = scale };
                data = json.ReadSkeletonData(skel);
            }
            else
            {
                var bin = new SkeletonBinary(attach) { Scale = scale };
                data = bin.ReadSkeletonData(skel);
            }
            var pack = new Pack { Data = data, Atlas = at, VFlipped = flip };
            foreach (var page in at.Pages)
            {
                if (page.rendererObject is Texture2D tex)
                    pack.Mats[tex] = NewMat(tex);
            }
            return pack;
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning("MdSpine " + scene + "/" + key + " " + ex.Message);
            return null;
        }
    }

    static bool BossPackKey(string key)
    {
        if (string.IsNullOrEmpty(key) || key.Length < 4) return false;
        for (var i = 0; i < 4; i++)
            if (!char.IsDigit(key[i])) return false;
        if (key[2] != '0' || key[3] != '1') return false;
        return key.Length == 4 || key[4] == '_';
    }

    static bool CharPackKey(string key)
        => !string.IsNullOrEmpty(key) && key.StartsWith("char_", System.StringComparison.OrdinalIgnoreCase);

    public static Material NewMat(Texture2D tex)
    {
        if (_shader == null) Ensure();
        var sh = _shader != null ? _shader : Shader.Find("MusePSona/SpinePMA") ?? Shader.Find("Sprites/Default");
        var m = new Material(sh);
        MdMat.SetTex(m, tex);
        m.color = Color.white;
        m.renderQueue = 3000;
        if (m.HasProperty("_SrcBlend")) m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
        if (m.HasProperty("_DstBlend")) m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        return m;
    }

    sealed class FileTex : TextureLoader
    {
        readonly string _dir;
        public FileTex(string dir) { _dir = dir; }
        public void Load(AtlasPage page, string path)
        {
            var p = path;
            if (!File.Exists(p))
            {
                var stem = Path.GetFileNameWithoutExtension(path);
                p = Path.Combine(_dir, stem + ".png");
                if (!File.Exists(p)) p = Path.Combine(_dir, Path.GetFileName(path));
            }
            if (!File.Exists(p)) return;
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.alphaIsTransparency = false;
            tex.LoadImage(File.ReadAllBytes(p), false);
            tex.alphaIsTransparency = false;
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.name = Path.GetFileNameWithoutExtension(p);
            page.rendererObject = tex;
            if (page.width == 0) page.width = tex.width;
            if (page.height == 0) page.height = tex.height;
        }
        public void Unload(object texture)
        {
            if (texture is Texture2D t) Object.Destroy(t);
        }
    }
}

public sealed class MdSpineView : MonoBehaviour
{
    public MdSpine.Pack Source;
    public Skeleton Skeleton;
    public Spine.AnimationState Anim;
    float[] _world = new float[128];
    public float VisualSize { get; private set; }
    public float VisualHeight { get; private set; }
    public Vector3 VisualCenter { get; private set; }
    public int SortQueue = 3000;
    public int SortOrder;
    string _clipName;
    readonly List<PageDraw> _pages = new();
    readonly SkeletonClipping _clipper = new SkeletonClipping();
    static readonly int[] QuadTris = { 0, 1, 2, 2, 3, 0 };

    sealed class PageDraw
    {
        public Texture2D Tex;
        public Mesh Mesh;
        public MeshFilter Filter;
        public MeshRenderer Rend;
        public readonly List<Vector3> V = new(128);
        public readonly List<Vector2> Uv = new(128);
        public readonly List<Color32> C = new(128);
        public readonly List<int> T = new(128);
        public bool Behind;
        public bool Additive;
        public bool Front;
        public void Clear() { V.Clear(); Uv.Clear(); C.Clear(); T.Clear(); }
    }

    public static MdSpineView Attach(Transform parent, MdSpine.Pack pack)
    {
        var go = new GameObject("spine");
        go.transform.SetParent(parent, false);
        var v = go.AddComponent<MdSpineView>();
        v.Source = pack;
        v.Skeleton = new Skeleton(pack.Data);
        v.WearPlaySkin();
        var mix = new Spine.AnimationStateData(pack.Data) { DefaultMix = 0f };
        v.Anim = new Spine.AnimationState(mix);
        return v;
    }

    string _sortLayer = "Default";

    public void SetLayer(int queue, int order)
    {
        SortQueue = queue;
        SortOrder = order;
        foreach (var p in _pages)
            ApplyPageLayer(p);
    }

    public void SetSortLayer(string layer)
    {
        _sortLayer = string.IsNullOrEmpty(layer) ? "Default" : layer;
        foreach (var p in _pages)
            ApplyPageLayer(p);
    }

    public void PoseSetup()
    {
        _clipName = null;
        Anim.ClearTracks();
        Skeleton.SetToSetupPose();
        if (!ComboSkel())
        {
            if (!GirlSkel())
            {
                UncensorSlots();
                EnsureOfficialFace();
            }
            FillOriginSlots();
            if (!GirlSkel()) FillMikuSlots();
        }
        Skeleton.UpdateWorldTransform();
        Build();
    }

    public void PoseTime(string clip, float time, bool loop)
    {
        var missing = string.IsNullOrEmpty(clip) || Skeleton.Data.FindAnimation(clip) == null;
        if (missing)
        {
            if (!string.IsNullOrEmpty(clip) && OutLike(clip)) return;
            clip = PickIdle();
        }
        if (string.IsNullOrEmpty(clip)) return;
        Skeleton.A = 1f;
        if (_clipName != clip)
        {
            _clipName = clip;
            Anim.SetAnimation(0, clip, loop);
        }
        var entry = Anim.GetCurrent(0);
        if (entry != null)
        {
            entry.Loop = loop;
            entry.MixDuration = 0f;
            entry.MixTime = 0f;
            if (time < 0f) time = 0f;
            if (!loop && time > entry.AnimationEnd) time = entry.AnimationEnd;
            entry.TrackTime = time;
            entry.TimeScale = 0f;
        }
        Skeleton.SetToSetupPose();
        Anim.Apply(Skeleton);
        Skeleton.UpdateWorldTransform();
        // 1014 出场会把第二套头/盔/头发清成空。FillMikuSlots 按 setup 名填回去，那些槽会在爆炸骨上漂。
        // 0905 眼睛在 origin，不在 default。双押有 laser，以前整段跳过 FillOrigin，鸭子没眼睛。
        if (!OutLike(clip) && !ComboSkel())
        {
            if (!GirlSkel() && !DualOrPendulum())
            {
                UncensorSlots();
                if (!BossSkel()) EnsureOfficialFace();
            }
            if (GirlSkel() || !BossSkel())
                FillOriginSlots();
            else
                FillBossParts();
        }
        else if (OutLike(clip) && PendulumWoodenSlot())
        {
            // 0315 锤面在 origin。out_p 只动骨骼，不写附件；跳过补槽就只剩绳子。
            FillOriginSlots();
        }
        Skeleton.UpdateWorldTransform();
        Build();
    }

    public void PoseLoop(string clip, float time)
    {
        if (Skeleton == null || Anim == null) return;
        var hasClip = false;
        if (string.IsNullOrEmpty(clip) || Skeleton.Data.FindAnimation(clip) == null)
            clip = PickIdle();
        if (!string.IsNullOrEmpty(clip)) hasClip = true;
        Skeleton.A = 1f;
        if (hasClip)
        {
            if (_clipName != clip)
            {
                _clipName = clip;
                Anim.SetAnimation(0, clip, true);
            }
            var entry = Anim.GetCurrent(0);
            if (entry != null)
            {
                entry.Loop = true;
                entry.MixDuration = 0f;
                entry.MixTime = 0f;
                entry.TimeScale = 0f;
                var dur = entry.AnimationEnd;
                if (dur > 0.01f)
                {
                    time %= dur;
                    if (time < 0f) time += dur;
                }
                if (time < 0f) time = 0f;
                entry.TrackTime = time;
            }
            Skeleton.SetToSetupPose();
            Anim.Apply(Skeleton);
        }
        else
        {
            // 没有动画片段时（如 s08_moon），直接渲染 setup pose。
            _clipName = null;
            Anim.ClearTracks();
            Skeleton.SetToSetupPose();
        }
        if (GirlSkel() && !OutLike(clip ?? ""))
            FillOriginSlots();
        if (!hasClip && !OutLike(clip ?? "") && !ComboSkel())
        {
            if (!GirlSkel() && !BossSkel() && !DualOrPendulum())
            {
                UncensorSlots();
                EnsureOfficialFace();
            }
            if (GirlSkel() || !BossSkel())
                FillOriginSlots();
        }
        Skeleton.UpdateWorldTransform();
        Build();
    }

    public void Pose(PlayNote n, float chartNow, string clip)
    {
        // 官机：time = (now − 判定) + 判定帧/30。clip 比判定更长（in_boss_2_17 约 1.1s，判定在 17/30）。
        var time = chartNow - (float)n.TimeSec + Flight.DurationSec(n);
        if (time < 0f) time = 0f;
        PoseTime(clip, time, false);
    }

    public void SetMulA(float a)
    {
        if (Skeleton == null) return;
        Skeleton.A = Mathf.Clamp01(a);
        Build();
    }

    /// <summary>同刻 13/14 各一颗 0E 时，非保留轨只留脸和端点球。1205 的 laser_pink 是 58×268 整条，两轨各画一次会叠成 vis 长按。</summary>
    public void HideLaserSlots()
    {
        if (Skeleton == null) return;
        var changed = false;
        for (var i = 0; i < Skeleton.Slots.Count; i++)
        {
            var slot = Skeleton.Slots.Items[i];
            if (slot.Attachment == null) continue;
            var sn = slot.Data.Name ?? "";
            var an = slot.Attachment.Name ?? "";
            if (Hit(an, sn, "laser_ball") || Hit(an, sn, "laser_circle")) continue;
            if (!BeamSlot(an, sn) && !Hit(an, sn, "star")) continue;
            slot.Attachment = null;
            changed = true;
        }
        if (changed) Build();
    }

    /// <summary>
    /// 双押连线：03/08/09 空中包朝地面（dir −1），scene_12 地面包朝空中（dir +1）。
    /// 0805 的 laser 就在 body 上，不能用 laser 相对 body 的 Y 猜方向。
    /// 0305 头会转，先把 laser 世界旋转掰回 0；平移走父骨 WorldToLocal，避免沿头骨斜轴推。
    /// </summary>
    public void StretchLaserTo(float span, float dir)
    {
        if (Skeleton == null || span < 0.2f) return;
        var bone = Skeleton.FindBone("laser");
        if (bone == null) return;
        if (Mathf.Abs(bone.WorldRotationX) > 0.5f)
        {
            bone.Rotation -= bone.WorldRotationX;
            Skeleton.UpdateWorldTransform();
        }
        var face = LaneFaceCenter();
        if (face == null) return;
        dir = dir >= 0f ? 1f : -1f;
        var sy = Mathf.Abs(transform.lossyScale.y);
        if (sy < 0.01f) sy = 1f;
        var skelSpan = span / sy;
        var wantA = face.Value.y;
        var wantB = wantA + dir * skelSpan;
        var wantMin = Mathf.Min(wantA, wantB);
        var wantMax = Mathf.Max(wantA, wantB);
        if (!PinkColumnBounds(out var minX, out var maxX, out var minY, out var maxY)) return;
        var h = maxY - minY;
        if (h < 0.002f) return;
        var mul = (wantMax - wantMin) / h;
        var laser2 = Skeleton.FindBone("laser2");
        if (Mathf.Abs(mul - 1f) > 0.02f)
        {
            bone.ScaleY *= mul;
            if (laser2 != null) laser2.ScaleY *= mul;
            Skeleton.UpdateWorldTransform();
            if (!PinkColumnBounds(out minX, out maxX, out minY, out maxY)) return;
        }
        var shiftX = face.Value.x - (minX + maxX) * 0.5f;
        var shiftY = (wantMin + wantMax) * 0.5f - (minY + maxY) * 0.5f;
        if (Mathf.Abs(shiftX) > 0.005f || Mathf.Abs(shiftY) > 0.01f)
        {
            NudgeBoneWorld(bone, bone.WorldX + shiftX, bone.WorldY + shiftY);
            if (laser2 != null && laser2.Parent != bone)
                NudgeBoneWorld(laser2, laser2.WorldX + shiftX, laser2.WorldY + shiftY);
            Skeleton.UpdateWorldTransform();
        }
        Build();
    }

    static void NudgeBoneWorld(Bone bone, float worldX, float worldY)
    {
        if (bone.Parent != null)
        {
            bone.Parent.WorldToLocal(worldX, worldY, out var lx, out var ly);
            bone.X = lx;
            bone.Y = ly;
        }
        else
        {
            bone.X = worldX;
            bone.Y = worldY;
        }
    }

    bool PinkColumnBounds(out float minX, out float maxX, out float minY, out float maxY)
    {
        minX = minY = 1e9f;
        maxX = maxY = -1e9f;
        var any = false;
        for (var i = 0; i < Skeleton.Slots.Count; i++)
        {
            var slot = Skeleton.Slots.Items[i];
            if (slot.Attachment == null) continue;
            var sn = slot.Data.Name ?? "";
            var an = slot.Attachment.Name ?? "";
            if (!ColumnSlot(an, sn)) continue;
            if (slot.Attachment is RegionAttachment reg)
            {
                if (_world.Length < 8) _world = new float[8];
                reg.ComputeWorldVertices(slot.Bone, _world, 0);
                for (var k = 0; k < 8; k += 2)
                {
                    if (_world[k] < minX) minX = _world[k];
                    if (_world[k] > maxX) maxX = _world[k];
                    if (_world[k + 1] < minY) minY = _world[k + 1];
                    if (_world[k + 1] > maxY) maxY = _world[k + 1];
                    any = true;
                }
            }
            else if (slot.Attachment is MeshAttachment mesh)
            {
                var n = mesh.WorldVerticesLength;
                if (_world.Length < n) _world = new float[n];
                mesh.ComputeWorldVertices(slot, _world);
                for (var k = 0; k < n; k += 2)
                {
                    if (_world[k] < minX) minX = _world[k];
                    if (_world[k] > maxX) maxX = _world[k];
                    if (_world[k + 1] < minY) minY = _world[k + 1];
                    if (_world[k + 1] > maxY) maxY = _world[k + 1];
                    any = true;
                }
            }
        }
        return any;
    }

    static bool ColumnSlot(string an, string sn)
    {
        if (Hit(an, sn, "explode") || Hit(an, sn, "circle") || Hit(an, sn, "line")
            || Hit(an, sn, "shine") || Hit(an, sn, "handle"))
            return false;
        if (Hit(an, sn, "laser_pink")) return true;
        var leaf = Leaf(an.Length > 0 ? an : sn);
        return sn == "laser" || leaf == "laser";
    }

    /// <summary>
    /// 双押对轨：莲花用花心，鸭子/恶魔用头，不要用飞碟。0905 飞碟在头下面，对飞碟会把头抬离轨道、光条变短。
    /// 0305 头贴图中心在骨右上，对骨会对不齐下轨脸；用头槽中心。
    /// </summary>
    public Vector3? LaneFaceCenter()
    {
        if (Skeleton == null) return null;
        Vector3? heart = null, head = null, body = null, saucer = null, petal = null;
        foreach (var s in Skeleton.Slots)
        {
            if (s.Attachment == null) continue;
            var sn = s.Data.Name ?? "";
            var an = s.Attachment.Name ?? "";
            if (IsFxSlot(an, sn) || Hit(an, sn, "hair") || Hit(an, sn, "hat") || Hit(an, sn, "eye"))
                continue;
            if (Hit(an, sn, "FlowerHeart"))
            {
                if (heart == null) heart = SlotCenter(s);
                continue;
            }
            if (sn == "head" || an == "head")
            {
                if (head == null)
                    head = SlotCenter(s) ?? new Vector3(s.Bone.WorldX, s.Bone.WorldY, 0f);
                continue;
            }
            if (sn == "body" || an == "body"
                || Hit(an, sn, "taiji") || Hit(an, sn, "bagua")
                || Hit(an, sn, "square") || Hit(an, sn, "round") || Hit(an, sn, "roung")
                || sn == "AA" || sn == "BB" || sn == "AA2" || sn == "BB2" || sn == "BB3")
            {
                if (body == null) body = SlotCenter(s) ?? new Vector3(s.Bone.WorldX, s.Bone.WorldY, 0f);
                continue;
            }
            if (Hit(an, sn, "feidie"))
            {
                if (saucer == null) saucer = SlotCenter(s);
                continue;
            }
            if (Hit(an, sn, "petal_H") && petal == null)
                petal = SlotCenter(s);
        }
        if (heart != null) return heart;
        if (head != null) return head;
        if (body != null) return body;
        if (saucer != null) return saucer;
        if (petal != null) return petal;
        var b = Skeleton.FindBone("FlowerHeart") ?? Skeleton.FindBone("FlowerHeart2")
            ?? Skeleton.FindBone("head") ?? Skeleton.FindBone("body");
        return b == null ? (Vector3?)null : new Vector3(b.WorldX, b.WorldY, 0f);
    }

    /// <summary>
    /// Boss 落点用投影骨，不用投影贴图中心。
    /// 0401 的车影被拆成多段，第一段中心在车尾（x≈10），贴图中心会把人拽到判定线左侧。骨仍在脚下。
    /// </summary>
    public Vector3? ShadowPivot()
    {
        if (Skeleton == null) return null;
        var bone = Skeleton.FindBone("shadow");
        if (bone != null) return new Vector3(bone.WorldX, bone.WorldY, 0f);
        return ShadowCenter();
    }

    /// <summary>地面投影槽。官机这条椭圆贴在轨道上，本体在它上方。</summary>
    public Vector3? ShadowCenter()
    {
        if (Skeleton == null) return null;
        foreach (var s in Skeleton.Slots)
        {
            var sn = s.Data.Name ?? "";
            var an = s.Attachment != null ? s.Attachment.Name ?? "" : "";
            if (Hit(an, sn, "body_shadow")) continue;
            if (!Hit(an, sn, "shadow")) continue;
            return SlotCenter(s) ?? new Vector3(s.Bone.WorldX, s.Bone.WorldY, 0f);
        }
        return null;
    }

    /// <summary>地面齿轮要对到红线上的那条底边。mask 从圆心裁掉下半时，可见底边是裁切线。</summary>
    public float? SawBottomY()
    {
        var saw = Skeleton?.FindSlot("sawtooth");
        if (saw?.Attachment == null || saw.Bone == null) return null;
        float meshMin = float.MaxValue;
        if (saw.Attachment is RegionAttachment reg)
        {
            reg.ComputeWorldVertices(saw.Bone, _world, 0);
            for (var i = 1; i < 8; i += 2)
                if (_world[i] < meshMin) meshMin = _world[i];
        }
        else if (saw.Attachment is MeshAttachment mesh)
        {
            var count = mesh.WorldVerticesLength;
            if (_world.Length < count) _world = new float[count];
            mesh.ComputeWorldVertices(saw, _world);
            for (var i = 1; i < count; i += 2)
                if (_world[i] < meshMin) meshMin = _world[i];
        }
        else return null;
        if (meshMin > 1e8f) return null;
        var edge = meshMin;
        for (var i = 0; i < Skeleton.Slots.Count; i++)
        {
            var s = Skeleton.Slots.Items[i];
            if (s.Attachment is not ClippingAttachment clip) continue;
            var sn = s.Data.Name ?? "";
            if (sn.IndexOf("mask", System.StringComparison.OrdinalIgnoreCase) < 0) continue;
            var count = clip.WorldVerticesLength;
            if (count < 2) continue;
            if (_world.Length < count) _world = new float[count];
            clip.ComputeWorldVertices(s, _world);
            var cmin = float.MaxValue;
            for (var v = 1; v < count; v += 2)
                if (_world[v] < cmin) cmin = _world[v];
            if (cmin < 1e8f && cmin > meshMin + 0.02f) edge = cmin;
        }
        return edge;
    }

    public Vector3 BodyOffset()
    {
        foreach (var s in Skeleton.Slots)
        {
            var sn = s.Data.Name ?? "";
            var an = s.Attachment != null ? s.Attachment.Name ?? "" : "";
            if (!Hit(an, sn, "wooden")) continue;
            // 锤面中心对轨；吊点骨在槽口，对骨会把整只锤吊到轨道下面。
            var mid = SlotCenter(s);
            if (mid != null) return mid.Value;
            var wood = s.Bone;
            return new Vector3(wood.WorldX, wood.WorldY, 0f);
        }
        if (StarSwing())
        {
            // scene_07 的 0715 没有 wooden：锤面是 down（粉星），up 是吊绳。
            // 用整段绳子的包围盒会把锤面甩到轨道旁边。
            var bob = Skeleton.FindSlot("down");
            var mid = bob == null ? null : SlotCenter(bob);
            if (mid != null) return mid.Value;
        }
        var saw = Skeleton.FindSlot("sawtooth");
        if (saw != null)
        {
            // 齿轮的 line / scrap 会把包围盒中心拽离锯盘。地面齿轮要对锯盘中心。
            var mid = SlotCenter(saw);
            if (mid != null) return mid.Value;
        }
        if (VisualHeight > 0.05f && DualOrPendulum())
            return VisualCenter;
        var collect = SlotWorldCenter("hp") ?? SlotWorldCenter("score")
            ?? SlotWorldCenter("body") ?? FirstBodyMeshCenter();
        if (collect != null) return collect.Value;
        if (VisualHeight > 0.05f) return VisualCenter;
        var b = Skeleton.FindBone("size")
            ?? Skeleton.FindBone("body")
            ?? Skeleton.FindBone("head")
            ?? Skeleton.FindBone("X")
            ?? Skeleton.RootBone;
        if (b == null) return Vector3.zero;
        return new Vector3(b.WorldX, b.WorldY, 0f);
    }

    Vector3? FirstBodyMeshCenter()
    {
        foreach (var s in Skeleton.Slots)
        {
            if (s.Attachment == null || s.Bone == null) continue;
            var sn = s.Data.Name ?? "";
            var an = s.Attachment.Name ?? "";
            if (IsFxSlot(an, sn)) continue;
            if (!(Hit(an, sn, "body") || Hit(an, sn, "bomb"))) continue;
            return SlotCenter(s);
        }
        return null;
    }

    Vector3? SlotWorldCenter(string name)
    {
        var slot = Skeleton.FindSlot(name);
        return slot == null ? null : SlotCenter(slot);
    }

    Vector3? SlotCenter(Slot slot)
    {
        if (slot?.Attachment == null || slot.Bone == null) return null;
        if (slot.Attachment is RegionAttachment reg)
        {
            reg.ComputeWorldVertices(slot.Bone, _world, 0);
            return new Vector3((_world[0] + _world[4]) * 0.5f, (_world[1] + _world[5]) * 0.5f, 0f);
        }
        if (slot.Attachment is MeshAttachment mesh)
        {
            var count = mesh.WorldVerticesLength;
            if (_world.Length < count) _world = new float[count];
            mesh.ComputeWorldVertices(slot, _world);
            float minX = 1e9f, minY = 1e9f, maxX = -1e9f, maxY = -1e9f;
            for (var i = 0; i < count; i += 2)
            {
                if (_world[i] < minX) minX = _world[i];
                if (_world[i + 1] < minY) minY = _world[i + 1];
                if (_world[i] > maxX) maxX = _world[i];
                if (_world[i + 1] > maxY) maxY = _world[i + 1];
            }
            return new Vector3((minX + maxX) * 0.5f, (minY + maxY) * 0.5f, 0f);
        }
        return new Vector3(slot.Bone.WorldX, slot.Bone.WorldY, 0f);
    }

    /// <summary>0715 粉星摆锤。_b 包仍走 wooden，不要当成这一种。灯笼是侧视，挂钩在绳子那一侧，不要再转 90°。</summary>
    bool StarSwing()
    {
        var n = Skeleton?.Data?.Name ?? "";
        if (n.IndexOf("15_road_b", System.StringComparison.OrdinalIgnoreCase) >= 0
            || n.IndexOf("15_air_b", System.StringComparison.OrdinalIgnoreCase) >= 0)
            return false;
        if (n.IndexOf("15_road", System.StringComparison.OrdinalIgnoreCase) < 0
            && n.IndexOf("15_air", System.StringComparison.OrdinalIgnoreCase) < 0)
            return false;
        return Skeleton.FindSlot("down") != null && Skeleton.FindSlot("up") != null;
    }

    bool ComboSkel()
    {
        var n = Skeleton?.Data?.Name ?? "";
        if (n.IndexOf("fx_combo", System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
        var p = transform.parent != null ? transform.parent.name : "";
        return p == "ComboHud";
    }

    bool GirlSkel()
    {
        var p = transform.parent != null ? transform.parent.name : "";
        return p == "GirlTest";
    }

    bool DualOrPendulum()
    {
        if (Skeleton.FindBone("laser_body") != null) return true;
        if (StarSwing()) return true;
        foreach (var s in Skeleton.Slots)
        {
            var sn = s.Data.Name ?? "";
            var an = s.Attachment != null ? s.Attachment.Name ?? "" : "";
            if (Hit(an, sn, "wooden") || Hit(an, sn, "laser")) return true;
        }
        return false;
    }

    bool PendulumWoodenSlot()
    {
        if (StarSwing()) return true;
        for (var i = 0; i < Skeleton.Slots.Count; i++)
        {
            var sn = Skeleton.Slots.Items[i].Data.Name ?? "";
            if (Hit("", sn, "wooden")) return true;
        }
        return false;
    }

    static bool OutLike(string clip)
    {
        if (string.IsNullOrEmpty(clip)) return false;
        return clip.StartsWith("out") || clip.StartsWith("hurt")
            || clip.IndexOf("hurt", System.StringComparison.OrdinalIgnoreCase) >= 0
            || clip.IndexOf("explode", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    string PickIdle()
    {
        if (GirlSkel())
        {
            var g = Skeleton.Data.FindAnimation("standby")
                ?? Skeleton.Data.FindAnimation("idle")
                ?? Skeleton.Data.FindAnimation("wait");
            if (g != null) return g.Name;
        }
        if (BossSkel())
        {
            var stand = Skeleton.Data.FindAnimation("standby") ?? Skeleton.Data.FindAnimation("normal_standby");
            if (stand != null) return stand.Name;
        }
        foreach (var a in Skeleton.Data.Animations)
            if (a.Name.StartsWith("in_")) return a.Name;
        return Skeleton.Data.Animations.Count > 0 ? Skeleton.Data.Animations.Items[0].Name : null;
    }

    bool BossSkel()
    {
        if (Skeleton?.Data == null) return false;
        if (Skeleton.FindSlot("bossA_body") != null || Skeleton.FindSlot("bossB_body") != null) return true;
        return Skeleton.Data.FindAnimation("standby") != null
            && (Skeleton.Data.FindAnimation("far_atk_1_standby") != null
                || Skeleton.Data.FindAnimation("far_atk_1") != null
                || Skeleton.Data.FindAnimation("close_atk_1_48") != null);
    }

    bool BossShotSkel()
    {
        var n = Skeleton?.Data?.Name ?? "";
        if (n.Length < 4) n = transform.parent != null ? transform.parent.name : "";
        return n.IndexOf("06_", System.StringComparison.OrdinalIgnoreCase) >= 0
            || n.IndexOf("07_", System.StringComparison.OrdinalIgnoreCase) >= 0
            || n.IndexOf("08_", System.StringComparison.OrdinalIgnoreCase) >= 0
            || n.IndexOf("09_", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    void Build()
    {
        foreach (var p in _pages) p.Clear();
        VisualSize = 0f;
        VisualHeight = 0f;
        VisualCenter = Vector3.zero;
        float bMinX = 1e9f, bMinY = 1e9f, bMaxX = -1e9f, bMaxY = -1e9f;
        var bodyAny = false;
        var pendulum = false;
        for (var i = 0; i < Skeleton.Slots.Count; i++)
        {
            var s = Skeleton.Slots.Items[i];
            var sn = s.Data.Name ?? "";
            var an = s.Attachment != null ? s.Attachment.Name ?? "" : "";
            if (Hit(an, sn, "wooden") || StarSwing()) { pendulum = true; break; }
        }
        var beamUnderFace = !BossSkel() && BeamUnderFace();
        var draw = Skeleton.DrawOrder.Items;
        var n = Skeleton.DrawOrder.Count;
        for (var i = 0; i < n; i++)
        {
            var slot = draw[i];
            var att = slot.Attachment;
            if (att is ClippingAttachment clip)
            {
                // 0705 dust_cut 的 EndSlot 就是自己，裁到循环结束会把方块/圆整段剪掉。
                var end = clip.EndSlot;
                var broken = end == null || end == slot.Data || end.Index <= slot.Data.Index;
                // 0101 飞碟用大轮廓当 clip 时，座舱 Weapon_box / ufo_end 会被剪成空环。
                if (!pendulum && !broken && !BossSkel()) _clipper.ClipStart(slot, clip);
                continue;
            }
            if (att == null)
            {
                _clipper.ClipEnd(slot);
                continue;
            }
            var nm = att.Name ?? "";
            var sn = slot.Data.Name ?? "";
            var keepStrip = BossSkel() || BossShotSkel() || IsFxSlot(nm, sn) || Hit(nm, sn, "wooden")
                || (pendulum && (Hit(nm, sn, "down") || Hit(nm, sn, "up")));
            var leaf = Leaf(nm.Length > 0 ? nm : sn);
            var keepCore = IsBody(nm, sn)
                || string.Equals(leaf, "aa", System.StringComparison.OrdinalIgnoreCase)
                || string.Equals(leaf, "bb", System.StringComparison.OrdinalIgnoreCase)
                || string.Equals(leaf, "aaa", System.StringComparison.OrdinalIgnoreCase)
                || string.Equals(leaf, "bbb", System.StringComparison.OrdinalIgnoreCase)
                || (leaf.Length >= 2 && (leaf.StartsWith("ac", System.StringComparison.OrdinalIgnoreCase)
                    || leaf.StartsWith("bc", System.StringComparison.OrdinalIgnoreCase)));
            BindAtlas(att);
            var additive = slot.Data.BlendMode == Spine.BlendMode.Additive;
            // 外环/灯/罩顶/玻璃轮廓在人前面。座舱台 ufo_end_2 留在罩内，不要跟外壳一起盖住内部。
            var hullFront = BossSkel() && (Hit(nm, sn, "ufo_end_1") || Hit(nm, sn, "ufo_light")
                || Hit(nm, sn, "ufo_top") || Hit(nm, sn, "ufo_body"));
            if (att is RegionAttachment reg)
            {
                var tex = TexOf(reg.RendererObject);
                if (tex != null)
                {
                    reg.ComputeWorldVertices(slot.Bone, _world, 0);
                    if (keepStrip || keepCore || !StripLike(_world, 8))
                        Push(Bucket(tex, beamUnderFace && BeamBehind(nm, sn), additive, hullFront), slot, _world, 8, reg.UVs, QuadTris, QuadTris.Length, Tint(slot, reg.R, reg.G, reg.B, reg.A), IsBody(nm, sn) || Hit(nm, sn, "ufo"), ref bMinX, ref bMinY, ref bMaxX, ref bMaxY, ref bodyAny);
                }
            }
            else if (att is MeshAttachment mesh)
            {
                var tex = TexOf(mesh.RendererObject);
                if (tex != null)
                {
                    var count = mesh.WorldVerticesLength;
                    if (_world.Length < count) _world = new float[count];
                    mesh.ComputeWorldVertices(slot, _world);
                    if (keepStrip || keepCore || !StripLike(_world, count))
                        Push(Bucket(tex, beamUnderFace && BeamBehind(nm, sn), additive, hullFront), slot, _world, count, mesh.UVs, mesh.Triangles, mesh.Triangles.Length, Tint(slot, mesh.R, mesh.G, mesh.B, mesh.A), IsBody(nm, sn) || Hit(nm, sn, "ufo"), ref bMinX, ref bMinY, ref bMaxX, ref bMaxY, ref bodyAny);
                }
            }
            _clipper.ClipEnd(slot);
        }
        _clipper.ClipEnd();
        foreach (var p in _pages) Upload(p);
        if (bodyAny)
        {
            VisualHeight = bMaxY - bMinY;
            VisualSize = Mathf.Max(bMaxX - bMinX, VisualHeight);
            VisualCenter = new Vector3((bMinX + bMaxX) * 0.5f, (bMinY + bMaxY) * 0.5f, 0f);
        }
        else
        {
            float minX = 1e9f, minY = 1e9f, maxX = -1e9f, maxY = -1e9f;
            var any = false;
            foreach (var p in _pages)
            {
                for (var i = 0; i < p.V.Count; i++)
                {
                    var q = p.V[i];
                    if (q.x < minX) minX = q.x;
                    if (q.y < minY) minY = q.y;
                    if (q.x > maxX) maxX = q.x;
                    if (q.y > maxY) maxY = q.y;
                    any = true;
                }
            }
            if (any)
            {
                VisualHeight = maxY - minY;
                VisualSize = Mathf.Max(maxX - minX, VisualHeight);
                VisualCenter = new Vector3((minX + maxX) * 0.5f, (minY + maxY) * 0.5f, 0f);
            }
        }
    }

    bool HasHexieVariant()
    {
        for (var i = 0; i < Skeleton.Slots.Count; i++)
        {
            var s = Skeleton.Slots.Items[i];
            if (Hit(s.Data.Name ?? "", s.Data.AttachmentName ?? "", "hexie")) return true;
            if (s.Attachment != null && Hit(s.Attachment.Name ?? "", "", "hexie")) return true;
        }
        return false;
    }

    /// <summary>scene_10 默认局用 default+Miku。测试角色的身体在 origin，不在 default。hexie 不穿。</summary>
    void WearPlaySkin()
    {
        var miku = Skeleton.Data.FindSkin("Miku");
        var origin = GirlSkel() ? Skeleton.Data.FindSkin("origin") : null;
        if (miku == null && origin == null) return;
        var play = new Skin("play");
        if (Skeleton.Data.DefaultSkin != null) play.AddSkin(Skeleton.Data.DefaultSkin);
        if (miku != null) play.AddSkin(miku);
        if (origin != null) play.AddSkin(origin);
        Skeleton.SetSkin(play);
        Skeleton.SetSlotsToSetupPose();
    }

    /// <summary>仅 Boss 本体。0301 的裙子和大腿在 origin，不在 default。hexie 不穿。</summary>
    public void WearBossSkin()
    {
        var origin = Skeleton.Data.FindSkin("origin");
        if (origin == null) return;
        var play = new Skin("boss");
        if (Skeleton.Data.DefaultSkin != null) play.AddSkin(Skeleton.Data.DefaultSkin);
        play.AddSkin(origin);
        Skeleton.SetSkin(play);
        Skeleton.SetSlotsToSetupPose();
    }

    /// <summary>只补 setup 写了附件名、却还空着的槽。1004 的 1_body/2_body/3_body 出场是空的，不能按槽名填，否则三套受击件一起漂在场上。</summary>
    void FillMikuSlots()
    {
        var miku = Skeleton.Data.FindSkin("Miku");
        if (miku == null) return;
        for (var i = 0; i < Skeleton.Slots.Count; i++)
        {
            var slot = Skeleton.Slots.Items[i];
            if (slot.Attachment != null) continue;
            var name = slot.Data.AttachmentName;
            if (string.IsNullOrEmpty(name)) continue;
            var att = miku.GetAttachment(slot.Data.Index, name);
            if (att == null || att is ClippingAttachment) continue;
            slot.Attachment = att;
        }
    }

    /// <summary>远攻片段会把飞碟座舱/环槽清掉。补回 default 里的 ufo/武器件。</summary>
    void FillBossParts()
    {
        var skin = Skeleton.Skin ?? Skeleton.Data.DefaultSkin;
        if (skin == null) return;
        for (var i = 0; i < Skeleton.Slots.Count; i++)
        {
            var slot = Skeleton.Slots.Items[i];
            if (slot.Attachment != null) continue;
            var sn = slot.Data.Name ?? "";
            var an = slot.Data.AttachmentName ?? "";
            if (!KeepBossPart(sn, an)) continue;
            var att = skin.GetAttachment(slot.Data.Index, an);
            if (att == null && Skeleton.Data.DefaultSkin != null && Skeleton.Data.DefaultSkin != skin)
                att = Skeleton.Data.DefaultSkin.GetAttachment(slot.Data.Index, an);
            if (att == null || att is ClippingAttachment) continue;
            slot.Attachment = att;
            slot.A = 1f;
            slot.R = 1f;
            slot.G = 1f;
            slot.B = 1f;
            BindAtlas(att);
        }
    }

    static bool KeepBossPart(string sn, string an)
    {
        if (Hit(an, sn, "weapon") || Hit(an, sn, "box")) return false;
        if (IsFxSlot(an, sn)) return false;
        return Hit(an, sn, "ufo") || Hit(an, sn, "wing")
            || Hit(an, sn, "foot") || Hit(an, sn, "ring")
            || Hit(an, sn, "cloak") || Hit(an, sn, "cloth") || Hit(an, sn, "tail")
            || Hit(an, sn, "body") || Hit(an, sn, "head") || Hit(an, sn, "hand")
            || Hit(an, sn, "leg") || Hit(an, sn, "neck") || Hit(an, sn, "mouth")
            || Hit(an, sn, "eye") || Hit(an, sn, "hair") || Hit(an, sn, "horn");
    }

    /// <summary>0303 的锯盘在 origin 皮肤，不在默认皮肤。不补的话槽是空的，只剩一根 line。</summary>
    void FillOriginSlots()
    {
        var origin = Skeleton.Data.FindSkin("origin");
        if (origin == null) return;
        for (var i = 0; i < Skeleton.Slots.Count; i++)
        {
            var slot = Skeleton.Slots.Items[i];
            if (slot.Attachment != null) continue;
            var name = slot.Data.AttachmentName;
            if (string.IsNullOrEmpty(name)) continue;
            var att = origin.GetAttachment(slot.Data.Index, name);
            if (att == null || att is ClippingAttachment) continue;
            if (CensoredName(att.Name ?? "")) continue;
            slot.Attachment = att;
        }
    }

    void UncensorSlots()
    {
        if (!HasHexieVariant()) return;
        var skins = CollectSkins();
        for (var i = 0; i < Skeleton.Slots.Count; i++)
        {
            var slot = Skeleton.Slots.Items[i];
            var sn = slot.Data.Name ?? "";
            Attachment found = null;
            if (sn.IndexOf("hexie", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                if (slot.Attachment is ClippingAttachment) continue;
                var stem = HexieStem(sn);
                Slot home = null;
                for (var j = 0; j < Skeleton.Slots.Count; j++)
                {
                    var other = Skeleton.Slots.Items[j];
                    var on = other.Data.Name ?? "";
                    if (on.IndexOf("hexie", System.StringComparison.OrdinalIgnoreCase) >= 0) continue;
                    if (string.Equals(Leaf(on), stem, System.StringComparison.OrdinalIgnoreCase))
                    {
                        home = other;
                        break;
                    }
                }
                found = FindOnSlot(skins, home ?? slot, stem);
                if (home != null && found != null)
                {
                    home.Attachment = found;
                    slot.Attachment = null;
                }
                else if (found != null)
                    slot.Attachment = found;
                continue;
            }

            var cur = slot.Attachment;
            var curName = cur != null ? cur.Name ?? "" : "";
            var want = Leaf(sn);
            if (cur != null && curName.IndexOf("hexie", System.StringComparison.OrdinalIgnoreCase) >= 0)
                want = HexieStem(curName);
            else if (string.IsNullOrEmpty(want) || want == "hexie")
                want = Leaf(slot.Data.AttachmentName ?? "");

            found = FindOnSlot(skins, slot, want);
            if (found == null && (want == "head" || want == "body" || want == "head2"))
                found = FindOnSlot(skins, slot, "");

            var leaf = Leaf(sn);
            if (leaf == "head" || leaf == "head2" || leaf.StartsWith("eyes", System.StringComparison.OrdinalIgnoreCase))
                continue;

            if (cur == null || curName.IndexOf("hexie", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                if (found != null) slot.Attachment = found;
            }
        }
    }

    void EnsureOfficialFace()
    {
        string[] slots = { "head", "eyes1", "eyes2", "eye" };
        foreach (var sn in slots)
        {
            var slot = Skeleton.FindSlot(sn);
            if (slot == null) continue;
            if (string.IsNullOrEmpty(slot.Data.AttachmentName) && slot.Attachment == null) continue;
            var pick = PickSlotAttachment(slot, Leaf(sn) != "eye");
            if (pick == null) continue;
            if (pick is MeshAttachment ma && ma.WorldVerticesLength < 6) continue;
            slot.Attachment = pick;
            slot.A = 1f; slot.R = 1f; slot.G = 1f; slot.B = 1f;
            BindAtlas(pick);
        }
    }

    Attachment PickSlotAttachment(Slot slot, bool preferHexie)
    {
        Attachment hexie = null, plain = null, any = null;
        var buf = new List<Skin.SkinEntry>();
        foreach (var skin in CollectSkins())
        {
            buf.Clear();
            skin.GetAttachments(slot.Data.Index, buf);
            foreach (var e in buf)
            {
                var a = e.Attachment;
                if (a == null || a is ClippingAttachment) continue;
                if (a is MeshAttachment mesh && mesh.WorldVerticesLength < 6) continue;
                any = a;
                var nm = e.Name ?? a.Name ?? "";
                if (CensoredName(nm)) hexie = a;
                else plain = a;
            }
        }
        var leaf = Leaf(slot.Data.AttachmentName ?? slot.Data.Name ?? "");
        var pre = PackPrefix();
        string[] aliases =
        {
            string.IsNullOrEmpty(pre) ? null : pre + leaf + "_hexie",
            leaf + "_hexie",
            string.IsNullOrEmpty(pre) ? null : pre + leaf,
            leaf,
            slot.Data.AttachmentName
        };
        foreach (var name in aliases)
        {
            if (string.IsNullOrEmpty(name)) continue;
            var a = Skeleton.GetAttachment(slot.Data.Index, name);
            if (a == null || a is ClippingAttachment) continue;
            if (a is MeshAttachment mesh && mesh.WorldVerticesLength < 6) continue;
            var nm = a.Name ?? name;
            if (CensoredName(nm)) hexie = hexie ?? a;
            else plain = plain ?? a;
            any = any ?? a;
        }
        if (preferHexie && hexie != null) return hexie;
        if (slot.Attachment != null) return slot.Attachment;
        return plain ?? any;
    }

    string PackPrefix()
    {
        for (var i = 0; i < Skeleton.Slots.Count; i++)
        {
            var att = Skeleton.Slots.Items[i].Attachment;
            string path = null;
            if (att is MeshAttachment mesh) path = mesh.Path ?? mesh.Name;
            else if (att is RegionAttachment ra) path = ra.Path ?? ra.Name;
            if (string.IsNullOrEmpty(path)) continue;
            var slash = path.LastIndexOf('/');
            if (slash > 0) return path.Substring(0, slash + 1);
        }
        var n = Skeleton.Data.Name ?? "";
        if (n.Length >= 4 && char.IsDigit(n[0]))
        {
            var code = n.Substring(0, 4);
            if (Source?.Atlas != null)
            {
                foreach (var folder in new[] { "/images/", "/image/" })
                {
                    var plain = code + folder;
                    foreach (var rg in Source.Atlas.Regions)
                    {
                        var nm = rg.name ?? "";
                        if (nm.StartsWith(plain, System.StringComparison.OrdinalIgnoreCase)
                            && nm.IndexOf("images_air", System.StringComparison.OrdinalIgnoreCase) < 0
                            && nm.IndexOf("images_road", System.StringComparison.OrdinalIgnoreCase) < 0)
                            return plain;
                    }
                }
            }
            var lane = n.IndexOf("road", System.StringComparison.OrdinalIgnoreCase) >= 0 ? "images_road/" : "images_air/";
            return code + "/" + lane;
        }
        return "";
    }

    AtlasRegion AliasRegion(string leaf)
    {
        if (string.IsNullOrEmpty(leaf) || Source?.Atlas == null) return null;
        // s03_fire 图集只有 Aa/Bb/fx，骨骼槽是 Ac1/Bc1/fx1。
        if (leaf.StartsWith("ac", System.StringComparison.OrdinalIgnoreCase)
            || string.Equals(leaf, "body1", System.StringComparison.OrdinalIgnoreCase)
            || string.Equals(leaf, "body2", System.StringComparison.OrdinalIgnoreCase))
            return Source.Atlas.FindRegion("Aa");
        if (leaf.StartsWith("bc", System.StringComparison.OrdinalIgnoreCase))
            return Source.Atlas.FindRegion("Bb");
        if (leaf == "fx" || leaf == "fx1")
            return Source.Atlas.FindRegion("fx");
        return null;
    }

    void BindAtlas(Attachment att)
    {
        if (att == null || Source == null || Source.Atlas == null) return;
        string path = null;
        if (att is MeshAttachment mesh) path = mesh.Path ?? mesh.Name;
        else if (att is RegionAttachment ra) path = ra.Path ?? ra.Name;
        else return;
        var have = att is IHasRendererObject ho ? TexOf(ho.RendererObject) : null;
        if (have != null) return;
        AtlasRegion region = Source.Atlas.FindRegion(path);
        if (region == null && path != null && path.IndexOf('/') < 0)
        {
            var pre = PackPrefix();
            if (pre.Length > 0) region = Source.Atlas.FindRegion(pre + path);
        }
        if (region == null) region = Source.Atlas.FindRegion(Leaf(path));
        if (region == null)
        {
            var want = Leaf(path);
            var pre = PackPrefix();
            foreach (var rg in Source.Atlas.Regions)
            {
                if (!string.Equals(Leaf(rg.name), want, System.StringComparison.OrdinalIgnoreCase)) continue;
                if (pre.Length == 0 || rg.name.StartsWith(pre, System.StringComparison.OrdinalIgnoreCase))
                {
                    region = rg;
                    break;
                }
                if (region == null) region = rg;
            }
        }
        if (region == null) region = AliasRegion(Leaf(path));
        if (region == null) return;
        if (att is MeshAttachment m2)
        {
            m2.RendererObject = region;
            m2.RegionU = region.u;
            m2.RegionV = region.v;
            m2.RegionU2 = region.u2;
            m2.RegionV2 = region.v2;
            m2.RegionRotate = region.rotate;
            m2.RegionDegrees = region.degrees;
            m2.RegionOffsetX = region.offsetX;
            m2.RegionOffsetY = region.offsetY;
            m2.RegionWidth = region.width;
            m2.RegionHeight = region.height;
            m2.RegionOriginalWidth = region.originalWidth;
            m2.RegionOriginalHeight = region.originalHeight;
            if (m2.RegionUVs != null) m2.UpdateUVs();
        }
        else if (att is RegionAttachment r2)
        {
            r2.RendererObject = region;
            r2.SetUVs(region.u, region.v, region.u2, region.v2, region.rotate);
        }
    }

    List<Skin> CollectSkins()
    {
        var skins = new List<Skin>();
        if (Skeleton.Skin != null) skins.Add(Skeleton.Skin);
        if (Skeleton.Data.DefaultSkin != null && !skins.Contains(Skeleton.Data.DefaultSkin))
            skins.Add(Skeleton.Data.DefaultSkin);
        for (var i = 0; i < Skeleton.Data.Skins.Count; i++)
        {
            var skin = Skeleton.Data.Skins.Items[i];
            if (skin != null && !skins.Contains(skin)) skins.Add(skin);
        }
        return skins;
    }

    Attachment FindOnSlot(List<Skin> skins, Slot slot, string stem)
    {
        var setup = slot.Data.AttachmentName;
        if (!string.IsNullOrEmpty(setup) && setup.IndexOf("hexie", System.StringComparison.OrdinalIgnoreCase) < 0)
        {
            var a = Skeleton.GetAttachment(slot.Data.Index, setup);
            if (IsPlain(a) && (stem.Length == 0 || string.Equals(Leaf(a.Name), stem, System.StringComparison.OrdinalIgnoreCase) || string.Equals(Leaf(setup), stem, System.StringComparison.OrdinalIgnoreCase)))
                return a;
        }
        if (stem.Length > 0)
        {
            var a = Skeleton.GetAttachment(slot.Data.Index, stem);
            if (IsPlain(a)) return a;
        }
        var buf = new List<Skin.SkinEntry>();
        foreach (var skin in skins)
        {
            buf.Clear();
            skin.GetAttachments(slot.Data.Index, buf);
            Attachment any = null;
            foreach (var e in buf)
            {
                if (!IsPlain(e.Attachment)) continue;
                var leaf = Leaf(e.Name);
                if (stem.Length == 0 || string.Equals(leaf, stem, System.StringComparison.OrdinalIgnoreCase))
                    return e.Attachment;
                if (any == null) any = e.Attachment;
            }
            if (stem.Length == 0 && any != null) return any;
        }
        return null;
    }

    static bool CensoredName(string nm)
    {
        if (string.IsNullOrEmpty(nm)) return false;
        if (nm.IndexOf("hexie", System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
        var leaf = Leaf(nm);
        return string.Equals(leaf, "bad_eye", System.StringComparison.OrdinalIgnoreCase);
    }

    static bool IsPlain(Attachment a)
        => a != null && !CensoredName(a.Name ?? "");

    static string Leaf(string name)
    {
        if (string.IsNullOrEmpty(name)) return "";
        var i = name.LastIndexOf('/');
        return i >= 0 ? name.Substring(i + 1) : name;
    }

    static string HexieStem(string name)
    {
        var leaf = Leaf(name);
        const string tag = "_hexie";
        if (leaf.Length > tag.Length && leaf.EndsWith(tag, System.StringComparison.OrdinalIgnoreCase))
            return leaf.Substring(0, leaf.Length - tag.Length);
        return "";
    }

    static bool IsFxSlot(string nm, string sn)
    {
        return Hit(nm, sn, "shadow") || Hit(nm, sn, "glow") || Hit(nm, sn, "highlight")
            || Hit(nm, sn, "fx") || Hit(nm, sn, "laser") || Hit(nm, sn, "line")
            || Hit(nm, sn, "handle")
            || Hit(nm, sn, "trail") || Hit(nm, sn, "smoke") || Hit(nm, sn, "scrap")
            || Hit(nm, sn, "dust") || Hit(nm, sn, "explode") || Hit(nm, sn, "water")
            || Hit(nm, sn, "saliva") || Hit(nm, sn, "vomit") || Hit(nm, sn, "tear")
            || Hit(nm, sn, "multi_hit") || Hit(nm, sn, "jump_ring") || Hit(nm, sn, "blow");
    }

    static bool IsBody(string nm, string sn)
    {
        if (IsFxSlot(nm, sn)) return false;
        if (Hit(nm, sn, "handle")) return false;
        return Hit(nm, sn, "body") || Hit(nm, sn, "head") || Hit(nm, sn, "cap")
            || Hit(nm, sn, "eye") || Hit(nm, sn, "mouth") || Hit(nm, sn, "sawtooth")
            || Hit(nm, sn, "tooth") || Hit(nm, sn, "wooden") || Hit(nm, sn, "helmet")
            || Hit(nm, sn, "horn") || Hit(nm, sn, "boss")
            || Hit(nm, sn, "hp") || Hit(nm, sn, "score")
            || Hit(nm, sn, "tenia") || Hit(nm, sn, "coat") || Hit(nm, sn, "heart")
            || Hit(nm, sn, "wing") || Hit(nm, sn, "hand")
            || Hit(nm, sn, "taiji") || Hit(nm, sn, "bagua")
            || Hit(nm, sn, "FlowerHeart") || Hit(nm, sn, "feidie")
            || Hit(nm, sn, "square") || Hit(nm, sn, "round") || Hit(nm, sn, "roung");
    }

    static bool Hit(string nm, string sn, string key)
        => nm.IndexOf(key, System.StringComparison.OrdinalIgnoreCase) >= 0
        || sn.IndexOf(key, System.StringComparison.OrdinalIgnoreCase) >= 0;

    static bool StripLike(float[] w, int count)
    {
        if (count < 6) return false;
        float minX = 1e9f, minY = 1e9f, maxX = -1e9f, maxY = -1e9f;
        for (var i = 0; i < count; i += 2)
        {
            var x = w[i];
            var y = w[i + 1];
            if (x < minX) minX = x;
            if (y < minY) minY = y;
            if (x > maxX) maxX = x;
            if (y > maxY) maxY = y;
        }
        var dx = maxX - minX;
        var dy = maxY - minY;
        var m = Mathf.Max(dx, dy);
        var n = Mathf.Max(0.001f, Mathf.Min(dx, dy));
        return m > 2.4f && m > n * 3.2f;
    }

    void Push(PageDraw p, Slot slot, float[] verts, int vLen, float[] uvs, int[] tris, int tLen, Color32 col, bool body, ref float bMinX, ref float bMinY, ref float bMaxX, ref float bMaxY, ref bool bodyAny)
    {
        if (_clipper.IsClipping)
        {
            _clipper.ClipTriangles(verts, vLen, tris, tLen, uvs);
            verts = _clipper.ClippedVertices.Items;
            vLen = _clipper.ClippedVertices.Count;
            uvs = _clipper.ClippedUVs.Items;
            tris = _clipper.ClippedTriangles.Items;
            tLen = _clipper.ClippedTriangles.Count;
        }
        if (vLen < 6 || tLen < 3) return;
        var baseV = p.V.Count;
        for (var i = 0; i < vLen; i += 2)
        {
            var x = verts[i];
            var y = verts[i + 1];
            p.V.Add(new Vector3(x, y, 0f));
            var vv = uvs[i + 1];
            if (Source == null || !Source.VFlipped) vv = 1f - vv;
            p.Uv.Add(new Vector2(uvs[i], vv));
            p.C.Add(col);
            if (!body) continue;
            if (x < bMinX) bMinX = x;
            if (y < bMinY) bMinY = y;
            if (x > bMaxX) bMaxX = x;
            if (y > bMaxY) bMaxY = y;
            bodyAny = true;
        }
        for (var i = 0; i < tLen; i++) p.T.Add(baseV + tris[i]);
    }

    /// <summary>双押彩虹条画在猫脸下面。上下两只各有一条，不拆开就会互相盖住脸。</summary>
    bool BeamUnderFace()
    {
        var face = false;
        var beam = false;
        for (var i = 0; i < Skeleton.Slots.Count; i++)
        {
            var s = Skeleton.Slots.Items[i];
            var sn = s.Data.Name ?? "";
            var an = s.Data.AttachmentName ?? "";
            if (BeamSlot(an, sn)) beam = true;
            if (Hit(an, sn, "shadow")) continue;
            if (sn == "head" || sn == "body" || an == "head" || an == "body"
                || Hit(an, sn, "taiji") || Hit(an, sn, "bagua")
                || Hit(an, sn, "FlowerHeart") || Hit(an, sn, "feidie")
                || Hit(an, sn, "petal_H")) face = true;
        }
        return face && beam;
    }

    static bool BeamSlot(string nm, string sn)
    {
        if (Hit(nm, sn, "ufo") || Hit(nm, sn, "weapon")) return false;
        if (Hit(nm, sn, "explode") || Hit(nm, sn, "circle") || Hit(nm, sn, "shine")) return false;
        if (Hit(nm, sn, "highlight")) return false;
        if (Hit(nm, sn, "laser_ball")) return false;
        return Hit(nm, sn, "laser") || Hit(nm, sn, "handle")
            || (Hit(nm, sn, "light") && !Hit(nm, sn, "spotlight"));
    }

    /// <summary>官机粉柱和柱上闪电都在莲花后面。fx 不是 BeamSlot，不单独算会盖住花心。</summary>
    static bool BeamBehind(string nm, string sn)
        => BeamSlot(nm, sn) || Hit(nm, sn, "fx");

    PageDraw Bucket(Texture2D tex, bool behind = false, bool additive = false, bool front = false)
    {
        foreach (var p in _pages)
            if (p.Tex == tex && p.Behind == behind && p.Additive == additive && p.Front == front) return p;
        var go = new GameObject((behind ? tex.name + "_back" : tex.name) + (front ? "_front" : "") + (additive ? "_add" : ""));
        go.transform.SetParent(transform, false);
        var d = new PageDraw
        {
            Tex = tex,
            Behind = behind,
            Additive = additive,
            Front = front,
            Mesh = new Mesh { name = go.name },
            Filter = go.AddComponent<MeshFilter>(),
            Rend = go.AddComponent<MeshRenderer>()
        };
        d.Mesh.MarkDynamic();
        d.Filter.sharedMesh = d.Mesh;
        go.transform.localPosition = new Vector3(0f, 0f, behind ? 0.04f : (front ? -0.03f : (additive ? -0.02f : 0f)));
        Material src = null;
        if (Source != null) Source.Mats.TryGetValue(tex, out src);
        if (src == null) src = MdSpine.NewMat(tex);
        d.Rend.sharedMaterial = new Material(src);
        MdMat.SetTex(d.Rend.sharedMaterial, tex);
        d.Rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        d.Rend.receiveShadows = false;
        ApplyPageLayer(d);
        _pages.Add(d);
        return d;
    }

    void ApplyPageLayer(PageDraw p)
    {
        if (p?.Rend == null) return;
        p.Rend.sortingLayerName = _sortLayer;
        p.Rend.sortingOrder = SortOrder + (p.Behind ? -2 : 0) + (p.Front ? 4 : 0) + (p.Additive ? 1 : 0);
        var m = p.Rend.sharedMaterial;
        if (m == null) return;
        m.renderQueue = SortQueue + (p.Additive ? 1 : 0);
        if (m.HasProperty("_SrcBlend")) m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
        if (m.HasProperty("_DstBlend"))
            m.SetInt("_DstBlend", p.Additive
                ? (int)UnityEngine.Rendering.BlendMode.One
                : (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        if (m.HasProperty("_PmaTex"))
            m.SetFloat("_PmaTex", Source != null && Source.VFlipped ? 1f : 0f);
    }

    void Upload(PageDraw p)
    {
        p.Mesh.Clear();
        if (p.V.Count == 0)
        {
            p.Rend.enabled = false;
            return;
        }
        p.Rend.enabled = true;
        if (p.Rend.sharedMaterial == null && p.Tex != null)
            p.Rend.sharedMaterial = MdSpine.NewMat(p.Tex);
        if (p.Rend.sharedMaterial != null)
            MdMat.SetTex(p.Rend.sharedMaterial, p.Tex);
        ApplyPageLayer(p);
        p.Mesh.SetVertices(p.V);
        p.Mesh.SetUVs(0, p.Uv);
        p.Mesh.SetColors(p.C);
        p.Mesh.SetTriangles(p.T, 0);
        p.Mesh.RecalculateBounds();
    }

    static Texture2D TexOf(object renderer)
    {
        if (renderer is AtlasRegion ar && ar.page != null) return ar.page.rendererObject as Texture2D;
        return renderer as Texture2D;
    }

    Color32 Tint(Slot slot, float r, float g, float b, float a)
    {
        var sr = slot.R * r;
        var sg = slot.G * g;
        var sb = slot.B * b;
        var sa = slot.A * a * Skeleton.A;
        return new Color32(
            (byte)(sr * sa * 255f),
            (byte)(sg * sa * 255f),
            (byte)(sb * sa * 255f),
            (byte)(sa * 255f));
    }

    void OnDestroy()
    {
        foreach (var p in _pages)
            if (p.Mesh != null) Destroy(p.Mesh);
    }
}
