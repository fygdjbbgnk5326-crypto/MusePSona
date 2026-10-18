using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using MusePSona.Play;
using UnityEngine;

/// <summary>StreamingAssets/MdLooks：按 MDM 当前场景切静图包。缺包回退方块。素材仅本机。</summary>
public static class MdLooks
{
    public sealed class Pack
    {
        public string Scene;
        public readonly Dictionary<string, Texture2D> Sprites = new();
    }

    static readonly Dictionary<string, Pack> Loaded = new();
    static readonly Regex FileOf = new(@"\""([^\""]+)\""\s*:\s*\{\s*\""file\""\s*:\s*\""([^\""]+)\""", RegexOptions.Compiled);
    static bool _ready;

    public static void Reload()
    {
        foreach (var p in Loaded.Values)
        {
            foreach (var t in p.Sprites.Values)
                if (t != null) Object.Destroy(t);
        }
        Loaded.Clear();
        _ready = false;
        Ensure();
    }

    public static void Ensure()
    {
        if (_ready) return;
        _ready = true;
        var sprites = Path.Combine(Application.streamingAssetsPath, "MdLooks", "sprites");
        if (!Directory.Exists(sprites)) return;
        foreach (var dir in Directory.GetDirectories(sprites))
        {
            var scene = Path.GetFileName(dir);
            var man = Path.Combine(dir, "manifest.json");
            if (!File.Exists(man)) continue;
            try
            {
                var pack = new Pack { Scene = scene };
                foreach (Match m in FileOf.Matches(File.ReadAllText(man)))
                {
                    var key = m.Groups[1].Value;
                    var png = Path.Combine(dir, m.Groups[2].Value);
                    if (!File.Exists(png)) png = Path.Combine(dir, key + ".png");
                    if (!File.Exists(png)) continue;
                    var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    if (!tex.LoadImage(File.ReadAllBytes(png))) continue;
                    tex.filterMode = FilterMode.Bilinear;
                    tex.wrapMode = TextureWrapMode.Clamp;
                    tex.name = scene + "/" + key;
                    pack.Sprites[key] = tex;
                }
                if (pack.Sprites.Count > 0) Loaded[scene] = pack;
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("MdLooks 读 " + scene + " 失败: " + ex.Message);
            }
        }
        Debug.Log("MdLooks 静图包 " + Loaded.Count + "：" + string.Join(", ", Loaded.Keys));
    }

    public static Pack For(string scene)
    {
        Ensure();
        scene = SceneTimeline.Norm(scene);
        return scene.Length > 0 && Loaded.TryGetValue(scene, out var p) ? p : null;
    }

    /// <summary>先本场景，再任意已加载包。HitPoint 在 common，抽进各 scene 静图包。</summary>
    public static bool TrySprite(string scene, string key, out Texture2D tex)
    {
        tex = null;
        if (string.IsNullOrEmpty(key)) return false;
        Ensure();
        var pack = For(scene);
        if (pack != null && pack.Sprites.TryGetValue(key, out tex) && tex != null) return true;
        foreach (var p in Loaded.Values)
        {
            if (p != null && p.Sprites.TryGetValue(key, out tex) && tex != null) return true;
        }
        // 官机素材增量导出时，PNG 可能已生成但旧 manifest 尚未登记（例如
        // HitsBase）。允许按当前场景 / common 文件名懒加载，避免整套 HUD
        // 因单个清单项缺失而直接不创建。
        if (TryLooseSprite(scene, key, out tex)) return true;
        tex = null;
        return false;
    }

    static bool TryLooseSprite(string scene, string key, out Texture2D tex)
    {
        tex = null;
        if (key.IndexOfAny(new[] { '/', '\\' }) >= 0) return false;
        var root = Path.Combine(Application.streamingAssetsPath, "MdLooks", "sprites");
        var norm = SceneTimeline.Norm(scene);
        var dirs = norm.Length > 0 && norm != "common"
            ? new[] { norm, "common" }
            : new[] { "common" };
        foreach (var dir in dirs)
        {
            var png = Path.Combine(root, dir, key + ".png");
            if (!File.Exists(png)) continue;
            try
            {
                var loaded = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!loaded.LoadImage(File.ReadAllBytes(png)))
                {
                    Object.Destroy(loaded);
                    continue;
                }
                loaded.filterMode = FilterMode.Bilinear;
                loaded.wrapMode = TextureWrapMode.Clamp;
                loaded.name = dir + "/" + key;
                if (!Loaded.TryGetValue(dir, out var pack))
                {
                    pack = new Pack { Scene = dir };
                    Loaded[dir] = pack;
                }
                pack.Sprites[key] = loaded;
                tex = loaded;
                return true;
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("MdLooks 散图 " + key + " 读取失败: " + ex.Message);
            }
        }
        return false;
    }

    public static string SpriteKey(PlayNote n)
    {
        var lane = n.Pathway == 1 ? "air" : "ground";
        switch (n.Category)
        {
            case NoteCategory.Gear: return lane + "_gear";
            case NoteCategory.Masher: return "masher";
            case NoteCategory.Heart: return "heart";
            case NoteCategory.Music: return "note";
            case NoteCategory.Ghost: return lane + "_ghost";
            case NoteCategory.Hold: return lane + "_hold_head";
        }
        if (n.Special == NoteSpecial.Pendulum) return lane + "_pendulum";
        if (n.Special == NoteSpecial.Raider) return lane + "_raider";
        // 近战 11/12 是打击物，不要用 Boss 本体静图当飞近。弹幕走 Spine。
        if (n.Category == NoteCategory.Boss && n.BmsId is not "11" and not "12")
            return lane + "_boss";
        return n.BmsId switch
        {
            "01" or "02" or "03" => lane + "_small",
            "04" or "05" or "06" => lane + "_medium1",
            "07" or "08" or "09" => lane + "_medium2",
            "0A" => lane + "_large1",
            "0B" => lane + "_large2",
            _ => lane + "_small"
        };
    }

    public const float LargeBox = 1.28f;
    public const float SmallBox = 0.72f;
    /// <summary>官机 0102_*_top 为 154×149、PPU 100，格子 1.54。再放大，星星下沿会埋进地板。</summary>
    public const float HoldEndBox = 1.54f;
    public const float HoldBarBox = 0.96f;
    /// <summary>音符与 body 同为 880×118，按条子高度平铺；整块上下漂约 10px。</summary>
    public const float HoldNoteBob = 0.10f;
    /// <summary>官谱同长度条子上音符略疏；相对 body 等比格距再放宽。</summary>
    public const float HoldNoteSpanMul = 1.2f;
    /// <summary>官机 HitPointOut 65×63 / HitPointIn 22×22，PPU100。内圈完整环，外圈三段弧逆时针转。</summary>
    public const float JudgeOutBox = 0.65f;
    public const float JudgeInBox = 0.22f;
    /// <summary>scene_01 SpriteRenderer 顶点色（空中/地面圈共用同一张 Sprite）。</summary>
    public static readonly Color HitPointAirOut = new Color(1f, 1f, 1f, 0.588f);
    public static readonly Color HitPointRoadOut = new Color(1f, 0.787f, 0.994f, 0.903f);
    public static readonly Color HitPointAirIn = new Color(0.220f, 1f, 0.968f, 0.409f);
    public static readonly Color HitPointRoadIn = new Color(0.862f, 0f, 1f, 1f);
    /// <summary>打击字 ScorePerfect 275×59，PPU100。</summary>
    public const float HitGradePpu = 100f;

    public static float SpriteBox(PlayNote n)
    {
        if (n.BmsId == "0E") return 1.05f;
        if (n.Category == NoteCategory.Boss) return 1.15f;
        if (n.Category == NoteCategory.Gear) return 0.88f;
        if (n.Special == NoteSpecial.Pendulum) return LargeBox;
        if (n.Special != NoteSpecial.None) return 1.05f;
        if (n.Category == NoteCategory.Hold) return LargeBox;
        if (n.Category is NoteCategory.Heart or NoteCategory.Music) return 0.58f;
        return n.BmsId switch
        {
            "01" or "02" or "03" => SmallBox,
            "0A" or "0B" => LargeBox,
            _ => 0.88f
        };
    }
}
