using System.Collections.Generic;
using System.IO;
using System.Linq;
using MuseDashChart;
using MusePSona.Play;
using Xunit;

public class FlightTests
{
    [Fact]
    public void Gear_0H_UsesX03_NorClip()
    {
        var n = new PlayNote { BmsId = "0H", Category = NoteCategory.Gear, Pathway = 0, ApproachRate = 1, NeedsHit = false };
        var spec = AnimBinding.Of(n)!.Value;
        Assert.Equal("03", spec.Code);
        Assert.Equal("in_nor_44", AnimBinding.InClip(spec, 1));
        Assert.Equal("in_nor_33", AnimBinding.InClip(spec, 3));
        var boss = new PlayNote { BmsId = "18", Category = NoteCategory.Gear, ApproachRate = 3, NeedsHit = false };
        Assert.Equal("boss_1_nor_15", AnimBinding.InClip(AnimBinding.Of(boss)!.Value, 3));
        Assert.Equal(NoteCategory.Gear, PlayNote.CategoryOf("0H", 0m));
        Assert.Equal(NoteCategory.Gear, PlayNote.CategoryOf("18", 0m));
    }

    [Fact]
    public void Gear_AfterJudge_FliesThroughOnRoad()
    {
        var n = new PlayNote
        {
            TimeSec = 2m,
            ApproachRate = 1,
            BmsId = "0H",
            Category = NoteCategory.Gear,
            Pathway = 0,
            NeedsHit = false
        };
        Assert.Equal(Flight.GearRoadY, Flight.LaneY(n));
        Assert.True(Flight.TryExitPose(n, 2.25f, out var p));
        Assert.True(p.X < Flight.HitX);
        Assert.InRange(p.Y, Flight.GearRoadY - 0.02f, Flight.GearRoadY + 0.02f);
        var air = new PlayNote { BmsId = "18", Category = NoteCategory.Gear, Pathway = 1, NeedsHit = false };
        Assert.Equal(Flight.GearAirY, Flight.LaneY(air));
    }

    [Fact]
    public void BossGear_18_UsesShortApproach()
    {
        var n = new PlayNote { BmsId = "18", ApproachRate = 3, Category = NoteCategory.Gear, NeedsHit = false };
        Assert.Equal(15, Flight.HitFrames(n));
    }

    [Fact]
    public void ShortApproach_KeepsNormalLaneSpeed()
    {
        var gear = new PlayNote { TimeSec = 2m, ApproachRate = 3, BmsId = "18", Category = NoteCategory.Gear, Pathway = 1 };
        var skull = new PlayNote { TimeSec = 2m, ApproachRate = 3, BmsId = "21", Category = NoteCategory.Ghost, Pathway = 1 };
        var tap = new PlayNote { TimeSec = 2m, ApproachRate = 3, BmsId = "01", Pathway = 0 };
        var t = 2f - 0.4f;
        Assert.True(Flight.TryPose(gear, t, out var g));
        Assert.True(Flight.TryPose(skull, t, out var s));
        Assert.True(Flight.TryPose(tap, t, out var a));
        Assert.InRange(g.X, a.X - 0.04f, a.X + 0.04f);
        Assert.InRange(s.X, a.X - 0.04f, a.X + 0.04f);
        var spawn = 2f - Flight.DurationSec(gear);
        Assert.True(Flight.TryPose(gear, spawn, out var gs));
        Assert.True(gs.X < Flight.SpawnX - 1f);
    }

    [Fact]
    public void LinearX_AtJudge_IsHitLine()
    {
        var n = new PlayNote { TimeSec = 2m, ApproachRate = 1, BmsId = "01", Pathway = 0 };
        var x = Flight.LinearX(n, 2f);
        Assert.InRange(x, Flight.HitX - 0.01f, Flight.HitX + 0.01f);
    }

    [Fact]
    public void HoldTail_IsRightOfHead()
    {
        var n = new PlayNote
        {
            TimeSec = 2m,
            LengthSec = 0.4m,
            ApproachRate = 1,
            BmsId = "0F",
            Category = NoteCategory.Hold,
            Pathway = 0
        };
        var head = Flight.LinearX(n, 1.9f);
        var tail = Flight.LinearXAt(n, (float)n.EndSec, 1.9f);
        Assert.False(float.IsNaN(head));
        Assert.False(float.IsNaN(tail));
        Assert.True(tail > head);
    }

    [Fact]
    public void Hold_InjectsHittableBeatStars()
    {
        var hold = new PlayNote
        {
            Id = 1,
            TimeSec = 42.9m,
            LengthSec = 1.2m,
            Pathway = 1,
            BmsId = "0F",
            ApproachRate = 2,
            Category = NoteCategory.Hold,
            NeedsHit = true,
            CountsCombo = true
        };
        var notes = PlayNote.WithHoldBeatStars(new[] { hold }, 200f);
        var stars = notes.Where(n => n.BmsId == "23").ToList();
        Assert.Equal(3, stars.Count);
        Assert.All(stars, s =>
        {
            Assert.Equal(NoteCategory.Music, s.Category);
            Assert.True(s.NeedsHit);
            Assert.False(s.CountsCombo);
            Assert.Equal(1, s.Pathway);
            Assert.Equal(2, s.ApproachRate);
        });
        Assert.Single(PlayNote.WithHoldBeatStars(notes, 200f).Where(n => n.BmsId == "23" && Math.Abs((float)n.TimeSec - 43.2f) < 0.02f));
    }

    [Fact]
    public void Hold_StarTimes_SkipHeadAndTail()
    {
        var mid = Flight.HoldStarTimes(50.1f, 51.3f, 200f);
        Assert.Equal(3, mid.Count);
        Assert.InRange(mid[0], 50.39f, 50.41f);
        Assert.InRange(mid[1], 50.69f, 50.71f);
        Assert.InRange(mid[2], 50.99f, 51.01f);
        var shortHold = Flight.HoldStarTimes(10.5f, 11.1f, 200f);
        Assert.Single(shortHold);
        Assert.InRange(shortHold[0], 10.79f, 10.81f);
        Assert.Empty(Flight.HoldStarTimes(1f, 1.2f, 200f));
    }

    [Fact]
    public void Hold_DoesNotCrossHitLine()
    {
        var n = new PlayNote
        {
            TimeSec = 2m,
            LengthSec = 0.5m,
            ApproachRate = 1,
            BmsId = "0F",
            Category = NoteCategory.Hold,
            Pathway = 0
        };
        Assert.True(Flight.TryHoldSpan(n, 2.1f, out var head, out var tail));
        Assert.Equal(Flight.HitX, head);
        Assert.True(tail > head);
        Assert.False(Flight.TryHoldSpan(n, 2.6f, out _, out _));
        Assert.True(Flight.TryHoldSpan(n, 2.0f, out var atHit, out _, pinHead: false));
        Assert.Equal(Flight.HitX, atHit);
        Assert.True(Flight.TryHoldSpan(n, 2.12f, out var missHead, out _, pinHead: false));
        Assert.True(missHead < Flight.HitX);
        Assert.True(Flight.TryHoldSpan(n, 2.13f, out var nextHead, out _, pinHead: false));
        Assert.True(nextHead < missHead);
        Assert.True(Flight.TryHoldSpan(n, 2.6f, out var laterHead, out var laterTail, pinHead: false));
        Assert.True(laterHead < nextHead);
        Assert.True(laterTail < Flight.HitX);
        Assert.Equal(1f, Flight.HoldMissVisual(n, 2f));
        Assert.InRange(Flight.HoldMissVisual(n, 2.1f), 0.69f, 0.70f);
        Assert.Equal(Flight.HoldMissAlpha, Flight.HoldMissVisual(n, 2.3f));
    }

    [Fact]
    public void Hold_DoesNotBindSpinePack()
    {
        var n = new PlayNote { BmsId = "0F", Category = NoteCategory.Hold, Pathway = 1, ApproachRate = 1 };
        Assert.Null(AnimBinding.Of(n));
        Assert.Null(AnimBinding.CollectPack(n));
        Assert.False(Flight.CenterLane(n));
        Assert.Equal(Flight.AirY, Flight.LaneY(n));
        Assert.Equal(44, Flight.HitFrames(n));
    }

    [Fact]
    public void Pendulum_AtJudge_IsHitLane()
    {
        var n = new PlayNote
        {
            TimeSec = 2m,
            ApproachRate = 1,
            BmsId = "0D",
            Pathway = 0,
            Special = NoteSpecial.Pendulum
        };
        Assert.True(Flight.TryPose(n, 2f, out var at));
        Assert.InRange(at.X, Flight.HitX - 0.02f, Flight.HitX + 0.02f);
        Assert.InRange(at.Y, Flight.RoadY - 0.02f, Flight.RoadY + 0.02f);
        Assert.True(Flight.TryPose(n, 1.7f, out var mid));
        Assert.True(mid.HasPivot);
        Assert.True(mid.X > at.X);
        Assert.True(Flight.TryPendulum(n, 0f, out var far));
        Assert.True(far.X > mid.X);
        Assert.True(far.HasPivot);
        Assert.True(Flight.TryPose(n, 2.2f, out var past));
        Assert.True(past.X < at.X);
        Assert.True(past.HasPivot);
    }

    [Fact]
    public void Raider_StartsOnBottomEdge()
    {
        var n = new PlayNote
        {
            TimeSec = 2m,
            ApproachRate = 1,
            BmsId = "0C",
            Pathway = 0,
            Special = NoteSpecial.Raider
        };
        var spawn = 2f - Flight.DurationSec(n);
        Assert.True(Flight.TryPose(n, spawn, out var p));
        Assert.InRange(p.Y, Flight.RaiderEdge - 0.05f, Flight.RaiderEdge + 0.05f);
        Assert.InRange(p.X, Flight.HitX + Flight.Travel - 0.05f, Flight.HitX + Flight.Travel + 0.05f);
    }

    [Fact]
    public void Raider_RunsOnEdgeThenJumpsToLane()
    {
        var n = new PlayNote
        {
            TimeSec = 2m,
            ApproachRate = 2,
            BmsId = "0C",
            Pathway = 0,
            Special = NoteSpecial.Raider
        };
        var dur = Flight.DurationSec(n);
        Assert.Equal(30, Flight.HitFrames(n));
        Assert.True(Flight.TryPose(n, 2f - dur * 0.75f, out var run));
        Assert.InRange(run.Y, Flight.RaiderEdge - 0.05f, Flight.RaiderEdge + 0.05f);
        Assert.True(Flight.TryPose(n, 2f - dur * 0.4f, out var jump));
        Assert.True(jump.Y < Flight.RaiderEdge - 0.2f);
        Assert.True(jump.Y > Flight.RoadY - Flight.RaiderOver - 0.05f);
        Assert.True(Flight.TryPose(n, 2f, out var hit));
        Assert.InRange(hit.X, Flight.HitX - 0.02f, Flight.HitX + 0.02f);
        Assert.InRange(hit.Y, Flight.RoadY - 0.02f, Flight.RoadY + 0.02f);
    }

    [Fact]
    public void RaiderInvert_RunsOnTopEdge()
    {
        var n = new PlayNote
        {
            TimeSec = 2m,
            ApproachRate = 3,
            BmsId = "0I",
            Pathway = 1,
            Special = NoteSpecial.Raider,
            Inverted = true
        };
        var spawn = 2f - Flight.DurationSec(n);
        var mid = (Flight.AirY + Flight.RoadY) * 0.5f;
        var top = 2f * mid - Flight.RaiderEdge;
        Assert.True(Flight.TryPose(n, spawn, out var p));
        Assert.InRange(p.Y, top - 0.08f, top + 0.08f);
        Assert.Equal("in_up_24", AnimBinding.InClip(AnimBinding.Of(n)!.Value, 3));
    }

    [Fact]
    public void Rise_EntersFromBottom()
    {
        var n = new PlayNote { TimeSec = 2m, ApproachRate = 1, BmsId = "02", Pathway = 0 };
        var spawn = 2f - Flight.DurationSec(n);
        Assert.True(Flight.TryPose(n, spawn, out var p));
        Assert.True(p.Y > Flight.RoadY + 1f);
        Assert.InRange(p.X, Flight.UpdownX - 0.05f, Flight.UpdownX + 0.05f);
    }

    [Fact]
    public void WeatherDoll_Rate3_Is23Frames_ButDoesNotSnapLaneEarly()
    {
        var n = new PlayNote { TimeSec = 2m, ApproachRate = 3, BmsId = "05", Pathway = 1 };
        Assert.Equal(23, Flight.HitFrames(n));
        var dur = Flight.DurationSec(n);
        Assert.InRange(dur, 0.766f, 0.768f);
        Assert.True(Flight.TryPose(n, 2f - Flight.UpdownMerge, out var near));
        var remain = MathF.Abs(near.Y - Flight.AirY);
        var full = MathF.Abs(Flight.UpdownEdge - Flight.AirY);
        Assert.True(remain > 0.02f && remain < 0.25f * full);
        Assert.True(Flight.TryPose(n, 2f - dur * 0.5f, out var mid));
        Assert.True(MathF.Abs(mid.Y - Flight.AirY) > 0.2f * full);
    }

    [Fact]
    public void WeatherDoll_InClip_IsUp23_NotRaider24()
    {
        var doll = new PlayNote { BmsId = "05", ApproachRate = 3 };
        var raider = new PlayNote { BmsId = "0C", ApproachRate = 3, Special = NoteSpecial.Raider };
        var drop = new PlayNote { BmsId = "06", ApproachRate = 3 };
        Assert.Equal("in_up_23", AnimBinding.InClip(AnimBinding.Of(doll)!.Value, 3));
        Assert.Equal("in_up_24", AnimBinding.InClip(AnimBinding.Of(raider)!.Value, 3));
        Assert.Equal("in_down_23", AnimBinding.InClip(AnimBinding.Of(drop)!.Value, 3));
        Assert.Equal("in_up_30", AnimBinding.InClip(AnimBinding.Of(doll)!.Value, 1));
    }

    [Fact]
    public void SkeletonKey_Scene03_RoadSmall()
    {
        var n = new PlayNote { BmsId = "01", Pathway = 0, Category = NoteCategory.Monster };
        var spec = AnimBinding.Of(n)!.Value;
        Assert.Equal("10", spec.Code);
        Assert.Equal("0310_road", AnimBinding.SkeletonKey("scene_03", spec, n));
        var large = new PlayNote { BmsId = "0A", Pathway = 0, Category = NoteCategory.Monster };
        Assert.Equal("13", AnimBinding.Of(large)!.Value.Code);
        Assert.Equal("14", AnimBinding.Of(new PlayNote { BmsId = "0B", Pathway = 1, Category = NoteCategory.Monster })!.Value.Code);
    }

    [Fact]
    public void Pendulum_UsesX15_OppositeLanePack()
    {
        var down = new PlayNote { BmsId = "0D", Pathway = 0, Special = NoteSpecial.Pendulum, ApproachRate = 1 };
        var up = new PlayNote { BmsId = "0J", Pathway = 0, Special = NoteSpecial.Pendulum, Inverted = true, ApproachRate = 3 };
        var spec = AnimBinding.Of(down)!.Value;
        Assert.Equal("15", spec.Code);
        Assert.Equal("num", spec.Group);
        Assert.Equal("in_26", AnimBinding.InClip(spec, 1));
        Assert.Equal("in_21", AnimBinding.InClip(spec, 2));
        Assert.Equal("in_18", AnimBinding.InClip(spec, 3));
        Assert.Equal("0115_road", AnimBinding.SkeletonKey("scene_01", spec, down));
        Assert.Equal("0115_air", AnimBinding.SkeletonKey("scene_01", AnimBinding.Of(up)!.Value, up));
    }

    [Fact]
    public void Raider_UsesX16_NotWeatherClip()
    {
        var rush = new PlayNote { BmsId = "0C", Pathway = 0, Special = NoteSpecial.Raider, ApproachRate = 2 };
        var drop = new PlayNote { BmsId = "0I", Pathway = 1, Special = NoteSpecial.Raider, Inverted = true, ApproachRate = 3 };
        var spec = AnimBinding.Of(rush)!.Value;
        Assert.Equal("16", spec.Code);
        Assert.Equal("in_up_30", AnimBinding.InClip(spec, 2));
        Assert.Equal("0116_road", AnimBinding.SkeletonKey("scene_01", spec, rush));
        Assert.Equal("0116_road", AnimBinding.SkeletonKey("scene_01", AnimBinding.Of(drop)!.Value, drop));
    }

    [Fact]
    public void HeartAndMusic_UseCommonSpinePacks()
    {
        var heart = new PlayNote { BmsId = "22", Category = NoteCategory.Heart, Pathway = 0, NeedsHit = true };
        var airHeart = new PlayNote { BmsId = "22", Category = NoteCategory.Heart, Pathway = 1, NeedsHit = true };
        var note = new PlayNote { BmsId = "23", Category = NoteCategory.Music, Pathway = 0, NeedsHit = true };
        Assert.Null(AnimBinding.Of(heart));
        Assert.Equal("0002_hp", AnimBinding.CollectPack(heart));
        Assert.Equal("0003_score", AnimBinding.CollectPack(note));
        Assert.Equal("in_road", AnimBinding.CollectInClip(heart));
        Assert.Equal("in_air", AnimBinding.CollectInClip(airHeart));
        Assert.True(heart.NeedsHit);
        Assert.False(heart.CountsCombo);
    }

    [Fact]
    public void DualPressAndMasher_FollowPathway()
    {
        var mash = new PlayNote { BmsId = "0G", Category = NoteCategory.Masher, Pathway = 0 };
        var mashAir = new PlayNote { BmsId = "16", Category = NoteCategory.Masher, Pathway = 1 };
        var melee = new PlayNote { BmsId = "11", Category = NoteCategory.Boss, Pathway = 1 };
        var shot = new PlayNote { BmsId = "13", Category = NoteCategory.Boss, Pathway = 1 };
        var shotRoad = new PlayNote { BmsId = "14", Category = NoteCategory.Boss, Pathway = 0, ApproachRate = 3 };
        var shot3 = new PlayNote { BmsId = "15", Category = NoteCategory.Boss, Pathway = 1, ApproachRate = 2 };
        var gear = new PlayNote { BmsId = "18", Category = NoteCategory.Gear, Pathway = 0 };
        var dual = new PlayNote { BmsId = "0E", Category = NoteCategory.Monster, Pathway = 0 };
        Assert.Equal(NoteCategory.Masher, PlayNote.CategoryOf("16", 1m));
        Assert.Equal(Flight.MidY, Flight.LaneY(mash));
        Assert.Equal(Flight.MidY, Flight.LaneY(mashAir));
        Assert.Equal(Flight.RoadY, Flight.LaneY(dual));
        Assert.False(Flight.CenterLane(dual));
        Assert.Equal("05", AnimBinding.Of(dual)!.Value.Code);
        Assert.Equal("in_nor_44", AnimBinding.InClip(AnimBinding.Of(dual)!.Value, 1));
        Assert.Equal(Flight.MidY, Flight.LaneY(melee));
        Assert.Equal(Flight.AirY, Flight.LaneY(shot));
        Assert.Equal(Flight.RoadY, Flight.LaneY(shotRoad));
        Assert.Equal(Flight.AirY, Flight.LaneY(shot3));
        Assert.False(Flight.CenterLane(shot));
        Assert.Equal(Flight.GearRoadY, Flight.LaneY(gear));
        Assert.Equal(Flight.GearAirY, Flight.LaneY(new PlayNote { BmsId = "0H", Category = NoteCategory.Gear, Pathway = 1 }));
        Assert.Equal("boss", AnimBinding.Of(shot)!.Value.Group);
        Assert.Equal("in_boss_1_15", AnimBinding.InClip(AnimBinding.Of(shot)!.Value, 3));
        Assert.Equal("boss2", AnimBinding.Of(shotRoad)!.Value.Group);
        Assert.Equal("in_boss_2_17", AnimBinding.InClip(AnimBinding.Of(shotRoad)!.Value, 3));
        Assert.Equal("in_boss_2_20", AnimBinding.InClip(AnimBinding.Of(shot3)!.Value, 2));
        Assert.Equal(17, Flight.HitFrames(shotRoad));
        Flight.Scene = "scene_05";
        Assert.Equal(24, Flight.HitFrames(shotRoad));
        Flight.Scene = "";
    }

    [Fact]
    public void Masher_StaysOnHitLineAfterArrive()
    {
        var n = new PlayNote
        {
            TimeSec = 2m,
            LengthSec = 0.4m,
            ApproachRate = 1,
            BmsId = "0G",
            Category = NoteCategory.Masher,
            Pathway = 0
        };
        Assert.True(Flight.TryPose(n, 1.5f, out var before));
        Assert.True(before.X > Flight.HitX);
        Assert.True(Flight.TryPose(n, 2.2f, out var hold));
        Assert.Equal(Flight.HitX, hold.X);
        Assert.Equal(Flight.MidY, hold.Y);
    }

    [Fact]
    public void AfterJudge_DoesNotPassHitLine()
    {
        var tap = new PlayNote { TimeSec = 2m, ApproachRate = 1, BmsId = "01", Pathway = 0 };
        Assert.Equal(Flight.HitX, Flight.LinearX(tap, 2.25f));
        Assert.True(Flight.TryPose(tap, 2.25f, out var tapPose));
        Assert.Equal(Flight.HitX, tapPose.X);

        var pend = new PlayNote
        {
            TimeSec = 2m,
            ApproachRate = 1,
            BmsId = "0D",
            Pathway = 0,
            Special = NoteSpecial.Pendulum
        };
        Assert.True(Flight.TryPose(pend, 2.25f, out var pendPose));
        Assert.True(pendPose.X < Flight.HitX);
        Assert.True(pendPose.HasPivot);

        var gear = new PlayNote
        {
            TimeSec = 2m,
            ApproachRate = 1,
            BmsId = "0H",
            Category = NoteCategory.Gear,
            Pathway = 0,
            NeedsHit = false
        };
        Assert.Equal(Flight.HitX, Flight.LinearX(gear, 2.25f));
    }

    [Fact]
    public void Miss_FliesPastHitLine()
    {
        var tap = new PlayNote { TimeSec = 2m, ApproachRate = 1, BmsId = "01", Pathway = 0, NeedsHit = true };
        Assert.True(Flight.TryExitPose(tap, 2.25f, out var pose));
        Assert.True(pose.X < Flight.HitX);
        Assert.Equal(Flight.ThroughX(tap, 2.25f), pose.X);
        Assert.False(Flight.TryExitPose(tap, 4f, out _));
    }

    [Fact]
    public void Ghost_FadesInLastPointOneFive()
    {
        var n = new PlayNote { TimeSec = 2m, BmsId = "21", Category = NoteCategory.Ghost, Pathway = 1 };
        Assert.Equal(1f, Flight.GhostAlpha(n, 1.8f));
        Assert.InRange(Flight.GhostAlpha(n, 1.925f), 0.49f, 0.51f);
        Assert.Equal(0f, Flight.GhostAlpha(n, 2f));
    }

    [Fact]
    public void Autoplay_Ghost_CountsComboAndHides()
    {
        var doc = new MuseDashChart.ChartDocument();
        doc.Charts[1] = new MuseDashChart.DifficultyChart
        {
            Notes =
            {
                new MuseDashChart.Placement { Id = 1, TimeSec = 1m, BmsId = "21", Pathway = 1, ApproachRate = 3, Kind = "note" }
            }
        };
        var s = new PlaySession(new PlaySong(doc, ".", null), 1);
        s.Autoplay(1.01f);
        Assert.Equal(Judge.Grade.Perfect, s.GradeOf(1));
        Assert.Equal(1, s.Perfects);
        Assert.Equal(1, s.Combo);
        Assert.DoesNotContain(s.Visible(1.01f), n => n.Id == 1);
        Assert.Contains(s.Visible(0.9f), n => n.Id == 1);
    }
}

public class SceneTests
{
    [Fact]
    public void OneQ_IsCastle()
    {
        var notes = new[]
        {
            new MuseDashChart.Placement { TimeSec = 10, BmsId = "1Q", Kind = "event" }
        };
        var steps = SceneTimeline.Build(notes, "scene_01");
        Assert.Equal("scene_01", SceneTimeline.At(steps, 0));
        Assert.Equal("scene_05", SceneTimeline.At(steps, 10));
    }

    [Fact]
    public void Needed_DefaultThenChangesOnceEach()
    {
        var notes = new[]
        {
            new MuseDashChart.Placement { TimeSec = 10, BmsId = "1Q", Kind = "event" },
            new MuseDashChart.Placement { TimeSec = 20, BmsId = "1R", Kind = "event" },
            new MuseDashChart.Placement { TimeSec = 21, BmsId = "1Q", Kind = "event" }
        };
        var steps = SceneTimeline.Build(notes, "scene_01");
        Assert.Equal(new[] { "scene_01", "scene_05", "scene_03" }, SceneTimeline.Needed(steps));
    }

    [Fact]
    public void OneY_IsScene12_OneX_IsStage10()
    {
        var notes = new[]
        {
            new MuseDashChart.Placement { TimeSec = 1, BmsId = "1Y", Kind = "event" },
            new MuseDashChart.Placement { TimeSec = 2, BmsId = "1X", Kind = "event" }
        };
        var steps = SceneTimeline.Build(notes, "scene_01");
        Assert.Equal("scene_12", SceneTimeline.At(steps, 1.1f));
        Assert.Equal("scene_10", SceneTimeline.At(steps, 2.1f));
        Assert.False(SceneTimeline.At(steps, 1.1f) == "scene_01");
    }
}

public class BossTests
{
    [Fact]
    public void ScriptAnims_DoNotCountAsHits()
    {
        var chart = new[]
        {
            new MuseDashChart.Placement { TimeSec = 1, BmsId = "1A", Kind = "event" },
            new MuseDashChart.Placement { TimeSec = 4, BmsId = "1C", Kind = "event" },
            new MuseDashChart.Placement { TimeSec = 8, BmsId = "1G", Kind = "event" },
            new MuseDashChart.Placement { TimeSec = 9, BmsId = "1E", Kind = "event" },
            new MuseDashChart.Placement { TimeSec = 12, BmsId = "1B", Kind = "event" }
        };
        var boss = BossTimeline.Build(chart, Array.Empty<PlayNote>());
        var enter = boss.At(1.2f);
        Assert.True(enter.Visible);
        Assert.Equal("in", enter.Clip);
        Assert.False(enter.CountsHit);
        var idle = boss.At(2.5f);
        Assert.Equal("standby", idle.Clip);
        Assert.True(idle.Loop);
        Assert.False(idle.CountsHit);
        var far = boss.At(4.2f);
        Assert.Equal("far_atk_1_start", far.Clip);
        Assert.False(far.CountsHit);
        var farIdle = boss.At(5.0f);
        Assert.Equal("far_atk_1_standby", farIdle.Clip);
        var phase = boss.At(8.1f);
        Assert.Equal("atk_1_to_2", phase.Clip);
        Assert.False(phase.CountsHit);
        Assert.Equal("far_atk_2_standby", boss.At(8.75f).Clip);
        var far2 = boss.At(9.9f);
        Assert.Equal("far_atk_2_standby", far2.Clip);
        var leave = boss.At(12.2f);
        Assert.Equal("out", leave.Clip);
        Assert.False(boss.At(14f).Visible);
    }

    [Fact]
    public void AfterOut_1M_DoesNotReturn()
    {
        var chart = new[]
        {
            new MuseDashChart.Placement { TimeSec = 1, BmsId = "1A", Kind = "event" },
            new MuseDashChart.Placement { TimeSec = 4, BmsId = "1B", Kind = "event" },
            new MuseDashChart.Placement { TimeSec = 6, BmsId = "1M", Kind = "event" }
        };
        var boss = BossTimeline.Build(chart, Array.Empty<PlayNote>());
        Assert.Equal("out", boss.At(4.2f).Clip);
        Assert.Equal("out", boss.At(5.2f).Clip);
        Assert.False(boss.At(5.8f).Visible);
        Assert.False(boss.At(6.1f).Visible);
    }

    [Fact]
    public void AfterOut_1A_WalksIn()
    {
        var chart = new[]
        {
            new MuseDashChart.Placement { TimeSec = 1, BmsId = "1A", Kind = "event" },
            new MuseDashChart.Placement { TimeSec = 4, BmsId = "1B", Kind = "event" },
            new MuseDashChart.Placement { TimeSec = 8, BmsId = "1A", Kind = "event" }
        };
        var boss = BossTimeline.Build(chart, Array.Empty<PlayNote>());
        Assert.False(boss.At(6f).Visible);
        var back = boss.At(8.1f);
        Assert.True(back.Visible);
        Assert.Equal("in", back.Clip);
    }

    [Fact]
    public void Sandwich1B_Between1AAnd1M_IsNotLeave()
    {
        var chart = new[]
        {
            new MuseDashChart.Placement { TimeSec = 1, BmsId = "1A", Kind = "event" },
            new MuseDashChart.Placement { TimeSec = 1.2m, BmsId = "1B", Kind = "event" },
            new MuseDashChart.Placement { TimeSec = 3, BmsId = "1M", Kind = "event" }
        };
        var boss = BossTimeline.Build(chart, Array.Empty<PlayNote>());
        Assert.True(boss.At(1.5f).Visible);
        Assert.NotEqual("out", boss.At(1.5f).Clip);
        Assert.True(boss.At(3.1f).Visible);
    }

    [Fact]
    public void Mash17_Then1M_DoesNotReturn()
    {
        var mash = new PlayNote { Id = 1, TimeSec = 10m, LengthSec = 0.3m, BmsId = "17", Category = NoteCategory.Masher, NeedsHit = true };
        var chart = new[]
        {
            new MuseDashChart.Placement { TimeSec = 12, BmsId = "1M", Kind = "event" }
        };
        var boss = BossTimeline.Build(chart, new[] { mash });
        Assert.Equal("multi_atk_hurt_end", boss.At(10.5f).Clip);
        var gone = 10.3f + BossTimeline.HurtEndSec;
        Assert.False(boss.At(12.1f).Visible);
        Assert.False(boss.At(gone + 0.05f).Visible);
    }

    [Fact]
    public void Mash17_Nearby1A_DoesNotWalkBackIn()
    {
        var mash = new PlayNote { Id = 1, TimeSec = 10m, LengthSec = 0.3m, BmsId = "17", Category = NoteCategory.Masher, NeedsHit = true };
        var chart = new[]
        {
            new MuseDashChart.Placement { TimeSec = 10.2m, BmsId = "1A", Kind = "event" },
            new MuseDashChart.Placement { TimeSec = 10.4m, BmsId = "1B", Kind = "event" },
            new MuseDashChart.Placement { TimeSec = 12, BmsId = "1M", Kind = "event" },
            new MuseDashChart.Placement { TimeSec = 20, BmsId = "1A", Kind = "event" }
        };
        var boss = BossTimeline.Build(chart, new[] { mash });
        var gone = 10.3f + BossTimeline.HurtEndSec;
        Assert.False(boss.At(12.1f).Visible);
        Assert.False(boss.At(gone + 0.2f).Visible);
        var back = boss.At(20.1f);
        Assert.True(back.Visible);
        Assert.Equal("in", back.Clip);
    }

    [Fact]
    public void MeleeWindup_IsNotAHit_DanmakuNote_Is()
    {
        var melee = new PlayNote { TimeSec = 10m, BmsId = "11", Category = NoteCategory.Boss, NeedsHit = true };
        var shot = new PlayNote { TimeSec = 10m, BmsId = "13", Category = NoteCategory.Boss, NeedsHit = true };
        var boss = BossTimeline.Build(Array.Empty<MuseDashChart.Placement>(), new[] { melee, shot });
        var wind = boss.At(10f - BossTimeline.Close1Hit);
        Assert.True(wind.Visible);
        Assert.Equal("close_atk_1_48", wind.Clip);
        Assert.Equal("hurt", boss.At(10.05f).Clip);
        // 回场在 close_atk 尾部。命中后不能在 0.7s 切待机，否则 0601 从右侧回来的那段被掐掉。
        Assert.Equal("hurt", boss.At(10f + 0.85f).Clip);
        Assert.NotEqual("hurt", boss.At(10f + BossTimeline.Close1Dur - BossTimeline.Close1Hit + 0.05f).Clip);
        Assert.False(wind.CountsHit);
        Assert.True(melee.NeedsHit);
        Assert.True(shot.NeedsHit);
        Assert.True(BossTimeline.IsDanmaku("13"));
        Assert.True(BossTimeline.IsMelee("11"));
        Assert.True(BossTimeline.IsBossMash("17"));
        Assert.False(BossTimeline.IsBossMash("0G"));
        Assert.False(BossTimeline.DrawsBody(new PlayNote { BmsId = "0I", Special = NoteSpecial.Raider }));
    }

    [Fact]
    public void MeleeThen1B_LeavesInsteadOfStanding()
    {
        var melee = new PlayNote { TimeSec = 10m, BmsId = "11", Category = NoteCategory.Boss, NeedsHit = true };
        var chart = new[]
        {
            new MuseDashChart.Placement { TimeSec = 1m, BmsId = "1A", Kind = "event" },
            new MuseDashChart.Placement { TimeSec = 10.88m, BmsId = "1B", Kind = "event" }
        };
        var boss = BossTimeline.Build(chart, new[] { melee });
        Assert.Equal("close_atk_1_48", boss.At(10f - 0.1f).Clip);
        var returning = boss.At(10.2f);
        Assert.Equal("hurt", returning.Clip);
        Assert.InRange(returning.Time, 0.15f, 0.3f);
        var outAt = 10f - BossTimeline.Close1Hit + BossTimeline.Close1Dur;
        Assert.Equal("out", boss.At(outAt + 0.05f).Clip);
        Assert.False(boss.At(outAt + BossTimeline.OutSec + 0.05f).Visible);
    }

    [Fact]
    public void Map4MeleeThenFar_HurtBringsHerBack()
    {
        var melee = new PlayNote { TimeSec = 132.22m, BmsId = "11", Category = NoteCategory.Boss, NeedsHit = true };
        var leaveMash = new PlayNote { TimeSec = 141.29m, BmsId = "17", Category = NoteCategory.Masher, NeedsHit = true };
        var chart = new[]
        {
            new MuseDashChart.Placement { TimeSec = 92.7m, BmsId = "1A", Kind = "event" },
            new MuseDashChart.Placement { TimeSec = 134.56m, BmsId = "1E", Kind = "event" },
            new MuseDashChart.Placement { TimeSec = 134.85m, BmsId = "1F", Kind = "event" }
        };
        var boss = BossTimeline.Build(chart, new[] { melee, leaveMash });
        Assert.Equal("close_atk_1_48", boss.At(132.22f - BossTimeline.Close1Hit + 0.2f).Clip);
        var back = boss.At(132.4f);
        Assert.Equal("hurt", back.Clip);
        Assert.True(back.Visible);
        Assert.True(boss.At(133.05f).Visible);
        Assert.Equal("hurt", boss.At(133.05f).Clip);
        Assert.Equal("far_atk_2_start", boss.At(134.7f).Clip);
        Assert.Equal("multi_atk_hurt_end", boss.At(141.5f).Clip);
    }

    [Fact]
    public void RapidFar2_HoldsHoverInsteadOfRestartingOnTheTrack()
    {
        var chart = new[]
        {
            new MuseDashChart.Placement { TimeSec = 28.027m, BmsId = "1A", Kind = "event" },
            new MuseDashChart.Placement { TimeSec = 29.198m, BmsId = "1E", Kind = "event" },
            new MuseDashChart.Placement { TimeSec = 29.490m, BmsId = "1F", Kind = "event" },
            new MuseDashChart.Placement { TimeSec = 29.783m, BmsId = "1E", Kind = "event" },
            new MuseDashChart.Placement { TimeSec = 35.929m, BmsId = "1E", Kind = "event" },
            new MuseDashChart.Placement { TimeSec = 36.905m, BmsId = "1F", Kind = "event" },
            new MuseDashChart.Placement { TimeSec = 39.246m, BmsId = "1E", Kind = "event" }
        };
        var boss = BossTimeline.Build(chart, Array.Empty<PlayNote>());
        Assert.Equal("far_atk_2_end", boss.At(29.6f).Clip);
        var shot = boss.At(30.0f);
        Assert.Equal("far_atk_2_start", shot.Clip);
        Assert.False(shot.Loop);
        Assert.True(shot.Time < 0.3f);
        var at305 = boss.At(35.929f);
        Assert.Equal("far_atk_2_start", at305.Clip);
        Assert.True(at305.Time < 0.05f);
        Assert.Equal("far_atk_2_end", boss.At(37.1f).Clip);
        Assert.Equal("far_atk_2_start", boss.At(39.3f).Clip);
    }

    [Fact]
    public void Map4Entrance_Combo238_ReplaysFarAttack2()
    {
        // chrome_vox map4：1A 28.024 入场，238 连击（31.829）是入场后的一拍 1E。
        var chart = new[]
        {
            new MuseDashChart.Placement { TimeSec = 28.024m, BmsId = "1A", Kind = "event" },
            new MuseDashChart.Placement { TimeSec = 29.195m, BmsId = "1E", Kind = "event" },
            new MuseDashChart.Placement { TimeSec = 29.488m, BmsId = "1F", Kind = "event" },
            new MuseDashChart.Placement { TimeSec = 29.780m, BmsId = "1E", Kind = "event" },
            new MuseDashChart.Placement { TimeSec = 31.537m, BmsId = "1E", Kind = "event" },
            new MuseDashChart.Placement { TimeSec = 31.829m, BmsId = "1E", Kind = "event" },
            new MuseDashChart.Placement { TimeSec = 37.854m, BmsId = "1F", Kind = "event" },
            new MuseDashChart.Placement { TimeSec = 38.561m, BmsId = "1L", Kind = "event" }
        };
        var boss = BossTimeline.Build(chart, Array.Empty<PlayNote>());
        Assert.Equal("in", boss.At(28.2f).Clip);
        Assert.Equal("far_atk_2_start", boss.At(29.4f).Clip);
        Assert.Equal("far_atk_2_end", boss.At(29.55f).Clip);
        var form = boss.At(29.78f);
        Assert.Equal("far_atk_2_start", form.Clip);
        Assert.True(form.Time < 0.05f);
        var beat = boss.At(31.829f);
        Assert.Equal("far_atk_2_start", beat.Clip);
        Assert.True(beat.Time < 0.05f);
        Assert.Equal("far_atk_2_end", boss.At(38.05f).Clip);
        Assert.Equal("far_atk_2_end", boss.At(38.45f).Clip);
        Assert.False(boss.At(38.7f).Visible);
    }

    [Fact]
    public void Danmaku_DrivesFarAttackPose()
    {
        var air = new PlayNote { TimeSec = 8m, BmsId = "13", Category = NoteCategory.Boss, Pathway = 1, ApproachRate = 3, NeedsHit = true };
        var road = new PlayNote { TimeSec = 12m, BmsId = "13", Category = NoteCategory.Boss, Pathway = 0, ApproachRate = 3, NeedsHit = true };
        var p2 = new PlayNote { TimeSec = 16m, BmsId = "14", Category = NoteCategory.Boss, Pathway = 0, ApproachRate = 3, NeedsHit = true };
        var boss = BossTimeline.Build(Array.Empty<MuseDashChart.Placement>(), new[] { air, road, p2 });
        var fire = boss.At(8f - Flight.DurationSec(air) + 0.05f);
        Assert.True(fire.Visible);
        Assert.Equal("far_atk_1_air", fire.Clip);
        Assert.False(fire.CountsHit);
        Assert.Equal("far_atk_1_road", boss.At(12f - 0.1f).Clip);
        Assert.Equal("far_atk_2", boss.At(16f - 0.1f).Clip);
        var airStart = 8f - Flight.DurationSec(air);
        Assert.Equal("far_atk_1_end", boss.At(airStart + BossTimeline.FireSec + 0.1f).Clip);
        Assert.Equal("far_atk_2_end", boss.At(16f - Flight.DurationSec(p2) + BossTimeline.FireSec + 0.1f).Clip);
    }

    [Fact]
    public void Danmaku_DuringFar1_StaysInFarPose()
    {
        // chrome_vox map4：1C 还在，98.12s 不能播 far_atk_1_end 把人按回原地待机。
        var chart = new[]
        {
            new MuseDashChart.Placement { TimeSec = 92.707m, BmsId = "1A", Kind = "event" },
            new MuseDashChart.Placement { TimeSec = 93.707m, BmsId = "1C", Kind = "event" }
        };
        var shot = new PlayNote
        {
            TimeSec = 96.951m,
            BmsId = "13",
            Pathway = 1,
            ApproachRate = 3,
            NeedsHit = true
        };
        var boss = BossTimeline.Build(chart, new[] { shot });
        var tail = (float)shot.TimeSec - Flight.DurationSec(shot) + BossTimeline.FireSec + 0.6f;
        Assert.Equal("far_atk_1_standby", boss.At(tail).Clip);
        Assert.Equal("far_atk_1_air", boss.At((float)shot.TimeSec - Flight.DurationSec(shot) + 0.05f).Clip);
    }

    [Fact]
    public void Map4_SwitchToFar2_DoesNotDropToIdle()
    {
        // 112.37 的 1G 切远程2。112.80 要播过渡，122.16 仍停在远攻2，不要回原地待机。
        var chart = new[]
        {
            new MuseDashChart.Placement { TimeSec = 92.707m, BmsId = "1A", Kind = "event" },
            new MuseDashChart.Placement { TimeSec = 93.707m, BmsId = "1C", Kind = "event" },
            new MuseDashChart.Placement { TimeSec = 112.372m, BmsId = "1G", Kind = "event" }
        };
        var shot = new PlayNote
        {
            TimeSec = 121.390m,
            BmsId = "15",
            Pathway = 1,
            ApproachRate = 3,
            NeedsHit = true
        };
        var boss = BossTimeline.Build(chart, new[] { shot });
        Assert.Equal("atk_1_to_2", boss.At(112.80f).Clip);
        Assert.Equal("far_atk_2_standby", boss.At(122.16f).Clip);
    }

    [Fact]
    public void FarAttackEnd_PlaysReturnClip()
    {
        var chart = new[]
        {
            new MuseDashChart.Placement { TimeSec = 1, BmsId = "1A", Kind = "event" },
            new MuseDashChart.Placement { TimeSec = 3, BmsId = "1C", Kind = "event" },
            new MuseDashChart.Placement { TimeSec = 6, BmsId = "1D", Kind = "event" },
            new MuseDashChart.Placement { TimeSec = 8, BmsId = "1E", Kind = "event" },
            new MuseDashChart.Placement { TimeSec = 10, BmsId = "1F", Kind = "event" }
        };
        var boss = BossTimeline.Build(chart, Array.Empty<PlayNote>());
        Assert.Equal("far_atk_1_end", boss.At(6.2f).Clip);
        Assert.Equal("standby", boss.At(6.9f).Clip);
        Assert.Equal("far_atk_2_end", boss.At(10.2f).Clip);
        Assert.Equal("standby", boss.At(10.9f).Clip);
    }

    [Fact]
    public void Mash_DoesNotCoverMeleeOrShot()
    {
        var melee = new PlayNote { TimeSec = 10m, BmsId = "11", Category = NoteCategory.Boss, NeedsHit = true };
        var mash = new PlayNote { TimeSec = 10m, LengthSec = 0.4m, BmsId = "17", Category = NoteCategory.Masher, NeedsHit = true };
        var shot = new PlayNote { TimeSec = 10m, BmsId = "13", Category = NoteCategory.Boss, Pathway = 1, ApproachRate = 3, NeedsHit = true };
        var bothHit = BossTimeline.Build(Array.Empty<MuseDashChart.Placement>(), new[] { melee, mash });
        Assert.Equal("close_atk_1_48", bothHit.At(10f - BossTimeline.Close1Hit).Clip);
        var mashShot = BossTimeline.Build(Array.Empty<MuseDashChart.Placement>(), new[] { mash, shot });
        Assert.Equal("far_atk_1_air", mashShot.At(10f - Flight.DurationSec(shot) + 0.05f).Clip);
        var mashOnly = BossTimeline.Build(Array.Empty<MuseDashChart.Placement>(), new[] { mash });
        Assert.Equal("standby", mashOnly.At(10f - BossTimeline.Close1Hit).Clip);
        Assert.Equal("multi_atk_hurt", mashOnly.At(10.05f).Clip);
    }

    [Fact]
    public void BossMash_DashHurtThenReturnOrLeave()
    {
        var m16 = new PlayNote { Id = 1, TimeSec = 10m, LengthSec = 0.5m, BmsId = "16", Category = NoteCategory.Masher, NeedsHit = true };
        var m17 = new PlayNote { Id = 2, TimeSec = 20m, LengthSec = 0.3m, BmsId = "17", Category = NoteCategory.Masher, NeedsHit = true };
        var boss = BossTimeline.Build(Array.Empty<MuseDashChart.Placement>(), new[] { m16, m17 });
        Assert.Equal("multi_atk_48", boss.At(9.5f).Clip);
        var hurt = boss.At(10.2f);
        Assert.Equal("multi_atk_hurt", hurt.Clip);
        Assert.Equal(1, hurt.MashId);
        Assert.Equal("multi_atk_out", boss.At(10.8f).Clip);
        Assert.Equal("multi_atk_hurt_end", boss.At(20.5f).Clip);
        Assert.False(boss.At(20.3f + BossTimeline.HurtEndSec + 0.05f).Visible);
        Assert.True(BossTimeline.BodyIsTarget("11"));
        Assert.True(BossTimeline.BodyIsTarget("17"));
        Assert.False(BossTimeline.BodyIsTarget("13"));
    }

    [Fact]
    public void HideCluster_AfterFarAttack_PlaysOutAndStaysGone()
    {
        var chart = new[]
        {
            new MuseDashChart.Placement { TimeSec = 28.024m, BmsId = "1A", Kind = "event" },
            new MuseDashChart.Placement { TimeSec = 29.195m, BmsId = "1E", Kind = "event" },
            new MuseDashChart.Placement { TimeSec = 37.854m, BmsId = "1F", Kind = "event" },
            new MuseDashChart.Placement { TimeSec = 38.561m, BmsId = "1L", Kind = "event" },
            new MuseDashChart.Placement { TimeSec = 38.707m, BmsId = "1A", Kind = "event" },
            new MuseDashChart.Placement { TimeSec = 38.854m, BmsId = "1B", Kind = "event" },
            new MuseDashChart.Placement { TimeSec = 40.317m, BmsId = "1M", Kind = "event" },
            new MuseDashChart.Placement { TimeSec = 92.707m, BmsId = "1A", Kind = "event" }
        };
        var boss = BossTimeline.Build(chart, Array.Empty<PlayNote>());
        Assert.Equal("far_atk_2_end", boss.At(38.05f).Clip);
        Assert.Equal("far_atk_2_end", boss.At(38.45f).Clip);
        Assert.False(boss.At(38.7f).Visible);
        Assert.False(boss.At(41f).Visible);
        var back = boss.At(92.8f);
        Assert.True(back.Visible);
        Assert.Equal("in", back.Clip);
    }

    [Fact]
    public void Map4_Mash17_PlaysThroughFarEndAndHide()
    {
        // 官谱 37.976 是 17 连击后退场_48，压在 1F 收尾和 1L 消失中间。
        var chart = new[]
        {
            new MuseDashChart.Placement { TimeSec = 28.024m, BmsId = "1A", Kind = "event" },
            new MuseDashChart.Placement { TimeSec = 29.195m, BmsId = "1E", Kind = "event" },
            new MuseDashChart.Placement { TimeSec = 37.854m, BmsId = "1F", Kind = "event" },
            new MuseDashChart.Placement { TimeSec = 38.561m, BmsId = "1L", Kind = "event" },
            new MuseDashChart.Placement { TimeSec = 38.707m, BmsId = "1A", Kind = "event" },
            new MuseDashChart.Placement { TimeSec = 38.854m, BmsId = "1B", Kind = "event" },
            new MuseDashChart.Placement { TimeSec = 40.317m, BmsId = "1M", Kind = "event" },
            new MuseDashChart.Placement { TimeSec = 92.707m, BmsId = "1A", Kind = "event" }
        };
        var mash = new PlayNote
        {
            Id = 328,
            TimeSec = 37.97562m,
            LengthSec = 0.29269m,
            BmsId = "17",
            Category = NoteCategory.Masher,
            Pathway = 0,
            NeedsHit = true
        };
        var boss = BossTimeline.Build(chart, new[] { mash });
        Assert.Equal("far_atk_2_standby", boss.At(37.0f).Clip);
        Assert.Equal("multi_atk_hurt", boss.At(38.05f).Clip);
        Assert.Equal("multi_atk_hurt_end", boss.At(38.45f).Clip);
        Assert.Equal("multi_atk_hurt_end", boss.At(38.7f).Clip);
        var leave = boss.At(38.561f);
        Assert.Equal("multi_atk_hurt_end", leave.Clip);
        Assert.InRange(leave.Time, 0.2f, 0.45f);
        Assert.False(boss.At(40.2f).Visible);
        Assert.False(boss.At(41f).Visible);
        var back = boss.At(92.8f);
        Assert.True(back.Visible);
        Assert.Equal("in", back.Clip);
    }

    [Fact]
    public void HideAndShow_FromScript()
    {
        var chart = new[]
        {
            new MuseDashChart.Placement { TimeSec = 1, BmsId = "1A", Kind = "event" },
            new MuseDashChart.Placement { TimeSec = 5, BmsId = "1L", Kind = "event" },
            new MuseDashChart.Placement { TimeSec = 7, BmsId = "1M", Kind = "event" }
        };
        var boss = BossTimeline.Build(chart, Array.Empty<PlayNote>());
        Assert.Equal("out", boss.At(5.1f).Clip);
        Assert.False(boss.At(5f + BossTimeline.OutSec + 0.05f).Visible);
        var back = boss.At(7.1f);
        Assert.True(back.Visible);
        Assert.Equal("standby", back.Clip);
        Assert.False(back.CountsHit);
    }

    [Fact]
    public void Events_AreNotPlayableNotes()
    {
        var doc = new MuseDashChart.ChartDocument();
        doc.Charts[1] = new MuseDashChart.DifficultyChart
        {
            Notes =
            {
                new MuseDashChart.Placement { Id = 1, TimeSec = 1, BmsId = "1A", Kind = "event" },
                new MuseDashChart.Placement { Id = 2, TimeSec = 2, BmsId = "13", Pathway = 1, Kind = "note" },
                new MuseDashChart.Placement { Id = 3, TimeSec = 3, BmsId = "11", Pathway = 0, Kind = "note" }
            }
        };
        var s = new PlaySession(new PlaySong(doc, ".", null), 1);
        Assert.DoesNotContain(s.Notes, n => n.BmsId == "1A");
        Assert.Contains(s.Notes, n => n.BmsId == "13" && n.NeedsHit);
        Assert.Contains(s.Notes, n => n.BmsId == "11" && n.NeedsHit);
        Assert.True(s.Boss.At(2.1f).Visible);
        Assert.False(s.Boss.At(2.1f).CountsHit);
    }

    [Fact]
    public void Attacks_DoNotSummonBody_BeforeEnterOrWhileHidden()
    {
        var shot = new PlayNote { TimeSec = 2m, BmsId = "13", Pathway = 1, ApproachRate = 3, NeedsHit = true };
        var melee = new PlayNote { TimeSec = 6m, BmsId = "11", NeedsHit = true };
        var far = new MuseDashChart.Placement { TimeSec = 5.2m, BmsId = "1E", Kind = "event" };
        var chart = new[]
        {
            new MuseDashChart.Placement { TimeSec = 4, BmsId = "1A", Kind = "event" },
            new MuseDashChart.Placement { TimeSec = 5, BmsId = "1L", Kind = "event" },
            far,
            new MuseDashChart.Placement { TimeSec = 8, BmsId = "1M", Kind = "event" }
        };
        var boss = BossTimeline.Build(chart, new[] { shot, melee });
        Assert.False(boss.At(2f).Visible);
        Assert.Equal("out", boss.At(5.1f).Clip);
        Assert.False(boss.At(5f + BossTimeline.OutSec + 0.05f).Visible);
        Assert.True(boss.At(8.2f).Visible);
    }

    [Fact]
    public void Body_SitsOnCenterLane()
    {
        Assert.InRange(BossTimeline.BodyX, 3.8f, 4.5f);
        Assert.True(BossTimeline.BodyXForAspect(1.3f) < BossTimeline.BodyX);
        Assert.Equal(Flight.MidY, BossTimeline.BodyY);
    }
}

public class BgTimelineTests
{
    [Fact]
    public void HideThenShow_AndScrollPair()
    {
        var notes = new[]
        {
            new MuseDashChart.Placement { Id = 1, TimeSec = 1, BmsId = "25", Kind = "event" },
            new MuseDashChart.Placement { Id = 2, TimeSec = 2, BmsId = "27", Kind = "event" },
            new MuseDashChart.Placement { Id = 3, TimeSec = 3, BmsId = "29", Kind = "event" },
            new MuseDashChart.Placement { Id = 4, TimeSec = 4, BmsId = "26", Kind = "event" },
            new MuseDashChart.Placement { Id = 5, TimeSec = 5, BmsId = "2N", Kind = "event" },
            new MuseDashChart.Placement { Id = 6, TimeSec = 6, BmsId = "2O", Kind = "event" }
        };
        var steps = BgTimeline.Build(notes);
        Assert.Equal(6, steps.Count);
        var a = BgTimeline.At(steps, 1.5f);
        Assert.True(a.Hidden);
        Assert.Equal(0, a.Scroll);
        var b = BgTimeline.At(steps, 2.5f);
        Assert.True(b.Hidden);
        Assert.Equal(1, b.Scroll);
        var c = BgTimeline.At(steps, 4.5f);
        Assert.False(c.Hidden);
        Assert.Equal(0, c.Scroll);
        var d = BgTimeline.At(steps, 5.5f);
        Assert.True(d.Frozen);
        Assert.False(BgTimeline.At(steps, 6.5f).Frozen);
    }
}

public class FxTimelineTests
{
    [Fact]
    public void Flash_InterpolatesBetweenOfficialKeyframes()
    {
        var notes = new[]
        {
            new MuseDashChart.Placement { TimeSec = 1m, BmsId = "2I", Kind = "event" },
            new MuseDashChart.Placement { TimeSec = 2m, BmsId = "2J", Kind = "event" },
            new MuseDashChart.Placement { TimeSec = 4m, BmsId = "2K", Kind = "event" }
        };
        var steps = FxTimeline.Build(notes);
        Assert.Equal(0f, FxTimeline.At(steps, 1f).Flash, 4);
        Assert.Equal(0.5f, FxTimeline.At(steps, 1.5f).Flash, 4);
        Assert.Equal(1f, FxTimeline.At(steps, 2f).Flash, 4);
        Assert.Equal(0.5f, FxTimeline.At(steps, 3f).Flash, 4);
        Assert.Equal(0f, FxTimeline.At(steps, 4f).Flash, 4);
    }

    [Fact]
    public void VignetteAndSpeedLine_FollowOpenClose()
    {
        var notes = new[]
        {
            new MuseDashChart.Placement { TimeSec = 104.286m, BmsId = "2E", Kind = "event" },
            new MuseDashChart.Placement { TimeSec = 104.643m, BmsId = "2F", Kind = "event" },
            new MuseDashChart.Placement { TimeSec = 115.714m, BmsId = "2T", Kind = "event" },
            new MuseDashChart.Placement { TimeSec = 125.714m, BmsId = "2V", Kind = "event" }
        };
        var steps = FxTimeline.Build(notes);
        Assert.True(FxTimeline.At(steps, 104.4f).Vignette);
        Assert.False(FxTimeline.At(steps, 104.8f).Vignette);
        Assert.Equal(1, FxTimeline.At(steps, 116f).SpeedLine);
        Assert.Equal(0, FxTimeline.At(steps, 126f).SpeedLine);
    }

    [Fact]
    public void PixelAndFilm_FollowOpenClose_Lane2XStaysFilm()
    {
        var notes = new[]
        {
            new MuseDashChart.Placement { TimeSec = 1, BmsId = "2P", Kind = "event", Channel = "15" },
            new MuseDashChart.Placement { TimeSec = 2, BmsId = "2Q", Kind = "event", Channel = "15" },
            new MuseDashChart.Placement { TimeSec = 3, BmsId = "2W", Kind = "event", Channel = "15" },
            new MuseDashChart.Placement { TimeSec = 4, BmsId = "2X", Kind = "event", Channel = "14" },
            new MuseDashChart.Placement { TimeSec = 5, BmsId = "2X", Kind = "event", Channel = "15" }
        };
        var steps = FxTimeline.Build(notes);
        Assert.True(FxTimeline.At(steps, 1.5f).Pixel);
        Assert.False(FxTimeline.At(steps, 2.5f).Pixel);
        Assert.True(FxTimeline.At(steps, 3.5f).Film);
        Assert.True(FxTimeline.At(steps, 4.5f).Film);
        Assert.False(FxTimeline.At(steps, 5.5f).Film);
    }

    [Fact]
    public void HideNotes_BlocksHitUntilOneK()
    {
        var doc = new MuseDashChart.ChartDocument();
        doc.Charts[1] = new MuseDashChart.DifficultyChart
        {
            Notes =
            {
                new MuseDashChart.Placement { TimeSec = 0.5m, BmsId = "1J", Kind = "event" },
                new MuseDashChart.Placement { TimeSec = 2, BmsId = "1K", Kind = "event" },
                new MuseDashChart.Placement { Id = 1, TimeSec = 2.2m, BmsId = "01", Pathway = 0, Kind = "note" }
            }
        };
        var s = new PlaySession(new PlaySong(doc, ".", null), 1);
        Assert.True(s.FxAt(1f).NotesHidden);
        Assert.Empty(s.Visible(1f));
        Assert.Equal(Judge.Grade.None, s.HitLane(false, 1.01f));
        Assert.False(s.FxAt(2.1f).NotesHidden);
        Assert.Equal(Judge.Grade.Perfect, s.HitLane(false, 2.21f));
    }
}

public class JudgeTests
{
    [Fact]
    public void Gear_Dodge_IsPassNotMiss()
    {
        var doc = new MuseDashChart.ChartDocument();
        doc.Charts[1] = new MuseDashChart.DifficultyChart
        {
            Notes =
            {
                new MuseDashChart.Placement { Id = 1, TimeSec = 1, BmsId = "0H", Pathway = 0, Kind = "note" }
            }
        };
        var s = new PlaySession(new PlaySong(doc, ".", null), 1);
        s.TickMisses(1.01f);
        Assert.Equal(Judge.Grade.Pass, s.GradeOf(1));
        Assert.Equal(0, s.Misses);
        Assert.Equal(0, s.Combo);
        var hits = new List<PlaySession.JudgeHit>();
        s.TakeHits(hits);
        Assert.Single(hits);
        Assert.Equal(Judge.Grade.Pass, hits[0].Grade);
        s.Autoplay(1.01f);
        var again = new List<PlaySession.JudgeHit>();
        s.TakeHits(again);
        Assert.Empty(again);
    }

    [Fact]
    public void PerfectThenLateMiss()
    {
        var n = new PlayNote { TimeSec = 1m, BmsId = "01", NeedsHit = true, CountsCombo = true };
        Assert.Equal(Judge.Grade.Perfect, Judge.Tap(n, 1.02f));
        Assert.True(Judge.IsLateMiss(n, 1.2f));
    }

    [Fact]
    public void ManualGear_UsesPlayerLane_WithoutAddingMiss()
    {
        static PlaySession Session()
        {
            var doc = new MuseDashChart.ChartDocument();
            doc.Charts[1] = new MuseDashChart.DifficultyChart
            {
                Notes =
                {
                    new MuseDashChart.Placement { Id = 1, TimeSec = 1, BmsId = "0H", Pathway = 0, Kind = "note" }
                }
            };
            return new PlaySession(new PlaySong(doc, ".", null), 1);
        }

        var collide = Session();
        collide.TickManual(1.01f, playerAir: false, activeHoldMask: 0);
        Assert.Equal(Judge.Grade.Miss, collide.GradeOf(1));
        Assert.Equal(0, collide.Misses);

        var dodge = Session();
        dodge.TickManual(1.01f, playerAir: true, activeHoldMask: 0);
        Assert.Equal(Judge.Grade.Pass, dodge.GradeOf(1));
        Assert.Equal(0, dodge.Misses);
    }

    [Fact]
    public void ManualGear_AfterJudgeFrameCannotDealDelayedDamage()
    {
        var doc = new MuseDashChart.ChartDocument();
        doc.Charts[1] = new MuseDashChart.DifficultyChart
        {
            Notes =
            {
                new MuseDashChart.Placement
                {
                    Id = 1, TimeSec = 1, BmsId = "0H", Pathway = 0, Kind = "note"
                }
            }
        };
        var s = new PlaySession(new PlaySong(doc, ".", null), 1);

        // 模拟判定帧未运行：齿轮已经越圈后，即使角色回到同轨也只能 PASS。
        s.TickManual(1f + Flight.GearJudgeLateSec + 0.01f,
            playerAir: false, activeHoldMask: 0);
        Assert.Equal(Judge.Grade.Pass, s.GradeOf(1));
        Assert.Equal(0, s.Misses);
    }

    [Fact]
    public void CollidedGear_RemainsVisibleForFullDestroyClipAtFastRate()
    {
        var doc = new MuseDashChart.ChartDocument();
        doc.Charts[1] = new MuseDashChart.DifficultyChart
        {
            Notes =
            {
                new MuseDashChart.Placement
                {
                    Id = 1, TimeSec = 1, BmsId = "0H", Pathway = 0,
                    ApproachRate = 3, Kind = "note"
                }
            }
        };
        var s = new PlaySession(new PlaySong(doc, ".", null), 1);
        s.TickManual(1f, playerAir: false, activeHoldMask: 0);

        Assert.Contains(s.Visible(1f + Flight.GearDestroySec - 0.01f), n => n.Id == 1);
        Assert.DoesNotContain(s.Visible(1f + Flight.GearDestroySec + 0.01f), n => n.Id == 1);
    }

    [Fact]
    public void ManualMusic_IsCollectedByTouchingLane()
    {
        var doc = new MuseDashChart.ChartDocument();
        doc.Charts[1] = new MuseDashChart.DifficultyChart
        {
            Notes =
            {
                new MuseDashChart.Placement { Id = 1, TimeSec = 1, BmsId = "23", Pathway = 1, Kind = "note" },
                new MuseDashChart.Placement { Id = 2, TimeSec = 2, BmsId = "23", Pathway = 0, Kind = "note" }
            }
        };
        var s = new PlaySession(new PlaySong(doc, ".", null), 1);
        s.TickManual(1.01f, playerAir: true, activeHoldMask: 0);
        Assert.Equal(Judge.Grade.Perfect, s.GradeOf(1));
        Assert.Equal(0, s.Combo);

        // 地面 Hold 已占据地轨时，即使主角色姿态采样为上轨也会接到条内音符。
        s.TickManual(2.01f, playerAir: true, activeHoldMask: 1);
        Assert.Equal(Judge.Grade.Perfect, s.GradeOf(2));
    }

    [Fact]
    public void ManualHoldStar24_RequiresIndependentKeyDuringHold()
    {
        var doc = new MuseDashChart.ChartDocument();
        doc.Charts[1] = new MuseDashChart.DifficultyChart
        {
            Notes =
            {
                new MuseDashChart.Placement { Id = 1, TimeSec = 1, BmsId = "24", Pathway = 0, Kind = "note" }
            }
        };
        var s = new PlaySession(new PlaySong(doc, ".", null), 1);

        // 已按住同轨 Hold 只代表角色在该轨，不能替代 24 的第二次按键。
        s.TickManual(1.01f, playerAir: false, activeHoldMask: 1);
        Assert.Equal(Judge.Grade.None, s.GradeOf(1));
        Assert.Equal(0, s.Combo);

        Assert.Equal(Judge.Grade.Perfect, s.HitLane(false, 1.01f));
        Assert.Equal(Judge.Grade.Perfect, s.GradeOf(1));
        Assert.Equal(1, s.Combo);
    }

    [Fact]
    public void ManualHold_ReleaseBreaksAfterGraceButAllowsLaneFingerSwap()
    {
        static PlaySession Session()
        {
            var doc = new MuseDashChart.ChartDocument();
            doc.Charts[1] = new MuseDashChart.DifficultyChart
            {
                Notes =
                {
                    new MuseDashChart.Placement
                    {
                        Id = 1, TimeSec = 1, LengthSec = 1m,
                        BmsId = "0F", Pathway = 0, Kind = "note"
                    }
                }
            };
            return new PlaySession(new PlaySong(doc, ".", null), 1);
        }

        var released = Session();
        Assert.Equal(Judge.Grade.Perfect, released.HitLane(false, 1f));
        released.TickManual(1.02f, playerAir: false, activeHoldMask: 1, heldLaneMask: 0);
        Assert.Equal(Judge.Grade.Perfect, released.GradeOf(1));
        released.TickManual(1.02f + PlaySession.HoldReleaseGrace + 0.01f,
            playerAir: false, activeHoldMask: 1, heldLaneMask: 0);
        Assert.Equal(Judge.Grade.Miss, released.GradeOf(1));

        var swapped = Session();
        Assert.Equal(Judge.Grade.Perfect, swapped.HitLane(false, 1f));
        swapped.TickManual(1.02f, playerAir: false, activeHoldMask: 1, heldLaneMask: 0);
        swapped.TickManual(1.02f + PlaySession.HoldReleaseGrace * 0.5f,
            playerAir: false, activeHoldMask: 1, heldLaneMask: 1);
        swapped.TickManual(2f, playerAir: false, activeHoldMask: 1, heldLaneMask: 1);
        Assert.Equal(Judge.Grade.Perfect, swapped.GradeOf(1));
        Assert.Equal(2, swapped.Combo);
        Assert.Equal(0, swapped.Misses);
    }

    [Fact]
    public void ManualInput_PrioritizesEnemyOverStackedMusic()
    {
        var doc = new MuseDashChart.ChartDocument();
        doc.Charts[1] = new MuseDashChart.DifficultyChart
        {
            Notes =
            {
                new MuseDashChart.Placement { Id = 1, TimeSec = 1, BmsId = "23", Pathway = 0, Kind = "note" },
                new MuseDashChart.Placement { Id = 2, TimeSec = 1, BmsId = "01", Pathway = 0, Kind = "note" }
            }
        };
        var s = new PlaySession(new PlaySong(doc, ".", null), 1);
        Assert.Equal(Judge.Grade.Perfect, s.HitLane(false, 1f));
        Assert.Equal(Judge.Grade.None, s.GradeOf(1));
        Assert.Equal(Judge.Grade.Perfect, s.GradeOf(2));
        s.TickManual(1f, playerAir: false, activeHoldMask: 0);
        Assert.Equal(Judge.Grade.Perfect, s.GradeOf(1));
    }

    [Fact]
    public void ManualInput_PassiveMusicNeverStealsNearbyEnemyInput()
    {
        var doc = new MuseDashChart.ChartDocument();
        doc.Charts[1] = new MuseDashChart.DifficultyChart
        {
            Notes =
            {
                new MuseDashChart.Placement { Id = 1, TimeSec = 1, BmsId = "23", Pathway = 0, Kind = "note" },
                new MuseDashChart.Placement { Id = 2, TimeSec = 1.04m, BmsId = "01", Pathway = 0, Kind = "note" }
            }
        };
        var s = new PlaySession(new PlaySong(doc, ".", null), 1);
        Assert.Equal(Judge.Grade.Perfect, s.HitLane(false, 1f));
        Assert.Equal(Judge.Grade.None, s.GradeOf(1));
        Assert.Equal(Judge.Grade.Perfect, s.GradeOf(2));
        s.TickManual(1f, playerAir: false, activeHoldMask: 0);
        Assert.Equal(Judge.Grade.Perfect, s.GradeOf(1));
    }

    [Fact]
    public void ManualInput_RapidSameLaneStreamConsumesOldestNoteFirst()
    {
        var doc = new MuseDashChart.ChartDocument();
        doc.Charts[1] = new MuseDashChart.DifficultyChart
        {
            Notes =
            {
                new MuseDashChart.Placement { Id = 1, TimeSec = 1m, BmsId = "01", Pathway = 0, Kind = "note" },
                new MuseDashChart.Placement { Id = 2, TimeSec = 1.06m, BmsId = "01", Pathway = 0, Kind = "note" },
                new MuseDashChart.Placement { Id = 3, TimeSec = 1.12m, BmsId = "01", Pathway = 0, Kind = "note" }
            }
        };
        var s = new PlaySession(new PlaySong(doc, ".", null), 1);

        // 1.055 更接近第二颗，但滚键必须先消费尚未超窗的第一颗。
        Assert.Equal(Judge.Grade.Great, s.HitLane(false, 1.055f));
        Assert.Equal(Judge.Grade.Great, s.GradeOf(1));
        Assert.Equal(Judge.Grade.None, s.GradeOf(2));
        Assert.Equal(Judge.Grade.Perfect, s.HitLane(false, 1.065f));
        Assert.Equal(Judge.Grade.Perfect, s.HitLane(false, 1.12f));
        Assert.Equal(3, s.Combo);
    }
}

public class SessionTests
{
    static PlaySession Make()
    {
        var doc = new MuseDashChart.ChartDocument();
        doc.Meta.Name = "t";
        doc.Charts[1] = new MuseDashChart.DifficultyChart
        {
            Notes =
            {
                new MuseDashChart.Placement { Id = 1, TimeSec = 1, BmsId = "01", Pathway = 0, Kind = "note" },
                new MuseDashChart.Placement { Id = 2, TimeSec = 2, BmsId = "01", Pathway = 1, Kind = "note" }
            }
        };
        return new PlaySession(new PlaySong(doc, ".", null), 1);
    }

    [Fact]
    public void HitRoad_Perfect_IncrementsCombo()
    {
        var s = Make();
        Assert.Equal(Judge.Grade.Perfect, s.HitLane(false, 1.01f));
        Assert.Equal(1, s.Combo);
        Assert.Equal(1, s.Perfects);
        var hits = new List<PlaySession.JudgeHit>();
        s.TakeHits(hits);
        Assert.Single(hits);
        Assert.Equal(Judge.Grade.Perfect, hits[0].Grade);
    }

    [Fact]
    public void Autoplay_KeepsNoteWhileOutPlays()
    {
        var s = Make();
        s.Autoplay(1.01f);
        Assert.Equal(Judge.Grade.Perfect, s.GradeOf(1));
        Assert.Contains(s.Visible(1.2f), n => n.Id == 1);
        Assert.DoesNotContain(s.Visible(1f + Flight.OutKeepSec + 0.05f), n => n.Id == 1);
    }

    [Fact]
    public void LateMiss_ResetsCombo()
    {
        var s = Make();
        s.HitLane(false, 1f);
        s.TickMisses(2.2f);
        Assert.Equal(0, s.Combo);
        Assert.Equal(1, s.Misses);
        Assert.Equal(1, s.Perfects);
    }

    [Fact]
    public void HoldStar_Autoplay_CountsComboLikeHoldHead()
    {
        var doc = new MuseDashChart.ChartDocument();
        doc.Charts[1] = new MuseDashChart.DifficultyChart
        {
            Notes =
            {
                new MuseDashChart.Placement { Id = 1, TimeSec = 1, BmsId = "24", Pathway = 1, Kind = "note" }
            }
        };
        var s = new PlaySession(new PlaySong(doc, ".", null), 1);
        Assert.True(s.Notes[0].CountsCombo);
        Assert.True(s.Notes[0].NeedsHit);
        s.Autoplay(1.01f);
        Assert.Equal(1, s.Combo);
        Assert.Equal(1, s.Perfects);
    }

    [Fact]
    public void Hold_Autoplay_CountsHeadAndTail()
    {
        var doc = new MuseDashChart.ChartDocument();
        doc.Charts[1] = new MuseDashChart.DifficultyChart
        {
            Notes =
            {
                new MuseDashChart.Placement { Id = 1, TimeSec = 1, LengthSec = 0.5m, BmsId = "0F", Pathway = 0, Kind = "note" }
            }
        };
        var s = new PlaySession(new PlaySong(doc, ".", null), 1);
        s.Autoplay(1.01f);
        Assert.Equal(1, s.Combo);
        s.Autoplay(1.5f);
        Assert.Equal(2, s.Combo);
        Assert.Equal(2, s.Perfects);
    }

    [Fact]
    public void Masher_Autoplay_CountsTicksAndKeepsForOut()
    {
        var doc = new MuseDashChart.ChartDocument();
        doc.Charts[1] = new MuseDashChart.DifficultyChart
        {
            Notes =
            {
                new MuseDashChart.Placement { Id = 1, TimeSec = 1, LengthSec = 0.4m, BmsId = "0G", Pathway = 0, Kind = "note" }
            }
        };
        var s = new PlaySession(new PlaySong(doc, ".", null), 1);
        s.Autoplay(1.01f);
        Assert.Equal(Judge.Grade.Perfect, s.GradeOf(1));
        Assert.True(s.MashHits(1) >= 1);
        s.Autoplay(1.25f);
        Assert.True(s.MashHits(1) >= 4);
        var hits = new List<PlaySession.JudgeHit>();
        s.TakeHits(hits);
        Assert.Equal(Enumerable.Range(1, hits.Count), hits.Select(h => h.MashIndex));
        s.Autoplay(1.5f);
        Assert.Contains(s.Visible(1.5f), n => n.Id == 1);
        Assert.DoesNotContain(s.Visible(1.4f + Flight.OutKeepSec + 0.05f), n => n.Id == 1);
    }

    [Fact]
    public void Masher_Miss_FliesPastHitLine()
    {
        var doc = new MuseDashChart.ChartDocument();
        doc.Charts[1] = new MuseDashChart.DifficultyChart
        {
            Notes =
            {
                new MuseDashChart.Placement { Id = 1, TimeSec = 1, LengthSec = 0.4m, BmsId = "0G", Pathway = 0, Kind = "note" }
            }
        };
        var s = new PlaySession(new PlaySong(doc, ".", null), 1);
        s.TickMisses(1.2f);
        Assert.Equal(Judge.Grade.Miss, s.GradeOf(1));
        Assert.Contains(s.Visible(1.3f), n => n.Id == 1);
        Assert.DoesNotContain(s.Visible(2.5f), n => n.Id == 1);
        var n = s.Notes[0];
        Assert.True(Flight.TryExitPose(n, 1.3f, out var pose));
        Assert.True(pose.X < Flight.HitX);
    }

    [Fact]
    public void Masher_BindsX04()
    {
        var n = new PlayNote { BmsId = "0G", Category = NoteCategory.Masher, Pathway = 0 };
        var boss = new PlayNote { BmsId = "16", Category = NoteCategory.Masher, Pathway = 0 };
        Assert.Equal("04", AnimBinding.Of(n)!.Value.Code);
        Assert.Equal("mash", AnimBinding.Of(n)!.Value.Group);
        Assert.Equal("in_down_27", AnimBinding.InClip(AnimBinding.Of(n)!.Value, 1));
        Assert.Equal("in_down_20", AnimBinding.InClip(AnimBinding.Of(n)!.Value, 3));
        Assert.Equal(27, Flight.HitFrames(n));
        Assert.Equal(20, Flight.HitFrames(new PlayNote { BmsId = "0G", Category = NoteCategory.Masher, ApproachRate = 3 }));
        Assert.Null(AnimBinding.Of(boss));
    }

    [Fact]
    public void Masher_EitherLaneHits()
    {
        var doc = new MuseDashChart.ChartDocument();
        doc.Charts[1] = new MuseDashChart.DifficultyChart
        {
            Notes =
            {
                new MuseDashChart.Placement { Id = 1, TimeSec = 1, LengthSec = 0.4m, BmsId = "0G", Pathway = 0, Kind = "note" }
            }
        };
        var s = new PlaySession(new PlaySong(doc, ".", null), 1);
        Assert.Equal(Judge.Grade.Perfect, s.HitLane(true, 1.01f));
        Assert.Equal(1, s.MashHits(1));
        Assert.Equal(Judge.Grade.Perfect, s.HitLane(false, 1.09f));
        Assert.Equal(2, s.MashHits(1));
    }

    [Fact]
    public void Melee_EitherLaneHits()
    {
        var doc = new MuseDashChart.ChartDocument();
        doc.Charts[1] = new MuseDashChart.DifficultyChart
        {
            Notes =
            {
                new MuseDashChart.Placement { Id = 1, TimeSec = 1, BmsId = "11", Pathway = 0, Kind = "note" }
            }
        };
        var s = new PlaySession(new PlaySong(doc, ".", null), 1);
        Assert.Equal(Judge.Grade.Perfect, s.HitLane(true, 1.01f));
    }

    [Fact]
    public void OneKey_FoolEggs_UseScene11RoadPacks()
    {
        Assert.True(SceneTimeline.IsOneKey("scene_11"));
        Assert.False(SceneTimeline.IsOneKey("scene_01"));
        var a = PlayNote.From(new MuseDashChart.Placement { BmsId = "40", Pathway = 1, Kind = "note" });
        var b = PlayNote.From(new MuseDashChart.Placement { BmsId = "41", Pathway = 0, Kind = "note" });
        var c = PlayNote.From(new MuseDashChart.Placement { BmsId = "42", Pathway = 0, Kind = "note" });
        Assert.Equal(0, a.Pathway);
        Assert.Equal(NoteCategory.Monster, a.Category);
        Assert.Equal("10", AnimBinding.Of(a)!.Value.Code);
        Assert.Equal("11", AnimBinding.Of(b)!.Value.Code);
        Assert.Equal("12", AnimBinding.Of(c)!.Value.Code);
        Assert.Equal("1110_road", AnimBinding.SkeletonKey("scene_11", AnimBinding.Of(a)!.Value, a));
        Assert.Equal("1111_road", AnimBinding.SkeletonKey("scene_11", AnimBinding.Of(b)!.Value, b));
        Assert.Equal("1112_road", AnimBinding.SkeletonKey("scene_11", AnimBinding.Of(c)!.Value, c));
        Assert.Equal(Flight.RoadY, Flight.LaneY(a));
    }

    [Fact]
    public void OneKey_AnyLaneKey_HitsGroundEgg()
    {
        var doc = new MuseDashChart.ChartDocument();
        doc.Meta.Scene = "scene_11";
        doc.Charts[1] = new MuseDashChart.DifficultyChart
        {
            Notes =
            {
                new MuseDashChart.Placement { Id = 1, TimeSec = 1, BmsId = "40", Pathway = 0, Kind = "note" }
            }
        };
        var s = new PlaySession(new PlaySong(doc, ".", null), 1);
        Assert.Equal("scene_11", s.SceneAt(1f));
        Assert.Equal(Judge.Grade.Perfect, s.HitLane(true, 1.01f));
    }

    [Fact]
    public void SceneJudge_IndependentOfNoteId()
    {
        Assert.True(SceneTimeline.RulesFor("scene_11").OneKey);
        Assert.True(SceneTimeline.RulesFor("scene_01").DualRings);

        var dual = new MuseDashChart.ChartDocument();
        dual.Meta.Scene = "scene_01";
        dual.Charts[1] = new MuseDashChart.DifficultyChart
        {
            Notes =
            {
                new MuseDashChart.Placement { Id = 1, TimeSec = 1, BmsId = "40", Pathway = 0, Kind = "note" }
            }
        };
        var a = new PlaySession(new PlaySong(dual, ".", null), 1);
        Assert.Equal(Judge.Grade.None, a.HitLane(true, 1.01f));
        Assert.Equal(Judge.Grade.Perfect, a.HitLane(false, 1.01f));

        var one = new MuseDashChart.ChartDocument();
        one.Meta.Scene = "scene_11";
        one.Charts[1] = new MuseDashChart.DifficultyChart
        {
            Notes =
            {
                new MuseDashChart.Placement { Id = 1, TimeSec = 1, BmsId = "01", Pathway = 0, Kind = "note" }
            }
        };
        var b = new PlaySession(new PlaySong(one, ".", null), 1);
        Assert.Equal(Judge.Grade.Perfect, b.HitLane(true, 1.01f));
    }

    [Fact]
    public void SceneJudge_FollowsOneOChange()
    {
        var doc = new MuseDashChart.ChartDocument();
        doc.Meta.Scene = "scene_11";
        doc.Charts[1] = new MuseDashChart.DifficultyChart
        {
            Notes =
            {
                new MuseDashChart.Placement { TimeSec = 0.5m, BmsId = "1O", Kind = "event" },
                new MuseDashChart.Placement { Id = 1, TimeSec = 1, BmsId = "40", Pathway = 0, Kind = "note" }
            }
        };
        var s = new PlaySession(new PlaySong(doc, ".", null), 1);
        Assert.Equal("scene_11", s.SceneAt(0.4f));
        Assert.Equal("scene_01", s.SceneAt(1f));
        Assert.False(SceneTimeline.RulesFor(s.SceneAt(1f)).OneKey);
        Assert.Equal(Judge.Grade.None, s.HitLane(true, 1.01f));
        Assert.Equal(Judge.Grade.Perfect, s.HitLane(false, 1.01f));
    }
}

public class ChangeTheGameMap4Tests
{
    static string Map4Path()
    {
        var d = AppContext.BaseDirectory;
        for (var i = 0; i < 10; i++)
        {
            var p = Path.Combine(d, "导出", "change_the_game", "map4.bms");
            if (File.Exists(p)) return p;
            p = Path.Combine(d, "MusePSona", "导出", "change_the_game", "map4.bms");
            if (File.Exists(p)) return p;
            var parent = Directory.GetParent(d);
            if (parent == null) break;
            d = parent.FullName;
        }
        return "";
    }

    [Fact]
    public void Map4_52s_HasFlashHideBg_AndFar1()
    {
        var path = Map4Path();
        if (path.Length == 0) return;
        DifficultyChart chart;
        using (var fs = File.OpenRead(path))
            chart = BmsReader.ToDifficulty(BmsReader.Load(fs));
        var fx = FxTimeline.At(FxTimeline.Build(chart.Notes), 51.97f);
        // 官机从 2J 峰值连续衰减到 2K；此时已接近尾部，不应仍保持满白。
        Assert.InRange(fx.Flash, 0.05f, 0.3f);
        Assert.True(BgTimeline.At(BgTimeline.Build(chart.Notes), 51.97f).Hidden);
        Assert.False(BgTimeline.At(BgTimeline.Build(chart.Notes), 61.3f).Hidden);
        var scenes = SceneTimeline.Build(chart.Notes, "scene_01");
        Assert.Equal("scene_01", SceneTimeline.At(scenes, 51.97f));
        Assert.Equal("scene_02", SceneTimeline.At(scenes, 61.25f));
        Assert.Equal("scene_05", SceneTimeline.At(scenes, 61.55f));
        var playable = chart.Notes.Where(n => n.Kind != "event").Select(PlayNote.From).ToList();
        var boss = BossTimeline.Build(chart.Notes, playable);
        Assert.StartsWith("far_atk_1", boss.At(51.97f).Clip);
    }
}
