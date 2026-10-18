namespace MusePSona.Play;

/// <summary>通道 15 的 1O–1W。与 bms.js SCENE_CHANGE 一致。</summary>
public static class SceneTimeline
{
    static readonly Dictionary<string, string> Map = new(StringComparer.OrdinalIgnoreCase)
    {
        ["1O"] = "scene_01", ["1P"] = "scene_02", ["1Q"] = "scene_05",
        ["1R"] = "scene_03", ["1S"] = "scene_04", ["1T"] = "scene_08",
        ["1U"] = "scene_07", ["1V"] = "scene_09", ["1W"] = "scene_10",
        ["1X"] = "scene_10", ["1Y"] = "scene_12"
    };

    public readonly struct Step
    {
        public Step(float t, string? scene, string id, bool unknown)
        {
            T = t; Scene = scene; Id = id; Unknown = unknown;
        }
        public float T { get; }
        public string? Scene { get; }
        public string Id { get; }
        public bool Unknown { get; }
    }

    public static string Norm(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return "";
        var m = System.Text.RegularExpressions.Regex.Match(s, @"(?:scene[_-]?)?(\d{1,2})",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        return m.Success ? "scene_" + int.Parse(m.Groups[1].Value).ToString("00") : "";
    }

    public static List<Step> Build(IEnumerable<MuseDashChart.Placement> notes, string? defaultScene)
    {
        var def = Norm(defaultScene);
        if (def.Length == 0) def = "scene_01";
        var steps = new List<Step> { new(float.NegativeInfinity, def, "", false) };
        foreach (var n in notes.Where(n => IsChange(n.BmsId)).OrderBy(n => n.TimeSec).ThenBy(n => n.Id))
        {
            if (Map.TryGetValue(n.BmsId, out var scene))
                steps.Add(new((float)n.TimeSec, scene, n.BmsId, false));
            else
                steps.Add(new((float)n.TimeSec, null, n.BmsId, true));
        }
        return steps;
    }

    public static string At(IReadOnlyList<Step> steps, float t)
    {
        var scene = steps.Count > 0 ? steps[0].Scene ?? "scene_01" : "scene_01";
        foreach (var s in steps)
        {
            if (s.T > t) break;
            if (!s.Unknown && s.Scene != null) scene = s.Scene;
        }
        return scene;
    }

    /// <summary>scene_11 = 1K 单轨。1O–1Y 切 01–10/12，判定跟当前场景。</summary>
    public static bool IsOneKey(string? scene) => Norm(scene) == "scene_11";

    /// <summary>当前场景自己的判定：切景立刻换，不看 40–42。</summary>
    public readonly struct JudgeRules
    {
        public JudgeRules(string? scene)
        {
            OneKey = IsOneKey(scene);
        }
        public bool OneKey { get; }
        public bool DualRings => !OneKey;
        public bool HideAirLane => OneKey;
    }

    public static JudgeRules RulesFor(string? scene) => new(scene);

    public static bool IsChange(string? id)
        => id != null && (Map.ContainsKey(id) || id is "1X" or "1Y");

    /// <summary>谱面用到的场景包（默认 + 1O–1W），按出现顺序去重。</summary>
    public static List<string> Needed(IReadOnlyList<Step> steps)
    {
        var list = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var s in steps)
        {
            if (s.Unknown || string.IsNullOrEmpty(s.Scene)) continue;
            if (seen.Add(s.Scene)) list.Add(s.Scene);
        }
        return list;
    }
}
