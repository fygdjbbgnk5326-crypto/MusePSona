namespace MuseDashChart;

public static class SampleCharts
{
    public static ChartDocument Blank()
    {
        var doc = new ChartDocument
        {
            Meta =
            {
                Name = "未命名谱面",
                Author = "",
                LevelDesigner = "",
                Bpm = "120",
                Scene = "scene_01",
                Difficulty1 = "1"
            }
        };
        doc.EnsureChart(1);
        return doc;
    }

    public static ChartDocument Minimal()
    {
        var doc = new ChartDocument
        {
            Meta =
            {
                Name = "P0 Sample",
                Author = "mdchart",
                Bpm = "120",
                Scene = "scene_01",
                Difficulty1 = "1"
            }
        };
        var chart = doc.EnsureChart(1);
        chart.Notes.Add(new Placement { TimeSec = 0, Pathway = 0, BmsId = BmsTables.IdSmall, Kind = "note" });
        chart.Notes.Add(new Placement { TimeSec = 2, Pathway = 1, BmsId = BmsTables.IdSmall, Kind = "note" });
        chart.Notes.Add(new Placement { TimeSec = 4, Pathway = 0, BmsId = BmsTables.IdGear, Kind = "note" });
        chart.Notes.Add(new Placement
        {
            TimeSec = 6,
            LengthSec = 2,
            Pathway = 0,
            BmsId = BmsTables.IdHold,
            Kind = "note"
        });
        chart.RenumberIds();
        return doc;
    }
}

