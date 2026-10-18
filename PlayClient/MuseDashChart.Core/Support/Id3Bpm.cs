using System.Globalization;
using System.Text;

namespace MuseDashChart;

/// <summary>读取 MP3 ID3 标签里的 TBPM / BPM 字段。</summary>
public static class Id3Bpm
{
    public static double? Read(string path)
    {
        try
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            var hdr = new byte[10];
            if (fs.Read(hdr, 0, 10) < 10) return null;
            if (hdr[0] != 'I' || hdr[1] != 'D' || hdr[2] != '3') return null;
            var version = hdr[3];
            var tagSize = SynchSafe(hdr, 6);
            if (tagSize is <= 0 or > 8_000_000) return null;
            var tag = new byte[tagSize];
            var got = fs.Read(tag, 0, tag.Length);
            if (got <= 0) return null;
            return version >= 3 ? ScanV3(tag.AsSpan(0, got), version >= 4) : ScanV2(tag.AsSpan(0, got));
        }
        catch
        {
            return null;
        }
    }

    private static double? ScanV3(ReadOnlySpan<byte> tag, bool v4)
    {
        var i = 0;
        while (i + 10 <= tag.Length)
        {
            var id = Encoding.ASCII.GetString(tag.Slice(i, 4));
            if (id[0] == '\0') break;
            var size = v4 ? SynchSafe(tag, i + 4) : (tag[i + 4] << 24) | (tag[i + 5] << 16) | (tag[i + 6] << 8) | tag[i + 7];
            i += 10;
            if (size <= 0 || i + size > tag.Length) break;
            var body = tag.Slice(i, size);
            i += size;
            if (id == "TBPM")
            {
                var n = ParseNumber(DecodeText(body));
                if (n != null) return n;
            }

            if (id == "TXXX")
            {
                var text = DecodeText(body);
                if (text.StartsWith("BPM", StringComparison.OrdinalIgnoreCase))
                {
                    var parts = text.Split('\0', StringSplitOptions.RemoveEmptyEntries);
                    var last = parts.Length > 0 ? parts[^1] : text;
                    var n = ParseNumber(last);
                    if (n != null) return n;
                }
            }
        }

        return null;
    }

    private static double? ScanV2(ReadOnlySpan<byte> tag)
    {
        var i = 0;
        while (i + 6 <= tag.Length)
        {
            var id = Encoding.ASCII.GetString(tag.Slice(i, 3));
            if (id[0] == '\0') break;
            var size = (tag[i + 3] << 16) | (tag[i + 4] << 8) | tag[i + 5];
            i += 6;
            if (size <= 0 || i + size > tag.Length) break;
            var body = tag.Slice(i, size);
            i += size;
            if (id is "TBP" or "BPM")
            {
                var n = ParseNumber(DecodeText(body));
                if (n != null) return n;
            }
        }

        return null;
    }

    private static string DecodeText(ReadOnlySpan<byte> body)
    {
        if (body.Length == 0) return "";
        var enc = body[0];
        var data = body[1..];
        try
        {
            return enc switch
            {
                0 => Latin1.Encoding.GetString(data).Trim('\0', ' ', '\n', '\r'),
                1 => Encoding.Unicode.GetString(data).Trim('\0', ' ', '\n', '\r'),
                2 => Encoding.BigEndianUnicode.GetString(data).Trim('\0', ' ', '\n', '\r'),
                3 => Encoding.UTF8.GetString(data).Trim('\0', ' ', '\n', '\r'),
                _ => Latin1.Encoding.GetString(body).Trim('\0', ' ', '\n', '\r')
            };
        }
        catch
        {
            return Encoding.ASCII.GetString(data).Trim('\0');
        }
    }

    private static double? ParseNumber(string text)
    {
        var t = text.Replace('\0', ' ').Trim();
        if (t.Length == 0) return null;
        foreach (var token in t.Split(' ', '/', '-', ','))
        {
            if (double.TryParse(token.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var v)
                && v is >= 40 and <= 300)
                return BpmEstimate.Snap(v);
        }

        return null;
    }

    private static int SynchSafe(ReadOnlySpan<byte> b, int offset)
        => ((b[offset] & 0x7F) << 21)
           | ((b[offset + 1] & 0x7F) << 14)
           | ((b[offset + 2] & 0x7F) << 7)
           | (b[offset + 3] & 0x7F);
}

