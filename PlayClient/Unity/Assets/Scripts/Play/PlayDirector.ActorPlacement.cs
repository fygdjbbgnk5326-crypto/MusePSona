using System.Collections.Generic;
using System.IO;
using System.Linq;
using MusePSona.Play;
using UnityEngine;

/// <summary>通用物件、钟摆和静态贴图的轨道摆放。</summary>
public sealed partial class PlayDirector
{
    void PlacePendulumRope(Actor a, Vector3 hammer, Flight.Pose fly, bool judged)
    {
        if (a.Extra == null) return;
        if (judged || !fly.HasPivot)
        {
            a.Extra.gameObject.SetActive(false);
            return;
        }
        a.Extra.gameObject.SetActive(true);
        var pivot = Stage(fly.PivotX, fly.PivotY, 0.02f);
        var lr = a.Extra.GetComponent<LineRenderer>();
        if (lr == null) return;
        lr.SetPosition(0, hammer);
        lr.SetPosition(1, pivot);
    }

    void PlacePendulum(Actor a, PlayNote n, float chartNow)
    {
        var g = _session.GradeOf(n.Id);
        var flight = Flight.DurationSec(n);
        var t0 = (float)n.TimeSec - flight;
        float p;
        if (g is Judge.Grade.Perfect or Judge.Grade.Great)
        {
            var back = Mathf.Max(0.4f, flight * 0.6f);
            p = 1f - Mathf.Clamp01((chartNow - (float)n.TimeSec) / back);
        }
        else
        {
            if (!Flight.TryPose(n, chartNow, out var missPose))
            {
                a.Root.gameObject.SetActive(false);
                return;
            }
            PutPendulum(a, n, missPose);
            return;
        }

        Flight.TryPendulum(n, p, out var pose);
        PutPendulum(a, n, pose);
    }

    void PutPendulum(Actor a, PlayNote n, Flight.Pose pose)
    {
        a.Root.gameObject.SetActive(true);
        var pos = Stage(pose.X, pose.Y);
        a.Root.position = pos;
        if (a.Body != null && a.BaseScale.sqrMagnitude < 1e-8f)
            a.BaseScale = a.Body.localScale;
        var attach = pos;
        if (pose.HasPivot && a.Body != null)
        {
            var pivot = Stage(pose.PivotX, pose.PivotY);
            var dir = pivot - pos;
            if (dir.sqrMagnitude > 1e-6f)
            {
                // 贴图为侧视：左锤面、右开槽。+Y 朝转轴 → 悠悠球横着挂，绳子从顶部进入开槽。
                var ang = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
                a.Body.localRotation = Quaternion.Euler(0f, 0f, ang);
                a.Body.localPosition = Vector3.zero;
                attach = pos + dir.normalized * (a.Body.lossyScale.y * 0.5f);
            }
        }
        PlacePendulumRope(a, attach, pose, false);
    }

    void RelookActor(Actor a, PlayNote n)
    {
        if (a.Root == null) return;
        if (a.LooksSprite)
        {
            foreach (var r in a.Root.GetComponentsInChildren<Renderer>())
            {
                if (r == null || r.sharedMaterial == null) continue;
                var material = r.sharedMaterial;
                r.sharedMaterial = null;
                Destroy(material);
            }
        }
        DestroyHoldLayerResources(a.HoldNoteUp);
        DestroyHoldLayerResources(a.HoldNoteDown);
        for (var i = a.Root.childCount - 1; i >= 0; i--)
            DestroyImmediate(a.Root.GetChild(i).gameObject);
        a.Spine = null;
        a.Body = a.Extra = a.Head = a.Tail = null;
        a.LooksSprite = false;
        a.Tint = null;
        a.InClip = null;
        a.InClipDuration = 0f;
        a.InClipReady = false;
        a.HoldNoteUp = null;
        a.HoldNoteDown = null;
        a.Alpha = -1f;
        a.HoldNoteUpTexture = null;
        a.HoldNoteDownTexture = null;
        a.HoldNoteTexturesReady = false;
        a.HoldNoteLayoutReady = false;
        a.LooksScene = _scene;
        if (TryLooks(a, n))
            ApplyActorLayer(a);
    }

    void Place(Actor a, PlayNote n, float chartNow)
    {
        if (!string.Equals(a.LooksScene, _scene, System.StringComparison.Ordinal))
            RelookActor(a, n);
        if (a.Spine != null)
        {
            PlaceSpine(a, n, chartNow);
            return;
        }
        if (n.Category == NoteCategory.Hold)
        {
            var holdGrade = _session.GradeOf(n.Id);
            var holdCaught = holdGrade is Judge.Grade.Perfect or Judge.Grade.Great;
            var holdMiss = holdGrade == Judge.Grade.Miss;
            if (!Flight.TryHoldSpan(n, chartNow, out var headX, out var tailX, pinHead: holdCaught))
            {
                a.Root.gameObject.SetActive(false);
                return;
            }
            a.Root.gameObject.SetActive(true);
            a.Root.localScale = Vector3.one;
            // 官机 0102_road_renderer.y=-0.95、0102_air_renderer.y=1.20。
            // 贴在通用轨（地面 -1、空中 1.125）上时星星下半截埋进地板，看起来不在轨道中心。
            var y = (n.Pathway == 1 ? 1.20f : -0.95f) * worldScale;
            var len = Mathf.Max(0f, (tailX - headX) * worldScale);
            a.Root.position = Vector3.zero;
            a.Body.position = new Vector3(headX * worldScale + len * 0.5f, y, 0.24f);
            var endBox = MdLooks.HoldEndBox * worldScale;
            FitTransformBox(a.Head, endBox);
            FitTransformBox(a.Tail, endBox);
            var thick = MdLooks.HoldBarBox * worldScale;
            a.Body.localScale = new Vector3(Mathf.Max(0.02f, len), thick, a.LooksSprite ? 1f : 0.12f);
            // 官机 SpineActionController.rotateRuration=1 → 头尾星星 360°/s。绕贴图重心转，避免甩到轨道下沿。
            var spin = chartNow * 360f;
            PlaceHoldStar(a.Head, headX * worldScale, y, endBox, spin);
            PlaceHoldStar(a.Tail, tailX * worldScale, y, endBox, spin);
            PlaceHoldNotes(a, n, headX, y, len, chartNow);
            SetActorAlpha(a, holdMiss ? Flight.HoldMissVisual(n, chartNow) : 1f);
            return;
        }
        if (n.Special == NoteSpecial.Pendulum)
        {
            PlacePendulum(a, n, chartNow);
            return;
        }

        if (!TryPlacePose(n, chartNow, out var pose))
        {
            a.Root.gameObject.SetActive(false);
            return;
        }
        a.Root.gameObject.SetActive(true);
        var g = _session.GradeOf(n.Id);
        if (n.Category == NoteCategory.Gear && g == Judge.Grade.Miss)
        {
            // 没有官机 Spine destroy 的资源包使用轻量回退：从撞击点继续
            // 向左离场，同时快速爆开、旋转并淡出。
            const float breakDuration = Flight.GearDestroySec;
            var age = Mathf.Max(0f, chartNow - (float)n.TimeSec);
            var u = Mathf.Clamp01(age / breakDuration);
            a.Root.position = Stage(Flight.ThroughX(n, chartNow), Flight.LaneY(n));
            a.Root.localScale = Vector3.one * worldScale * Mathf.Lerp(1f, 1.55f, u);
            a.Root.rotation = Quaternion.Euler(0f, 0f, 540f * u);
            SetActorAlpha(a, 1f - Mathf.SmoothStep(0.25f, 1f, u));
            return;
        }
        if (IsHoldStar(n) && a.Body != null)
        {
            var y = (n.Pathway == 1 ? 1.20f : -0.95f) * worldScale;
            var box = MdLooks.HoldEndBox * worldScale;
            PlaceHoldStar(a.Body, pose.X * worldScale, y, box, chartNow * 360f);
            if (a.BaseScale.sqrMagnitude > 1e-8f)
                a.Body.localScale = a.BaseScale;
            if (g is Judge.Grade.Perfect or Judge.Grade.Great)
            {
                var fadeT = Mathf.Max(0f, chartNow - (float)n.TimeSec);
                var pop = 1f + Mathf.Clamp01(fadeT / 0.12f) * 0.45f;
                var fade = 1f - Mathf.Clamp01((fadeT - 0.06f) / 0.42f);
                a.Body.localScale = a.BaseScale * pop;
                SetActorAlpha(a, fade);
            }
            return;
        }
        if (a.Body != null && a.BaseScale.sqrMagnitude < 1e-8f)
            a.BaseScale = a.Body.localScale;
        if (n.Category == NoteCategory.Masher && chartNow >= (float)n.TimeSec
            && g is Judge.Grade.Perfect or Judge.Grade.Great)
        {
            var pulse = 0.78f + 0.09f * Mathf.Sin(chartNow * 42f);
            if (a.Body != null)
                a.Body.localScale = a.BaseScale * (pulse / 0.78f);
        }

        var pos = Stage(pose.X, pose.Y, n.Category == NoteCategory.Masher ? -0.12f : 0f);
        var flying = a.LooksSprite
            && n.Special == NoteSpecial.None
            && n.Category is NoteCategory.Monster or NoteCategory.Ghost or NoteCategory.Music
            && g == Judge.Grade.None;
        if (flying)
            pos.y += Mathf.Sin(chartNow * 10f + n.Id * 0.37f) * 0.07f * worldScale;
        a.Root.position = pos;
        if (a.Body != null)
        {
            if (flying)
                a.Body.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(chartNow * 12f + n.Id) * 8f);
            else if (n.Category != NoteCategory.Gear)
                a.Body.localRotation = Quaternion.identity;
        }
        if (g is Judge.Grade.Perfect or Judge.Grade.Great && n.NeedsHit)
        {
            var hitAt = n.Category == NoteCategory.Masher ? (float)n.EndSec : (float)n.TimeSec;
            var t = Mathf.Max(0f, chartNow - hitAt);
            var pop = 1f + Mathf.Clamp01(t / 0.12f) * 0.45f;
            var fade = 1f - Mathf.Clamp01((t - 0.06f) / 0.42f);
            if (a.Body != null) a.Body.localScale = a.BaseScale * pop;
            if (a.LooksSprite || a.Tint != null) SetActorAlpha(a, fade);
        }

        if (n.Category == NoteCategory.Ghost && a.Tint != null)
        {
            var c = a.Tint.color;
            c.a = Flight.GhostAlpha(n, chartNow);
            a.Tint.color = c;
        }

        if (n.Special == NoteSpecial.Pendulum && a.Extra != null)
        {
            if (pose.HasPivot)
            {
                a.Extra.gameObject.SetActive(true);
                var pivot = Stage(pose.PivotX, pose.PivotY, 0.02f);
                var lr = a.Extra.GetComponent<LineRenderer>();
                var attach = pos;
                if (a.Body != null)
                {
                    var dir = pivot - pos;
                    if (dir.sqrMagnitude > 1e-6f)
                        a.Body.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f);
                    // 绳接到锤头顶部开槽，而不是贴图中心
                    attach = a.Body.position + a.Body.up * (a.Body.lossyScale.y * 0.48f);
                }
                if (lr != null)
                {
                    lr.SetPosition(0, attach);
                    lr.SetPosition(1, pivot);
                }
            }
            else a.Extra.gameObject.SetActive(false);
        }

        if (n.Category == NoteCategory.Gear)
        {
            a.Spin += 220f * Time.deltaTime;
            var rot = a.LooksSprite ? Quaternion.Euler(0f, 0f, a.Spin) : Quaternion.Euler(90f, 0f, a.Spin);
            if (a.Body != null) a.Body.localRotation = rot;
            if (a.Extra != null) a.Extra.localRotation = rot;
        }
    }
}

