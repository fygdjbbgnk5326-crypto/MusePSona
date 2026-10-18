namespace MusePSona.Play;

/// <summary>一局状态。Unity / CLI 共用：推进 now、按键、Miss 扫描。</summary>
public sealed class PlaySession
{
    public PlaySong Song { get; }
    public int Difficulty { get; }
    public IReadOnlyList<PlayNote> Notes { get; }
    public IReadOnlyList<PlayNote> HoldNotes { get; }
    public IReadOnlyList<PlayNote> MasherNotes { get; }
    public IReadOnlyList<SceneTimeline.Step> Scenes { get; }
    public IReadOnlyList<BgTimeline.Step> Bg { get; }
    public IReadOnlyList<FxTimeline.Step> Fx { get; }
    public BossTimeline Boss { get; }
    public float DelaySec { get; }
    public int Combo { get; private set; }
    public int Perfects { get; private set; }
    public int Greats { get; private set; }
    public int Misses { get; private set; }

    public readonly struct JudgeHit
    {
        public PlayNote Note { get; init; }
        public Judge.Grade Grade { get; init; }
        /// <summary>连打物件的 1 基击数；普通物件为 0。</summary>
        public int MashIndex { get; init; }
    }

    public const float MashTickSec = 0.08f;
    /// <summary>同轨 D↔F / J↔K 换指时允许的短暂松键间隔。</summary>
    public const float HoldReleaseGrace = 0.08f;

    readonly HashSet<int> _resolved = new();
    readonly HashSet<int> _comboed = new();
    readonly Dictionary<int, Judge.Grade> _grades = new();
    readonly Dictionary<int, int> _mashHits = new();
    readonly Dictionary<int, float> _mashAt = new();
    readonly Dictionary<int, float> _holdReleasedAt = new();
    readonly List<JudgeHit> _hits = new();
    int _settleIndex;
    int _visibleStartIndex;

    public PlaySession(PlaySong song, int difficulty)
    {
        Song = song;
        Difficulty = song.ResolveDifficulty(difficulty);
        Notes = song.Playable(Difficulty);
        HoldNotes = Notes.Where(n => n.Category == NoteCategory.Hold).ToArray();
        MasherNotes = Notes.Where(n => n.Category == NoteCategory.Masher).ToArray();
        Scenes = SceneTimeline.Build(song.Chart(Difficulty).Notes, song.Document.Meta.Scene);
        Bg = BgTimeline.Build(song.Chart(Difficulty).Notes);
        Fx = FxTimeline.Build(song.Chart(Difficulty).Notes);
        Boss = BossTimeline.Build(song.Chart(Difficulty).Notes, Notes);
        DelaySec = song.DelaySec;
    }

    public string SceneAt(float now) => SceneTimeline.At(Scenes, now - DelaySec);

    public FxTimeline.State FxAt(float now) => FxTimeline.At(Fx, now - DelaySec);

    bool NotesHidden(float now) => FxAt(now).NotesHidden;

    public IEnumerable<PlayNote> Visible(float now, float scrollMul = 1f)
    {
        var t = now - DelaySec;
        var hide = NotesHidden(now);
        // 已完全离场的旧物件不再从谱头重复检查；同时在尚未进入最长
        // 飞近窗口时提前结束。长谱后半段由 O(全谱) 收敛为 O(屏内物件)。
        while (_visibleStartIndex < Notes.Count)
        {
            var old = Notes[_visibleStartIndex];
            var end = old.Category is NoteCategory.Hold or NoteCategory.Masher
                ? (float)old.EndSec : (float)old.TimeSec;
            if (t <= end + 1f) break;
            _visibleStartIndex++;
        }
        var lookAhead = 2f / Math.Max(0.01f, scrollMul);
        for (var noteIndex = _visibleStartIndex; noteIndex < Notes.Count; noteIndex++)
        {
            var n = Notes[noteIndex];
            if ((float)n.TimeSec - t > lookAhead) yield break;
            if (hide) continue;
            if (n.Category == NoteCategory.Hold)
            {
                var holdCaught = _grades.TryGetValue(n.Id, out var hg)
                    && hg is Judge.Grade.Perfect or Judge.Grade.Great;
                if (!Flight.TryHoldSpan(n, t, out _, out _, scrollMul, pinHead: holdCaught)) continue;
                yield return n;
                continue;
            }
            if (n.Category == NoteCategory.Masher)
            {
                if (_grades.TryGetValue(n.Id, out var mg) && mg is Judge.Grade.Perfect or Judge.Grade.Great)
                {
                    if (t > (float)n.EndSec + Flight.OutKeepSec) continue;
                    if ((float)n.TimeSec - t > Flight.DurationSec(n, scrollMul)) continue;
                    yield return n;
                    continue;
                }
                if (t < (float)n.TimeSec)
                {
                    if (Flight.TryPose(n, t, out _, scrollMul)) yield return n;
                    continue;
                }
                if (Flight.TryExitPose(n, t, out _, scrollMul)) yield return n;
                continue;
            }

            if (n.Category == NoteCategory.Ghost && Flight.GhostAlpha(n, t) <= 0f)
                continue;

            var grade = GradeOf(n.Id);
            if (grade is Judge.Grade.Perfect or Judge.Grade.Great)
            {
                if (t < (float)n.TimeSec + Flight.OutKeepSec) yield return n;
                continue;
            }
            if (grade == Judge.Grade.Miss)
            {
                if (n.Category == NoteCategory.Gear)
                {
                    if (t < (float)n.TimeSec + Flight.GearDestroySec) yield return n;
                    continue;
                }
                if (Flight.TryExitPose(n, t, out _, scrollMul)) yield return n;
                continue;
            }
            if (!n.NeedsHit)
            {
                if (t < (float)n.TimeSec)
                {
                    if (Flight.TryPose(n, t, out _, scrollMul)) yield return n;
                }
                else if (Flight.TryExitPose(n, t, out _, scrollMul))
                    yield return n;
                continue;
            }
            if (t >= (float)n.TimeSec)
            {
                if (Flight.TryExitPose(n, t, out _, scrollMul)) yield return n;
                continue;
            }
            if (!Flight.TryPose(n, t, out _, scrollMul)) continue;
            yield return n;
        }
    }

    /// <summary>只看 SceneAt：scene_11 单轨，1O 切走后立刻双轨。</summary>
    bool OneKeyIgnoreLane(float now)
        => SceneTimeline.RulesFor(SceneAt(now)).OneKey;

    /// <param name="air">true = 空中轨（D/F），false = 地面（J/K）。1K 忽略分轨。</param>
    public Judge.Grade HitLane(bool air, float now)
    {
        var t = now - DelaySec;
        if (NotesHidden(now)) return Judge.Grade.None;
        var oneKey = OneKeyIgnoreLane(now);
        for (var i = _settleIndex; i < Notes.Count; i++)
        {
            var n = Notes[i];
            if ((float)n.TimeSec > t + Judge.Great) break;
            if (n.Category != NoteCategory.Masher || _resolved.Contains(n.Id)) continue;
            if (!oneKey && !Flight.CenterLane(n) && (n.Pathway == 1) != air) continue;
            if (!_grades.TryGetValue(n.Id, out var mg) || mg == Judge.Grade.Miss) continue;
            if (t < (float)n.TimeSec || t > (float)n.EndSec) continue;
            MashTick(n, t);
            return Judge.Grade.Perfect;
        }

        PlayNote? best = null;
        for (var i = _settleIndex; i < Notes.Count; i++)
        {
            var n = Notes[i];
            if ((float)n.TimeSec > t + Judge.Great) break;
            // 10 / 23 / 爱心由角色接触自动收集，不能消耗攻击按键；24 不是
            // 被动收集物，仍会进入这里要求玩家单独按键。
            if (!n.NeedsHit || n.IsPassiveCollectible || _resolved.Contains(n.Id)) continue;
            if ((n.Category is NoteCategory.Hold or NoteCategory.Masher) && _grades.ContainsKey(n.Id))
                continue;
            if (!oneKey && !Flight.CenterLane(n) && (n.Pathway == 1) != air) continue;
            var err = Math.Abs(t - (float)n.TimeSec);
            if (err > Judge.Great) continue;
            // 密集同轨串按时间顺序消费，不能因下一颗更接近当前时刻而
            // 越过前一颗；否则 J/K 滚键会出现“后音吃前音”的断连感。
            best = n;
            break;
        }
        if (best == null) return Judge.Grade.None;
        var g = Judge.Tap(best, now, DelaySec);
        Apply(best, g);
        return g;
    }

    public Judge.Grade GradeOf(int id)
        => _grades.TryGetValue(id, out var g) ? g : Judge.Grade.None;

    public int MashHits(int id)
        => _mashHits.TryGetValue(id, out var c) ? c : 0;

    public float MashAt(int id)
        => _mashAt.TryGetValue(id, out var at) ? at : 0f;

    public void TakeHits(List<JudgeHit> dest)
    {
        if (_hits.Count == 0) return;
        dest.AddRange(_hits);
        _hits.Clear();
    }

    public void TickMisses(float now)
    {
        var t = now - DelaySec;
        if (NotesHidden(now)) return;
        foreach (var n in Notes)
        {
            if (n.Category == NoteCategory.Gear)
            {
                MarkDodgePass(n, t);
                continue;
            }
            if (!n.NeedsHit || _resolved.Contains(n.Id)) continue;
            if (n.Category is NoteCategory.Hold or NoteCategory.Masher)
            {
                if (!_grades.ContainsKey(n.Id))
                {
                    if (Judge.IsLateMiss(n, now, DelaySec))
                        Apply(n, Judge.Grade.Miss);
                }
                else if (t >= (float)n.EndSec)
                {
                    if (n.Category == NoteCategory.Hold)
                        Apply(n, _grades[n.Id], tail: true);
                    else
                        _resolved.Add(n.Id);
                }
                continue;
            }
            if (Judge.IsLateMiss(n, now, DelaySec))
                Apply(n, Judge.Grade.Miss);
        }
    }

    /// <summary>
    /// 官机手动结算：收集物按角色所在轨接触获得；齿轮异轨 PASS、同轨碰撞。
    /// activeHoldMask 位 0=地面 Hold、位 1=空中 Hold；Hold 内齿轮不打断 press。
    /// </summary>
    public void TickManual(float now, bool playerAir, int activeHoldMask, int heldLaneMask = -1)
    {
        var t = now - DelaySec;
        if (NotesHidden(now)) return;
        for (var i = _settleIndex; i < Notes.Count; i++)
        {
            var n = Notes[i];
            if ((float)n.TimeSec > t) break;
            if (_resolved.Contains(n.Id)) continue;

            var noteAir = n.Pathway == 1;
            if (n.Category == NoteCategory.Gear)
            {
                // 官机在齿轮抵达判定圈（TimeSec）的当帧锁定结果；越过判定圈后
                // 不能再按角色 X 二次判定，否则会出现已经通过却延迟受击。
                // 若该帧因藏键、模式切换或卡顿没有结算，过期后只允许 PASS，
                // 不能用后续帧的角色轨道补判碰撞。
                if (t - (float)n.TimeSec > Flight.GearJudgeLateSec)
                {
                    Apply(n, Judge.Grade.Pass);
                    continue;
                }
                // 官机长按期间的条内齿轮只做擦身结算，不注入跳跃/下砍。
                var safe = activeHoldMask != 0 || playerAir != noteAir;
                Apply(n, safe ? Judge.Grade.Pass : Judge.Grade.Miss);
                continue;
            }

            if (n.IsPassiveCollectible)
            {
                var laneBit = 1 << (noteAir ? 1 : 0);
                var touching = (activeHoldMask & laneBit) != 0
                    || (activeHoldMask == 0 && playerAir == noteAir);
                if (touching)
                    Apply(n, Judge.Grade.Perfect);
                else if (Judge.IsLateMiss(n, now, DelaySec))
                    Apply(n, Judge.Grade.Miss);
                continue;
            }

            if (!n.NeedsHit) continue;
            if (n.Category is NoteCategory.Hold or NoteCategory.Masher)
            {
                if (!_grades.ContainsKey(n.Id))
                {
                    if (Judge.IsLateMiss(n, now, DelaySec))
                        Apply(n, Judge.Grade.Miss);
                }
                else if (n.Category == NoteCategory.Hold && t < (float)n.EndSec
                    && heldLaneMask >= 0)
                {
                    var laneBit = 1 << (noteAir ? 1 : 0);
                    if ((heldLaneMask & laneBit) != 0)
                    {
                        _holdReleasedAt.Remove(n.Id);
                    }
                    else if (!_holdReleasedAt.TryGetValue(n.Id, out var releasedAt))
                    {
                        _holdReleasedAt[n.Id] = t;
                    }
                    else if (t - releasedAt > HoldReleaseGrace)
                    {
                        // 头部已命中但持续按键中断：结束 press 并按断 Hold 结算。
                        _holdReleasedAt.Remove(n.Id);
                        Apply(n, Judge.Grade.Miss);
                    }
                }
                else if (t >= (float)n.EndSec)
                {
                    if (n.Category == NoteCategory.Hold)
                    {
                        _holdReleasedAt.Remove(n.Id);
                        Apply(n, _grades[n.Id], tail: true);
                    }
                    else
                        _resolved.Add(n.Id);
                }
                continue;
            }
            if (Judge.IsLateMiss(n, now, DelaySec))
                Apply(n, Judge.Grade.Miss);
        }
        AdvanceSettled();
    }

    void AdvanceSettled()
    {
        while (_settleIndex < Notes.Count && _resolved.Contains(Notes[_settleIndex].Id))
            _settleIndex++;
    }

    /// <summary>自动演奏：到点 Perfect。用来对账谱面，不是官机判定。</summary>
    public void Autoplay(float now)
    {
        var t = now - DelaySec;
        var hide = NotesHidden(now);
        foreach (var n in Notes)
        {
            if (_resolved.Contains(n.Id)) continue;
            if (hide) continue;
            if (!n.NeedsHit)
            {
                MarkDodgePass(n, t);
                if (t >= (float)n.TimeSec && !Flight.TryExitPose(n, t, out _))
                    _resolved.Add(n.Id);
                continue;
            }
            if (n.Category == NoteCategory.Hold)
            {
                if (!_grades.ContainsKey(n.Id) && t >= (float)n.TimeSec)
                    Apply(n, Judge.Grade.Perfect);
                if (_grades.ContainsKey(n.Id) && t >= (float)n.EndSec)
                    Apply(n, Judge.Grade.Perfect, tail: true);
                continue;
            }
            if (n.Category == NoteCategory.Masher)
            {
                if (!_grades.ContainsKey(n.Id) && t >= (float)n.TimeSec)
                    Apply(n, Judge.Grade.Perfect);
                if (_grades.TryGetValue(n.Id, out var mg) && mg != Judge.Grade.Miss && t >= (float)n.TimeSec)
                {
                    var span = Math.Min(t, (float)n.EndSec) - (float)n.TimeSec;
                    var expected = Math.Max(1, 1 + (int)(span / MashTickSec));
                    while (MashHits(n.Id) < expected)
                        MashTick(n, (float)n.TimeSec + MashHits(n.Id) * MashTickSec);
                }
                if (_grades.ContainsKey(n.Id) && t >= (float)n.EndSec)
                    _resolved.Add(n.Id);
                continue;
            }
            if (t >= (float)n.TimeSec)
                Apply(n, Judge.Grade.Perfect);
        }
    }

    void Apply(PlayNote n, Judge.Grade g, bool tail = false)
    {
        if (g == Judge.Grade.None) return;
        var mashIndex = n.Category == NoteCategory.Masher && !tail
            && g is Judge.Grade.Perfect or Judge.Grade.Great ? 1 : 0;
        _hits.Add(new JudgeHit { Note = n, Grade = g, MashIndex = mashIndex });
        if (g == Judge.Grade.Pass)
        {
            _grades[n.Id] = g;
            return;
        }
        if (g == Judge.Grade.Miss)
        {
            _resolved.Add(n.Id);
            _grades[n.Id] = g;
            // 齿轮碰撞属于受伤事件，不是漏键：不增加 Miss、不清 Combo。
            if (n.Category == NoteCategory.Gear) return;
            Misses++;
            Combo = 0;
            return;
        }
        if (g == Judge.Grade.Perfect) Perfects++;
        else Greats++;
        if (n.CountsCombo)
        {
            var key = tail ? -n.Id : n.Id;
            if (_comboed.Add(key)) Combo++;
        }
        _grades[n.Id] = g;
        if (n.Category == NoteCategory.Masher && !tail)
        {
            _mashHits[n.Id] = 1;
            _mashAt[n.Id] = (float)n.TimeSec;
            return;
        }
        if (n.Category == NoteCategory.Hold && !tail)
            return;
        _resolved.Add(n.Id);
    }

    void MarkDodgePass(PlayNote n, float t)
    {
        if (n.Category != NoteCategory.Gear) return;
        if (_grades.ContainsKey(n.Id) || _resolved.Contains(n.Id)) return;
        if (t >= (float)n.TimeSec)
            Apply(n, Judge.Grade.Pass);
    }

    void MashTick(PlayNote n, float chartNow)
    {
        var c = MashHits(n.Id) + 1;
        _mashHits[n.Id] = c;
        _mashAt[n.Id] = chartNow;
        if (n.CountsCombo) Combo++;
        _hits.Add(new JudgeHit { Note = n, Grade = Judge.Grade.Perfect, MashIndex = c });
    }
}
