namespace MusePSona.Play;

/// <summary>可玩键分类。连击规则跟网页 combo.js：gear / note / heart 不计；24 星与长按头相同。</summary>
public enum NoteCategory
{
    Monster,
    Boss,
    Ghost,
    Hold,
    Masher,
    Gear,
    Music,
    Heart
}

public enum NoteSpecial
{
    None,
    Pendulum,
    Raider
}

public sealed class PlayNote
{
    public int Id { get; init; }
    public decimal TimeSec { get; init; }
    public decimal LengthSec { get; init; }
    public int Pathway { get; init; }
    public string BmsId { get; init; } = "01";
    public int ApproachRate { get; init; } = 1;
    public NoteCategory Category { get; init; }
    public NoteSpecial Special { get; init; }
    public bool Inverted { get; init; }
    public bool CountsCombo { get; init; }
    /// <summary>齿轮擦身而过，不接判定。</summary>
    public bool NeedsHit { get; init; }

    public decimal EndSec => TimeSec + LengthSec;

    /// <summary>
    /// 官机可直接由角色接触收集的物件。24 虽共用 Music 外观分类，实际是
    /// Hold 星，必须由另一枚同轨按键独立打击，不能随正在按住的长条自动结算。
    /// </summary>
    public bool IsPassiveCollectible => Category == NoteCategory.Heart
        || (Category == NoteCategory.Music && BmsId != MuseDashChart.BmsTables.IdHoldStar);

    /// <summary>
    /// 长按头尾之间每拍补一颗可打的 23（不计连击）。官机 D/J 按条、F/K 打星。
    /// 谱面已有同刻 23 则不重复。
    /// </summary>
    public static List<PlayNote> WithHoldBeatStars(IReadOnlyList<PlayNote> notes, float bpm)
    {
        var list = notes.ToList();
        var holds = list.Where(n => n.Category == NoteCategory.Hold).ToList();
        if (holds.Count == 0) return list;
        var taken = new HashSet<(int slot, int path)>();
        foreach (var n in list)
        {
            if (n.Category != NoteCategory.Music) continue;
            taken.Add((MuseDashChart.BmsTime.SlotIndex((float)n.TimeSec, bpm), n.Pathway));
        }
        var id = list.Count == 0 ? 1 : list.Max(n => n.Id) + 1;
        foreach (var h in holds)
        {
            foreach (var t in Flight.HoldStarTimes((float)h.TimeSec, (float)h.EndSec, bpm))
            {
                var key = (MuseDashChart.BmsTime.SlotIndex(t, bpm), h.Pathway);
                if (!taken.Add(key)) continue;
                list.Add(new PlayNote
                {
                    Id = id++,
                    TimeSec = (decimal)t,
                    Pathway = h.Pathway,
                    BmsId = "23",
                    ApproachRate = h.ApproachRate,
                    Category = NoteCategory.Music,
                    NeedsHit = true,
                    CountsCombo = false
                });
            }
        }
        list.Sort((a, b) => a.TimeSec.CompareTo(b.TimeSec));
        return list;
    }

    public static PlayNote From(MuseDashChart.Placement n)
    {
        var id = (n.BmsId ?? "").ToUpperInvariant();
        var spec = SpecialOf(id);
        var cat = CategoryOf(id, n.LengthSec);
        var needsHit = cat is not NoteCategory.Gear;
        var combo = cat is NoteCategory.Monster or NoteCategory.Boss or NoteCategory.Ghost
            or NoteCategory.Hold or NoteCategory.Masher
            || id == MuseDashChart.BmsTables.IdHoldStar;
        var pathway = IsOneKeyId(id) ? 0 : n.Pathway;
        return new PlayNote
        {
            Id = n.Id,
            TimeSec = n.TimeSec,
            LengthSec = n.LengthSec,
            Pathway = pathway,
            BmsId = id,
            ApproachRate = n.ApproachRate <= 1 ? 1 : n.ApproachRate == 2 ? 2 : 3,
            Category = cat,
            Special = spec.special,
            Inverted = spec.inverted,
            CountsCombo = combo,
            NeedsHit = needsHit
        };
    }

    /// <summary>scene_11 愚人彩蛋：地面 1/2/3，对应 F8 <c>1110/1111/1112_road</c>。</summary>
    public static bool IsOneKeyId(string? id) => id is "40" or "41" or "42";

    public static NoteCategory CategoryOf(string id, decimal lengthSec)
    {
        if (MuseDashChart.BmsTables.IsGear(id)) return NoteCategory.Gear;
        if (id is "0G" or "16" or "17") return NoteCategory.Masher;
        if (MuseDashChart.BmsTables.IsHoldKey(id) && lengthSec > MuseDashChart.BmsTables.TapHoldLength)
            return NoteCategory.Hold;
        if (id is "10" or "23" or MuseDashChart.BmsTables.IdHoldStar) return NoteCategory.Music;
        if (id == "22") return NoteCategory.Heart;
        if (id == "21") return NoteCategory.Ghost;
        if (id is "0I" or "0J") return NoteCategory.Boss;
        if (id.Length == 2 && id[0] == '3') return NoteCategory.Boss;
        if (id.Length == 2 && id[0] == '1' && id[1] is >= '1' and <= '8') return NoteCategory.Boss;
        return NoteCategory.Monster;
    }

    static (NoteSpecial special, bool inverted) SpecialOf(string id) => id switch
    {
        "0D" => (NoteSpecial.Pendulum, false),
        "0J" => (NoteSpecial.Pendulum, true),
        "0C" => (NoteSpecial.Raider, false),
        "0I" => (NoteSpecial.Raider, true),
        _ => (NoteSpecial.None, false)
    };
}
