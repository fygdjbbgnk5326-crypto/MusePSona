namespace MuseDashChart;

public static class SelfCheck
{
    public static List<string> Run()
    {
        var errors = new List<string>();
        try
        {
            var clock = new GridClock(120, 4, 0.25m);
            if (clock.ElementTime != 60m / 120m / 4m)
                errors.Add("GridClock.ElementTime");
            var t = clock.TimeFromIndex(8);
            if (clock.IndexFromTime(t) != 8)
                errors.Add("GridClock roundtrip");
        }
        catch (Exception ex)
        {
            errors.Add("GridClock: " + ex.Message);
        }

        try
        {
            var entries = new List<BmsTime.BpmEntry> { new(0f, BmsTime.SecondsPerMeasure(120f)) };
            var sigs = new Dictionary<int, BmsTime.TimeSigEntry>();
            var sec = BmsTime.CalculateNoteTime(1f, entries, sigs);
            if (MathF.Abs(sec - 2f) >= 0.002f)
                errors.Add($"BmsTime measure: {sec}");
        }
        catch (Exception ex)
        {
            errors.Add("BmsTime: " + ex.Message);
        }

        try
        {
            var doc = SampleCharts.Minimal();
            var json = ProjectJson.Serialize(doc);
            var back = ProjectJson.Deserialize(json);
            if (back.Charts[1].Notes.Count != 4)
                errors.Add("JSON note count");
        }
        catch (Exception ex)
        {
            errors.Add("JSON: " + ex.Message);
        }

        var root = Path.Combine(
            AppContext.BaseDirectory,
            "_selftest");
        try
        {
            if (Directory.Exists(root))
                Directory.Delete(root, true);
            Directory.CreateDirectory(root);
            var src = SampleCharts.Minimal();
            var folder = Path.Combine(root, "chart");
            FolderChart.Save(src, folder, writeProjectJson: false);
            var loaded = FolderChart.Load(folder);
            CompareNotes(src.Charts[1], loaded.Charts[1], errors, "folder");

            ProjectJson.Save(new ChartDocument { Meta = src.Meta, Charts = { [1] = new DifficultyChart() } },
                Path.Combine(folder, FolderChart.ProjectFileName));
            var preferBms = FolderChart.Load(folder);
            if (preferBms.Charts[1].Notes.Count != src.Charts[1].Notes.Count)
                errors.Add("stale chart.json shadowed BMS");
            if (SampleCharts.Blank().Charts[1].Notes.Count != 0)
                errors.Add("blank chart");

            var mdm = Path.Combine(root, "sample.mdm");
            MdmPacker.PackFolder(folder, mdm);
            var unpacked = Path.Combine(root, "unpacked");
            MdmPacker.Unpack(mdm, unpacked);
            var again = FolderChart.Load(unpacked);
            CompareNotes(src.Charts[1], again.Charts[1], errors, "mdm");
            if (again.Meta.Name != "P0 Sample")
                errors.Add("info.json name");

            var bms = BmsWriter.Write(src.Charts[1], src.Meta);
            if (!bms.Contains("#00354:", StringComparison.Ordinal))
                errors.Add("hold measure line missing");
            var parsed = BmsReader.LoadText(bms);
            var hold = BmsReader.ToDifficulty(parsed).Notes.Single(n => n.BmsId == BmsTables.IdHold);
            if (Math.Abs((double)(hold.LengthSec - 2m)) >= 0.02)
                errors.Add($"hold length {hold.LengthSec}");
        }
        catch (Exception ex)
        {
            errors.Add("roundtrip: " + ex);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }

        try
        {
            var chart = new DifficultyChart
            {
                Notes =
                [
                    new Placement { TimeSec = 1, BmsId = "0P", Kind = "event" },
                    new Placement { TimeSec = 3, BmsId = "0Q", Kind = "event" }
                ]
            };
            if (ScrollSpeed.At(chart, 0).Tier != 1) errors.Add("ScrollSpeed default");
            if (ScrollSpeed.At(chart, 1).Tier != 2) errors.Add("ScrollSpeed 0P");
            if (ScrollSpeed.At(chart, 4).Tier != 3) errors.Add("ScrollSpeed 0Q hold");
            if (ScrollSpeed.TierOf("0S") != 2) errors.Add("ScrollSpeed 0S");
            var vig = new DifficultyChart
            {
                Notes =
                [
                    new Placement { TimeSec = 2, BmsId = "2E", Kind = "event" },
                    new Placement { TimeSec = 4, BmsId = "2F", Kind = "event" }
                ]
            };
            if (Vignette.At(vig, 1).On) errors.Add("Vignette default off");
            if (!Vignette.At(vig, 2).On) errors.Add("Vignette on");
            if (Vignette.At(vig, 5).On) errors.Add("Vignette off");
        }
        catch (Exception ex)
        {
            errors.Add("ScrollSpeed: " + ex.Message);
        }

        try
        {
            var chart = new DifficultyChart
            {
                Notes =
                [
                    new Placement { Id = 1, TimeSec = 0, BmsId = "01", Kind = "note", Channel = "13", Pathway = 1, ApproachRate = 1 },
                    new Placement { Id = 2, TimeSec = 1, BmsId = "0F", Kind = "note", Channel = "53", Pathway = 1, LengthSec = 0.5m, ApproachRate = 2 },
                    new Placement { Id = 3, TimeSec = 2, BmsId = "0D", Kind = "note", Channel = "14", ApproachRate = 3 },
                    new Placement { Id = 4, TimeSec = 3, BmsId = "01", Kind = "note", Channel = "14", ApproachRate = 1 }
                ]
            };
            var written = BmsWriter.Write(chart, new AlbumMeta { Bpm = "120" });
            if (!written.Contains("0S:", StringComparison.Ordinal))
                errors.Add("speed 0S channel missing");
            var back = BmsReader.ToDifficulty(BmsReader.LoadText(written)).Notes
                .Where(BmsTables.DrawsOnPathway)
                .OrderBy(n => n.TimeSec).ThenBy(n => n.Id)
                .ToList();
            if (back.Count != 4) errors.Add($"speed notes {back.Count}");
            else
            {
                if (back[0].ApproachRate != 1) errors.Add($"speed[0] {back[0].ApproachRate}");
                if (back[1].ApproachRate != 2) errors.Add($"speed[1] {back[1].ApproachRate}");
                if (back[2].ApproachRate != 3) errors.Add($"speed[2] {back[2].ApproachRate}");
                if (back[3].ApproachRate != 1) errors.Add($"speed[3] {back[3].ApproachRate}");
            }
        }
        catch (Exception ex)
        {
            errors.Add("ApproachSpeed: " + ex.Message);
        }

        try
        {
            var parsed = BmsReader.LoadText("""
                #PLAYER 1
                #BPM 120
                #00013:13
                #00014:13
                #00015:1B
                """);
            var notes = BmsReader.ToDifficulty(parsed).Notes;
            var air = notes.Single(n => n.BmsId == "13" && n.Channel == "13");
            var ground = notes.Single(n => n.BmsId == "13" && n.Channel == "14");
            var exit = notes.Single(n => n.BmsId == "1B");
            if (air.Kind != "note" || air.Pathway != 1) errors.Add("boss projectile air");
            if (ground.Kind != "note" || ground.Pathway != 0) errors.Add("boss projectile ground");
            if (exit.Kind != "event") errors.Add("boss exit event");
        }
        catch (Exception ex)
        {
            errors.Add("boss pathway: " + ex.Message);
        }

        try
        {
            if (BeatPos.Short(0m, 120) != "1.1") errors.Add("BeatPos 0");
            if (BeatPos.Short(2m, 120) != "2.1") errors.Add("BeatPos 2s");
            if (ScrollSpeed.Caption("0P") != "流速2 双轨") errors.Add("ScrollSpeed caption");
            var clock = new GridClock(120, 4);
            if (clock.Snap(0.10m) != 0.125m) errors.Add("grid snap");
            if (Array.IndexOf(GridClock.DefaultGridList, 48) < 0) errors.Add("grid list 48");
            if (Array.IndexOf(GridClock.DefaultGridList, 32) >= 0) errors.Add("grid 32 not 192");
            var bmsClock = new GridClock(160, 4);
            var snapBms = (float)bmsClock.SnapBms(1.662m);
            var snapUnits = BmsTime.TickFromSecondsConstantBpm(snapBms, 160f) * 192f;
            if (Math.Abs(snapUnits - MathF.Round(snapUnits)) > 1e-3f) errors.Add("SnapBms 192");
            if (BmsTime.DelayToSlots(0.35f, 160f) != 45) errors.Add("delay slots");
            if (MasherHits.High(1m) != 25) errors.Add("masher 1s");
            if (MasherHits.High(0.8m) != 20) errors.Add("masher 0.8s");
            if (MasherHits.High(0.04m) != 1) errors.Add("masher tick");
            var mashBms = BmsReader.LoadText("""
                #PLAYER 1
                #BPM 120
                #00054:16
                #00154:16
                """);
            var mash = BmsReader.ToDifficulty(mashBms).Notes.Single(n => n.BmsId == "16");
            if (mash.Kind != "note") errors.Add("boss mash kind");
            if (Math.Abs((double)(mash.LengthSec - 2m)) >= 0.02) errors.Add($"boss mash len {mash.LengthSec}");
            if (MasherHits.High(mash.LengthSec) != 50) errors.Add("boss mash hits");
            CheckParserP0(errors);
            var clicks = ClickTrack(120, 44100, 8);
            var est = BpmEstimate.FromMono(clicks, 44100);
            if (est is null || Math.Abs(est.Value - 120) > 2) errors.Add($"BpmEstimate {est}");
            var clicks176 = ClickTrack(176, 44100, 8);
            var est176 = BpmEstimate.FromMono(clicks176, 44100);
            if (est176 is null || Math.Abs(est176.Value - 176) > 3) errors.Add($"BpmEstimate176 {est176}");
            if (!BpmEstimate.Near(BpmEstimate.AlignToKnown(89.43, 176), 176))
                errors.Add("AlignToKnown 89→176");
            if (BpmEstimate.Format(155.9) != "156") errors.Add("Format int");
            var tmp = Path.Combine(AppContext.BaseDirectory, "md-id3-bpm-" + Guid.NewGuid().ToString("N") + ".mp3");
            File.WriteAllBytes(tmp, FakeId3Mp3(128));
            var tagged = Id3Bpm.Read(tmp);
            if (tagged is null || Math.Abs(tagged.Value - 128) > 0.1) errors.Add($"Id3Bpm {tagged}");
            try { File.Delete(tmp); } catch { }
        }
        catch (Exception ex)
        {
            errors.Add("masher: " + ex.Message);
        }

        try
        {
            CheckApproachOverride(errors);
        }
        catch (Exception ex)
        {
            errors.Add("approach: " + ex.Message);
        }

        return errors;
    }

    private static void CheckParserP0(List<string> errors)
    {
        var bpmChart = BmsReader.ToDifficulty(BmsReader.LoadText("""
            #BPM 120
            #BPM01 180
            #00013:01
            #00108:01
            #00113:0001
            """));
        var written = BmsWriter.Write(bpmChart, new AlbumMeta { Bpm = "120", Name = "p0" });
        var again = BmsReader.ToDifficulty(BmsReader.LoadText(written));
        if (!written.Contains("#BPM01 ", StringComparison.Ordinal) || !written.Contains("08:", StringComparison.Ordinal))
            errors.Add("p0 bpm write");
        if (again.Notes.Count != 2) errors.Add($"p0 bpm notes {again.Notes.Count}");
        else if (Math.Abs((double)again.Notes[1].TimeSec - (double)bpmChart.Notes[1].TimeSec) >= 0.02)
            errors.Add($"p0 bpm time {again.Notes[1].TimeSec}");

        var sigChart = BmsReader.ToDifficulty(BmsReader.LoadText("""
            #BPM 120
            #00002:0.5
            #00013:01
            #00113:01
            """));
        var sigWrite = BmsWriter.Write(sigChart, new AlbumMeta { Bpm = "120" });
        var sigAgain = BmsReader.ToDifficulty(BmsReader.LoadText(sigWrite));
        if (!sigWrite.Contains("#00002:0.5", StringComparison.Ordinal))
            errors.Add("p0 timesig write");
        if (sigAgain.Notes.Count != 2) errors.Add("p0 timesig notes");
        else if (Math.Abs((double)sigAgain.Notes[1].TimeSec - 1.0) >= 0.02)
            errors.Add($"p0 timesig t {sigAgain.Notes[1].TimeSec}");

        var hold = BmsReader.ToDifficulty(BmsReader.LoadText("""
            #BPM 120
            #00054:0F
            """));
        if (hold.Notes.Count != 1 || hold.Notes[0].LengthSec != 0)
            errors.Add("p0 unpaired hold");
        if (!hold.Warnings.Any(w => w.Contains("未配对长按")))
            errors.Add("p0 unpaired warn");

        var clash = new DifficultyChart
        {
            Notes =
            [
                new Placement { Id = 1, TimeSec = 0, BmsId = "01", Kind = "note", Channel = "13", Pathway = 1 },
                new Placement { Id = 2, TimeSec = 0, BmsId = "04", Kind = "note", Channel = "13", Pathway = 1 }
            ]
        };
        var clashBms = BmsWriter.Write(clash, new AlbumMeta { Bpm = "120" });
        var clashCount = clashBms.Split('\n').Count(l => l.StartsWith("#00013:", StringComparison.Ordinal));
        if (clashCount < 2) errors.Add($"p0 overlay {clashCount}");
        var clashNotes = BmsReader.ToDifficulty(BmsReader.LoadText(clashBms)).Notes;
        if (clashNotes.Count != 2 || clashNotes.Any(n => n.TimeSec > 0.02m))
            errors.Add("p0 overlay time");

        var unknown = BmsReader.ToDifficulty(BmsReader.LoadText("""
            #BPM 120
            #00019:2C
            """));
        if (unknown.Notes.Count != 1 || unknown.Notes[0].BmsId != "2C" || unknown.Notes[0].Channel != "19")
            errors.Add("p0 unknown channel");

        CheckApproachOverride(errors);
    }

    private static void CheckApproachOverride(List<string> errors)
    {
        var marks = ApproachSpeed.FromPlayable([(1.0m, 1), (1.0m, 1), (1.0m, 3), (2.0m, 3)]);
        if (marks.Count != 1 || marks[0].TimeSec != 2.0m || marks[0].Rate != 3)
            errors.Add("approach majority tick");

        var chart = new DifficultyChart();
        ApproachSpeed.ApplyAt(chart, 0m, 2);
        var inherit = new Placement { TimeSec = 1m, BmsId = "01", Kind = "note" };
        var locked = new Placement
        {
            TimeSec = 1m, BmsId = BmsTables.IdGhost, Kind = "note", Pathway = 1,
            ApproachRate = 1, ApproachOverride = true
        };
        chart.Notes.Add(inherit);
        chart.Notes.Add(locked);
        ApproachSpeed.StampNotes(chart);
        if (inherit.ApproachRate != 2 || inherit.ApproachOverride)
            errors.Add("approach stamp inherit");
        var stairs = ApproachSpeed.ToBmsMarkers(chart);
        if (stairs.Count != 1 || stairs[0].Rate != 2)
            errors.Add("approach 0S stair mix");
        if (!ApproachSpeed.SidecarOverrides(chart).Any(n => n.BmsId == BmsTables.IdGhost))
            errors.Add("approach sidecar loser");

        var stair = new DifficultyChart();
        stair.Notes.Add(new Placement { TimeSec = 1m, BmsId = "01", Kind = "note", ApproachRate = 1 });
        stair.Notes.Add(new Placement
        {
            TimeSec = 2m, BmsId = "01", Kind = "note", ApproachRate = 3, ApproachOverride = true
        });
        stair.Notes.Add(new Placement { TimeSec = 3m, BmsId = "01", Kind = "note", ApproachRate = 1 });
        var stairMarks = ApproachSpeed.ToBmsMarkers(stair);
        if (stairMarks.Count != 2 || stairMarks[0].Rate != 3 || stairMarks[1].Rate != 1)
            errors.Add("approach 0S sequential pin");
        if (ApproachSpeed.SidecarOverrides(stair).Any())
            errors.Add("approach sequential pin leaked sidecar");

        var doc = SampleCharts.Minimal();
        ApproachSpeed.ApplyAt(doc.Charts[1], 0m, 2);
        doc.Charts[1].Notes.Add(new Placement
        {
            TimeSec = 0m, BmsId = BmsTables.IdGhost, Kind = "note", Pathway = 1,
            ApproachRate = 1, ApproachOverride = true
        });
        ApproachSpeed.StampNotes(doc.Charts[1]);
        var root = Path.Combine(AppContext.BaseDirectory, "_approach_self");
        try
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
            Directory.CreateDirectory(root);
            var folder = Path.Combine(root, "song");
            FolderChart.Save(doc, folder, writeProjectJson: true);
            var loaded = FolderChart.Load(folder);
            var g = loaded.Charts[1].Notes.FirstOrDefault(n => n.BmsId == BmsTables.IdGhost);
            if (g is null || !g.ApproachOverride || g.ApproachRate != 1)
                errors.Add("approach folder override");
            var mdm = Path.Combine(root, "song.mdm");
            MdmPacker.PackFolder(folder, mdm);
            using (var zip = System.IO.Compression.ZipFile.OpenRead(mdm))
            {
                if (!zip.Entries.Any(e => e.Name.Equals("chart.json", StringComparison.OrdinalIgnoreCase)))
                    errors.Add("approach mdm missing chart.json");
            }
            var unpacked = Path.Combine(root, "unpacked");
            MdmPacker.Unpack(mdm, unpacked);
            var again = FolderChart.Load(unpacked);
            var g2 = again.Charts[1].Notes.FirstOrDefault(n => n.BmsId == BmsTables.IdGhost);
            if (g2 is null || !g2.ApproachOverride || g2.ApproachRate != 1)
                errors.Add("approach mdm override");
        }
        catch (Exception ex)
        {
            errors.Add("approach persist: " + ex.Message);
        }
        finally
        {
            try { if (Directory.Exists(root)) Directory.Delete(root, true); } catch { }
        }
    }

    private static void CompareNotes(DifficultyChart expected, DifficultyChart actual, List<string> errors, string tag)
    {
        var a = expected.Notes.Where(n => n.Kind == "note").OrderBy(n => n.TimeSec).ThenBy(n => n.BmsId).ToList();
        var b = actual.Notes.Where(n => n.Kind == "note").OrderBy(n => n.TimeSec).ThenBy(n => n.BmsId).ToList();
        if (a.Count != b.Count)
        {
            errors.Add($"{tag} count {a.Count} vs {b.Count}");
            return;
        }

        for (var i = 0; i < a.Count; i++)
        {
            if (a[i].BmsId != b[i].BmsId || a[i].Pathway != b[i].Pathway)
                errors.Add($"{tag}[{i}] id/path {a[i].BmsId}/{a[i].Pathway} vs {b[i].BmsId}/{b[i].Pathway}");
            if (Math.Abs((double)(a[i].TimeSec - b[i].TimeSec)) >= 0.02)
                errors.Add($"{tag}[{i}] time {a[i].TimeSec} vs {b[i].TimeSec}");
            if (Math.Abs((double)(a[i].LengthSec - b[i].LengthSec)) >= 0.02)
                errors.Add($"{tag}[{i}] len {a[i].LengthSec} vs {b[i].LengthSec}");
        }
    }

    private static float[] ClickTrack(double bpm, int sampleRate, double seconds)
    {
        var n = (int)(sampleRate * seconds);
        var samples = new float[n];
        var interval = 60.0 / bpm;
        for (var beat = 0; beat * interval < seconds; beat++)
        {
            var at = (int)(beat * interval * sampleRate);
            var width = sampleRate / 200;
            for (var i = 0; i < width && at + i < n; i++)
                samples[at + i] = 1f - i / (float)width;
        }

        return samples;
    }

    private static byte[] FakeId3Mp3(int bpm)
    {
        var text = " " + bpm;
        var payload = new byte[1 + text.Length];
        payload[0] = 0;
        System.Text.Encoding.ASCII.GetBytes(text, 0, text.Length, payload, 1);
        var frame = new byte[10 + payload.Length];
        System.Text.Encoding.ASCII.GetBytes("TBPM").CopyTo(frame, 0);
        frame[4] = 0;
        frame[5] = 0;
        frame[6] = 0;
        frame[7] = (byte)payload.Length;
        payload.CopyTo(frame, 10);
        var tagSize = frame.Length;
        var tag = new byte[10 + tagSize];
        tag[0] = (byte)'I';
        tag[1] = (byte)'D';
        tag[2] = (byte)'3';
        tag[3] = 3;
        tag[6] = (byte)((tagSize >> 21) & 0x7F);
        tag[7] = (byte)((tagSize >> 14) & 0x7F);
        tag[8] = (byte)((tagSize >> 7) & 0x7F);
        tag[9] = (byte)(tagSize & 0x7F);
        frame.CopyTo(tag, 10);
        return tag;
    }
}

