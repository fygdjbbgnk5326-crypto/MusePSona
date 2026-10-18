namespace MusePSona.Play;

/// <summary>ibms → 场景内 Spine 条目号 + 入场组。与 render.js ANIM_SPEC 一致。</summary>
public static class AnimBinding
{
    public readonly struct Spec
    {
        public Spec(string code, string group) { Code = code; Group = group; }
        public string Code { get; }
        public string Group { get; }
    }

    public static Spec? Of(PlayNote n)
    {
        if (n.Category is NoteCategory.Hold) return null;
        var collect = CollectPack(n);
        if (collect != null) return null;
        if (n.Special == NoteSpecial.Pendulum) return new Spec("15", "num");
        if (n.Special == NoteSpecial.Raider) return new Spec("16", "up");
        return n.BmsId switch
        {
            "01" => new Spec("10", "nor"),
            "02" => new Spec("10", "up"),
            "03" => new Spec("10", "down"),
            "04" => new Spec("11", "nor"),
            "05" => new Spec("11", "up"),
            "06" => new Spec("11", "down"),
            "07" => new Spec("12", "nor"),
            "08" => new Spec("12", "up"),
            "09" => new Spec("12", "down"),
            "0A" => new Spec("13", "nor"),
            "0B" => new Spec("14", "nor"),
            "0E" => new Spec("05", "nor"),
            "0G" => new Spec("04", "mash"),
            "0H" => new Spec("03", "nor"),
            "18" => new Spec("09", "boss_1"),
            "13" => new Spec("06", "boss"),
            "14" => new Spec("07", "boss2"),
            "15" => new Spec("08", "boss2"),
            "21" => new Spec("17", "nor"),
            "40" => new Spec("10", "nor"),
            "41" => new Spec("11", "nor"),
            "42" => new Spec("12", "nor"),
            _ => null
        };
    }

    /// <summary>Skeleton 资源名：scene_05 + 04 → 0504。air/road 由 Pathway 决定。</summary>
    public static string SkeletonKey(string scene, Spec spec, PlayNote n)
    {
        var prefix = SceneTimeline.Norm(scene);
        var num = prefix.Length >= 2 ? prefix[^2..] : "01";
        // 1K / scene_11 只有 road 包（1110/1111/1112_road），没有 air。
        var lane = SceneTimeline.IsOneKey(scene) || PlayNote.IsOneKeyId(n.BmsId)
            ? "road"
            : (n.Pathway == 1 ^ n.Inverted ? "air" : "road");
        return num + spec.Code + "_" + lane;
    }

    public static string InClip(Spec spec, int rate)
    {
        var n = rate <= 1 ? "44" : rate == 2 ? "38" : "33";
        if (spec.Group == "num") n = rate <= 1 ? "26" : rate == 2 ? "21" : "18";
        if (spec.Group == "nor") return "in_nor_" + n;
        // 02/05/08 升降上升：in_up_30/26/23。突击者 0C/0I 也走 Group=up，但是 39/30/24。
        if (spec.Group == "up")
        {
            if (spec.Code == "16") n = rate <= 1 ? "39" : rate == 2 ? "30" : "24";
            else n = rate <= 1 ? "30" : rate == 2 ? "26" : "23";
            return "in_up_" + n;
        }
        // 03/06/09 下落：in_down_30/26/23。连打 0G 下落款 in_down_27/23/20。
        if (spec.Group == "down") return "in_down_" + (rate <= 1 ? "30" : rate == 2 ? "26" : "23");
        if (spec.Group == "mash") return "in_down_" + (rate <= 1 ? "27" : rate == 2 ? "23" : "20");
        if (spec.Group == "boss_1") return "boss_1_nor_" + (rate <= 1 ? "21" : rate == 2 ? "18" : "15");
        if (spec.Group == "boss") return "in_boss_1_" + (rate <= 1 ? "21" : rate == 2 ? "18" : "15");
        if (spec.Group == "boss2")
        {
            var castle = SceneTimeline.Norm(Flight.Scene) == "scene_05";
            n = castle
                ? (rate <= 1 ? "31" : rate == 2 ? "28" : "24")
                : (rate <= 1 ? "23" : rate == 2 ? "20" : "17");
            return "in_boss_2_" + n;
        }
        return "in_" + n;
    }

    /// <summary>官机 common 包：红心 0002_hp、音符 0003_score。不要编成 01xx 场景怪号。</summary>
    public static string CollectPack(PlayNote n)
    {
        if (n.Category == NoteCategory.Heart) return "0002_hp";
        if (n.Category == NoteCategory.Music) return "0003_score";
        return null!;
    }

    public static string CollectInClip(PlayNote n)
        => n.Pathway == 1 ? "in_air" : "in_road";
}
