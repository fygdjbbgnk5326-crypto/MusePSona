namespace MuseDashChart;

public readonly record struct PaletteItem(string Id, string Label, string Kind, string Group);

public static class BmsLabels
{
    public static readonly PaletteItem[] LanePalette =
    [
        new(BmsTables.IdSmall, "单击 小", "note", "lane"),
        new("02", "单击 上", "note", "lane"),
        new("03", "单击 下", "note", "lane"),
        new("04", "中型1", "note", "lane"),
        new("05", "中型1 上", "note", "lane"),
        new("06", "中型1 下", "note", "lane"),
        new("07", "中型2", "note", "lane"),
        new("08", "中型2 上", "note", "lane"),
        new("09", "中型2 下", "note", "lane"),
        new("0A", "大型1", "note", "lane"),
        new("0B", "大型2", "note", "lane"),
        new("0C", "突击者", "note", "lane"),
        new("0D", "摆锤", "note", "lane"),
        new("0E", "双押", "note", "lane"),
        new(BmsTables.IdHold, "长按", "note", "lane"),
        new(BmsTables.IdMasher, "连打", "note", "lane"),
        new(BmsTables.IdGear, "齿轮", "note", "lane"),
        new(BmsTables.IdNote, "音符条", "note", "lane"),
        new(BmsTables.IdMusic, "音符", "note", "lane"),
        new(BmsTables.IdHeart, "红心", "note", "lane"),
        new(BmsTables.IdGhost, "幽灵", "note", "lane"),
        new("20", "P道具", "note", "lane"),
        new("30", "中弹", "note", "lane"),
        new("31", "中弹 上", "note", "lane"),
        new("32", "中弹 下", "note", "lane"),
        new("34", "小弹", "note", "lane"),
        new("35", "小弹 上", "note", "lane"),
        new("36", "小弹 下", "note", "lane"),
        new("38", "大弹", "note", "lane"),
        new("39", "大弹 上", "note", "lane"),
        new("3A", "大弹 下", "note", "lane")
    ];

    public static readonly PaletteItem[] BossPalette =
    [
        new("11", "Boss 近战1", "note", "boss"),
        new("12", "Boss 近战2", "note", "boss"),
        new("13", "Boss 弹幕1", "note", "boss"),
        new("14", "Boss 弹幕2", "note", "boss"),
        new("15", "Boss 弹幕3", "note", "boss"),
        new("16", "Boss 连打1", "note", "boss"),
        new("17", "Boss 连打2", "note", "boss"),
        new("18", "Boss 齿轮", "note", "boss"),
        new("3C", "Boss弹1", "note", "boss"),
        new("3E", "Boss弹2", "note", "boss"),
        new("1A", "Boss 入场", "event", "boss"),
        new("1B", "Boss 退场", "event", "boss"),
        new("1C", "Boss 准备1", "event", "boss"),
        new("1D", "Boss 结束1", "event", "boss"),
        new("1E", "Boss 准备2", "event", "boss"),
        new("1F", "Boss 结束2", "event", "boss"),
        new("1G", "换阶段 1→2", "event", "boss"),
        new("1H", "换阶段 2→1", "event", "boss"),
        new("1L", "隐藏 Boss", "event", "boss"),
        new("1M", "显示 Boss", "event", "boss")
    ];

    public static readonly PaletteItem[] EventPalette =
    [
        new("0O", "流速1 双", "event", "event"),
        new("0P", "流速2 双", "event", "event"),
        new("0Q", "流速3 双", "event", "event"),
        new("0R", "流速1 下", "event", "event"),
        new("0S", "流速2 下", "event", "event"),
        new("0T", "流速3 下", "event", "event"),
        new("0U", "流速1 上", "event", "event"),
        new("0V", "流速2 上", "event", "event"),
        new("0W", "流速3 上", "event", "event"),
        new("0X", "变速 速度1", "event", "event"),
        new("0Y", "变速 速度2", "event", "event"),
        new("0Z", "变速 速度3", "event", "event"),
        new("2E", "暗角 On", "event", "event"),
        new("2F", "暗角 Off", "event", "event"),
        new("1J", "隐藏音符", "event", "event"),
        new("1K", "显示音符", "event", "event"),
        new("1O", "切景 空间站", "event", "event"),
        new("1P", "切景 复古城", "event", "event"),
        new("1Q", "切景 城堡", "event", "event"),
        new("1R", "切景 雨夜", "event", "event"),
        new("1S", "切景 糖果", "event", "event"),
        new("1T", "切景 东方", "event", "event"),
        new("1U", "切景 GC", "event", "event"),
        new("1V", "切景 车万", "event", "event"),
        new("1W", "切景 DJMAX", "event", "event"),
        new("1X", "切景 舞台10", "event", "event"),
        new("1Y", "切景 场景12", "event", "event"),
        new("25", "隐藏背景", "event", "event"),
        new("26", "显示背景", "event", "event"),
        new("27", "画面上滚", "event", "event"),
        new("28", "画面下滚", "event", "event"),
        new("29", "画面滚动关", "event", "event"),
        new("2A", "判定线波纹 开", "event", "event"),
        new("2B", "判定线波纹 关", "event", "event"),
        new("2C", "色散 开", "event", "event"),
        new("2D", "色散 关", "event", "event"),
        new("2G", "TV噪点 开", "event", "event"),
        new("2H", "TV噪点 关", "event", "event"),
        new("2I", "闪光 始", "event", "event"),
        new("2J", "闪光 中", "event", "event"),
        new("2K", "闪光 末", "event", "event"),
        new("2N", "背景静止 开", "event", "event"),
        new("2O", "背景静止 关", "event", "event"),
        new("2P", "马赛克 开", "event", "event"),
        new("2Q", "马赛克 关", "event", "event"),
        new("2R", "棕褐 开", "event", "event"),
        new("2S", "棕褐 关", "event", "event"),
        new("2T", "集中线 黑", "event", "event"),
        new("2U", "集中线 白", "event", "event"),
        new("2V", "集中线 关", "event", "event"),
        new("2W", "怀旧滤镜 开", "event", "event"),
        new("2X", "怀旧滤镜 关", "event", "event"),
        new("2Y", "Auto 开", "event", "event"),
        new("2Z", "Auto 关", "event", "event")
    ];

    public static PaletteItem[] Palette => [..LanePalette, ..BossPalette, ..EventPalette];

    public static readonly Dictionary<string, string> Names = new(StringComparer.OrdinalIgnoreCase)
    {
        ["01"] = "单击 小",
        ["02"] = "单击 上",
        ["03"] = "单击 下",
        ["04"] = "中型1",
        ["05"] = "中型1 上",
        ["06"] = "中型1 下",
        ["07"] = "中型2",
        ["08"] = "中型2 上",
        ["09"] = "中型2 下",
        ["0A"] = "大型1",
        ["0B"] = "大型2",
        ["0C"] = "突击者",
        ["0D"] = "摆锤",
        ["0E"] = "双押",
        ["0F"] = "长按",
        ["0G"] = "连打",
        ["0H"] = "齿轮",
        ["0I"] = "突击者 倒",
        ["0J"] = "摆锤 倒",
        ["0O"] = "流速1 双",
        ["0P"] = "流速2 双",
        ["0Q"] = "流速3 双",
        ["0R"] = "流速1 下",
        ["0S"] = "流速2 下",
        ["0T"] = "流速3 下",
        ["0U"] = "流速1 上",
        ["0V"] = "流速2 上",
        ["0W"] = "流速3 上",
        ["0X"] = "变速 速度1",
        ["0Y"] = "变速 速度2",
        ["0Z"] = "变速 速度3",
        ["10"] = "音符",
        ["11"] = "Boss 近战1",
        ["12"] = "Boss 近战2",
        ["13"] = "Boss 弹幕1",
        ["14"] = "Boss 弹幕2",
        ["15"] = "Boss 弹幕3",
        ["16"] = "Boss 连打1",
        ["17"] = "Boss 连打2",
        ["18"] = "Boss 齿轮",
        ["1A"] = "Boss 入场",
        ["1B"] = "Boss 退场",
        ["1C"] = "Boss 准备1",
        ["1D"] = "Boss 结束1",
        ["1E"] = "Boss 准备2",
        ["1F"] = "Boss 结束2",
        ["1G"] = "换阶段 1→2",
        ["1H"] = "换阶段 2→1",
        ["1J"] = "隐藏音符",
        ["1K"] = "显示音符",
        ["1L"] = "隐藏 Boss",
        ["1M"] = "显示 Boss",
        ["1O"] = "切场景 空间站",
        ["1P"] = "切场景 复古城",
        ["1Q"] = "切场景 城堡",
        ["1R"] = "切场景 雨夜",
        ["1S"] = "切场景 糖果",
        ["1T"] = "切场景 东方",
        ["1U"] = "切场景 GC",
        ["1V"] = "切场景 车万",
        ["1W"] = "切场景 DJMAX",
        ["1X"] = "切场景 舞台10",
        ["1Y"] = "切场景 场景12",
        ["20"] = "P道具",
        ["21"] = "幽灵",
        ["22"] = "红心",
        ["23"] = "音符条",
        ["25"] = "隐藏背景",
        ["26"] = "显示背景",
        ["27"] = "画面上滚",
        ["28"] = "画面下滚",
        ["29"] = "画面滚动关",
        ["2A"] = "判定线波纹 开",
        ["2B"] = "判定线波纹 关",
        ["2C"] = "色散 开",
        ["2D"] = "色散 关",
        ["2E"] = "暗角 开",
        ["2F"] = "暗角 关",
        ["2G"] = "TV噪点 开",
        ["2H"] = "TV噪点 关",
        ["2I"] = "闪光 始",
        ["2J"] = "闪光 中",
        ["2K"] = "闪光 末",
        ["2N"] = "背景静止 开",
        ["2O"] = "背景静止 关",
        ["2P"] = "马赛克 开",
        ["2Q"] = "马赛克 关",
        ["2R"] = "棕褐 开",
        ["2S"] = "棕褐 关",
        ["2T"] = "集中线 黑",
        ["2U"] = "集中线 白",
        ["2V"] = "集中线 关",
        ["2W"] = "怀旧滤镜 开",
        ["2X"] = "怀旧滤镜 关",
        ["2Y"] = "Auto 开",
        ["2Z"] = "Auto 关",
        ["30"] = "中弹",
        ["31"] = "中弹 上",
        ["32"] = "中弹 下",
        ["33"] = "中弹 换轨",
        ["34"] = "小弹",
        ["35"] = "小弹 上",
        ["36"] = "小弹 下",
        ["37"] = "小弹 换轨",
        ["38"] = "大弹",
        ["39"] = "大弹 上",
        ["3A"] = "大弹 下",
        ["3B"] = "大弹 换轨",
        ["3C"] = "Boss弹1",
        ["3D"] = "Boss弹1 换轨",
        ["3E"] = "Boss弹2",
        ["3F"] = "Boss弹2 换轨"
    };

    public static string Title(string bmsId)
        => Names.TryGetValue(bmsId ?? "", out var name) ? name : bmsId ?? "";

    public static string EventGroup(string bmsId)
    {
        var id = (bmsId ?? "").ToUpperInvariant();
        if (ScrollSpeed.IsScrollId(id)) return "速度事件";
        if (ApproachSpeed.IsApproachId(id)) return "变速事件";
        if (Vignette.IsVignetteId(id)) return "暗角";
        if (id.StartsWith('2')) return "画面效果";
        if (id.StartsWith('3')) return "弹幕";
        if (id.StartsWith('1') && id.Length >= 2)
        {
            var c = id[1];
            if (c is >= '1' and <= '8' or >= 'A' and <= 'H' or 'L' or 'M')
                return "Boss";
            return "场景";
        }

        return "事件";
    }

    public static ColorRgba ScrollTierColor(int tier) => tier switch
    {
        1 => new ColorRgba(90, 220, 210),
        2 => new ColorRgba(255, 190, 50),
        3 => new ColorRgba(255, 95, 85),
        _ => new ColorRgba(200, 80, 220)
    };

    public static ColorRgba ApproachTierColor(int rate) => rate switch
    {
        2 => new ColorRgba(255, 205, 80),
        3 => new ColorRgba(255, 100, 110),
        _ => new ColorRgba(80, 170, 240)
    };

    public static ColorRgba Color(string bmsId, string kind)
    {
        var scroll = ScrollSpeed.TierOf(bmsId);
        if (scroll > 0) return ScrollTierColor(scroll);
        var id = bmsId.ToUpperInvariant();
        if (ApproachSpeed.IsApproachId(id)) return ApproachTierColor(ApproachSpeed.RateOfBmsId(id));
        if (id == "2E") return new ColorRgba(48, 20, 56);
        if (id == "2F") return new ColorRgba(210, 200, 220);
        if (kind == "event") return new ColorRgba(168, 96, 220);
        return bmsId.ToUpperInvariant() switch
        {
            BmsTables.IdGear or "18" => new ColorRgba(214, 148, 12),
            BmsTables.IdHold => new ColorRgba(140, 90, 230),
            BmsTables.IdMasher => new ColorRgba(220, 60, 80),
            BmsTables.IdNote => new ColorRgba(240, 210, 70),
            BmsTables.IdMusic => new ColorRgba(80, 200, 120),
            BmsTables.IdHeart => new ColorRgba(240, 90, 140),
            BmsTables.IdGhost => new ColorRgba(160, 160, 180),
            "0A" or "0B" => new ColorRgba(90, 180, 255),
            "11" or "12" or "13" or "14" or "15" or "16" or "17"
                => new ColorRgba(255, 110, 70),
            "3C" or "3D" or "3E" or "3F" => new ColorRgba(255, 170, 60),
            _ => new ColorRgba(70, 210, 180)
        };
    }
}

public readonly record struct ColorRgba(byte R, byte G, byte B, byte A = 255);

