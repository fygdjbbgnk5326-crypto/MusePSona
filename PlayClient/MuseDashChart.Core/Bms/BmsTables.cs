namespace MuseDashChart;

[Flags]
public enum ChannelType
{
    None = 0,
    Ground = 1,
    Air = 2,
    Event = 4,
    Scene = 8,
    SpBlood = 16,
    SpTapHolds = 32,
    SpBpmDirect = 64,
    SpBpmLookup = 128,
    SpTimesig = 256,
    SpScroll = 512,
    SpSpeed = 1024
}

public static class BmsTables
{
    public const string IdSmall = "01";
    public const string IdHold = "0F";
    public const string IdMasher = "0G";
    public const string IdGear = "0H";
    public const string IdMusic = "10";
    public const string IdGhost = "21";
    public const string IdHeart = "22";
    public const string IdNote = "23";
    /// <summary>官机 1 槽点长 0F 转成的 02_top 星。不是 type7 音符 23。</summary>
    public const string IdHoldStar = "24";

    /// <summary>点按长按通道（D1–D4）的占位时长，使 IsHoldLike 为真但不写出尾点。</summary>
    public const decimal TapHoldLength = 0.001m;

    /// <summary>超过该时长才写出长按尾点，须大于 <see cref="TapHoldLength"/>。</summary>
    public const decimal HoldEndMinLength = 0.002m;

    public static readonly Dictionary<string, ChannelType> Channels = new(StringComparer.OrdinalIgnoreCase)
    {
        ["01"] = ChannelType.Scene,
        ["02"] = ChannelType.SpTimesig,
        ["03"] = ChannelType.SpBpmDirect,
        ["08"] = ChannelType.SpBpmLookup,
        ["15"] = ChannelType.Event,
        ["16"] = ChannelType.Event,
        // BMS 通道 18 = 事件轨；ibms「18」是 Boss 齿轮（走 13/14 等轨道通道），二者不是同一键。
        ["18"] = ChannelType.Event,
        ["11"] = ChannelType.Air | ChannelType.Event,
        ["13"] = ChannelType.Air | ChannelType.Event,
        ["31"] = ChannelType.Air | ChannelType.Event | ChannelType.SpBlood,
        ["33"] = ChannelType.Air | ChannelType.Event | ChannelType.SpBlood,
        ["51"] = ChannelType.Air | ChannelType.Event,
        ["53"] = ChannelType.Air | ChannelType.Event,
        ["D1"] = ChannelType.Air | ChannelType.Event | ChannelType.SpTapHolds,
        ["D3"] = ChannelType.Air | ChannelType.Event | ChannelType.SpTapHolds,
        ["12"] = ChannelType.Ground | ChannelType.Event,
        ["14"] = ChannelType.Ground | ChannelType.Event,
        ["32"] = ChannelType.Ground | ChannelType.Event | ChannelType.SpBlood,
        ["34"] = ChannelType.Ground | ChannelType.Event | ChannelType.SpBlood,
        ["52"] = ChannelType.Ground | ChannelType.Event,
        ["54"] = ChannelType.Ground | ChannelType.Event,
        ["D2"] = ChannelType.Ground | ChannelType.Event | ChannelType.SpTapHolds,
        ["D4"] = ChannelType.Ground | ChannelType.Event | ChannelType.SpTapHolds,
        ["SC"] = ChannelType.SpScroll,
        ["0S"] = ChannelType.SpSpeed
    };

    public static bool IsHoldKey(string bmsId)
        => bmsId is IdHold or IdMasher or "16" or "17";

    /// <summary>只有 53/54 才两两收条。13/14 上的点长 0F 是导出残渣，不能收成跨曲无限长条。</summary>
    public static bool IsLongNoteChannel(string? channel)
    {
        var c = (channel ?? "").ToUpperInvariant();
        return c is "53" or "54";
    }

    public static bool IsGear(string? bmsId)
    {
        var id = (bmsId ?? "").ToUpperInvariant();
        return id is IdGear or "18";
    }

    public static bool IsScrollEvent(string bmsId)
        => ScrollSpeed.IsScrollId(bmsId);

    public static bool IsPlayableP0(string bmsId)
        => bmsId is IdSmall or "02" or "03" or "04" or "05" or "06" or "07" or "08" or "09"
            or "0A" or "0B" or "0C" or "0D" or "0E"
            or IdHold or IdMasher or IdGear or IdMusic or "20" or IdGhost or IdHeart or IdNote
            or IdHoldStar
            or "40" or "41" or "42";

    /// <summary>落在空中/地面通道上的可玩体：P0 键、Boss 近战/弹幕/齿轮、以及 3x 弹幕。</summary>
    public static bool IsLaneNote(string bmsId)
    {
        if (IsPlayableP0(bmsId)) return true;
        var id = (bmsId ?? "").ToUpperInvariant();
        if (id is "0I" or "0J") return true;
        if (id.Length != 2) return false;
        if (id[0] == '1' && id[1] is >= '1' and <= '8') return true;
        return id[0] == '3';
    }

    public static bool IsAirOrGroundChannel(string? channel)
    {
        if (string.IsNullOrWhiteSpace(channel)) return false;
        if (!Channels.TryGetValue(channel, out var type)) return false;
        return type.HasFlag(ChannelType.Air) || type.HasFlag(ChannelType.Ground);
    }

    public static bool DrawsOnPathway(Placement note)
    {
        if (ApproachSpeed.IsApproachId(note.BmsId))
            return false;
        if (ScrollSpeed.IsScrollId(note.BmsId) || Vignette.IsVignetteId(note.BmsId))
            return false;
        if (note.Kind != "event") return true;
        return IsLaneNote(note.BmsId) && IsAirOrGroundChannel(note.Channel);
    }

    public static string ChannelFor(Placement note)
    {
        if (!string.IsNullOrWhiteSpace(note.Channel))
            return note.Channel;

        if (note.Kind == "event")
            return "15";

        var air = note.Pathway == 1;
        if (note.Blood)
            return air ? "33" : "34";
        if (note.IsHoldLike)
            return air ? "53" : "54";
        return air ? "13" : "14";
    }

    public static int PathwayFromChannel(string channel)
    {
        if (!Channels.TryGetValue(channel, out var type))
            return 0;
        if (type.HasFlag(ChannelType.Air))
            return 1;
        return 0;
    }

    public static string MirrorChannel(string channel) => channel.ToUpperInvariant() switch
    {
        "13" => "14",
        "14" => "13",
        "53" => "54",
        "54" => "53",
        "33" => "34",
        "34" => "33",
        "11" => "12",
        "12" => "11",
        "31" => "32",
        "32" => "31",
        "51" => "52",
        "52" => "51",
        "D1" => "D2",
        "D2" => "D1",
        "D3" => "D4",
        "D4" => "D3",
        _ => channel
    };

    public static void MirrorNote(Placement note)
    {
        if (note.Kind == "event") return;
        note.Pathway = note.Pathway == 1 ? 0 : 1;
        if (!string.IsNullOrWhiteSpace(note.Channel))
            note.Channel = MirrorChannel(note.Channel);
        else
            note.Channel = "";
    }
}

