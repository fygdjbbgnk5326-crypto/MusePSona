using System.Collections.Generic;
using System.IO;
using System.Linq;
using MusePSona.Play;
using UnityEngine;

/// <summary>舞台：相机、占位车道、判定圈。</summary>
public sealed partial class PlayDirector
{
    void SetupStage()
    {
        EnsureCamera();
        MdSceneBg.DropStrayEditRoots();
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.55f, 0.58f, 0.62f);
        if (FindObjectOfType<Light>() == null)
        {
            var lightGo = new GameObject("KeyLight");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.15f;
            light.color = Color.white;
            lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }

        _shader = Shader.Find("Unlit/Color") ?? Shader.Find("Sprites/Default");
        _matRoad = NewMat(new Color(1f, 0.43f, 0.78f));
        _matAir = NewMat(new Color(0.22f, 0.84f, 1f));
        _matGuide = NewMat(new Color(0.95f, 0.95f, 0.55f));
        _matLaneAir = NewMat(new Color(0.55f, 0.55f, 0.42f));
        _matLaneRoad = NewMat(new Color(0.55f, 0.55f, 0.42f));
        _matGear = NewMat(new Color(0.96f, 0.94f, 1f));
        _matGearCore = NewMat(new Color(0.48f, 0.30f, 1f));
        _matMasher = NewMat(new Color(0.91f, 0.13f, 0.31f));
        _matBoss = NewMat(new Color(0.70f, 0.42f, 1f));
        _matHeart = NewMat(new Color(1f, 0.18f, 0.44f));
        _matMusic = NewMat(new Color(0.25f, 0.61f, 1f));
        _matHold = NewMat(new Color(1f, 0.82f, 0.35f));
        _matPendulum = NewMat(new Color(0.95f, 0.95f, 0.92f));
        _matRaider = NewMat(new Color(1f, 0.72f, 0.22f));
        _matFxP = NewMat(new Color(0.55f, 1f, 1f));
        _matFxG = NewMat(new Color(1f, 0.85f, 0.25f));
        _matFxM = NewMat(new Color(1f, 0.22f, 0.28f));

        SpawnBar("JudgeLine", new Vector3(Flight.HitX * worldScale, 0f, 0.2f), new Vector3(0.08f, 6f, 0.08f), _matGuide);
        _roadLane = SpawnBar("RoadLane", new Vector3(0f, -Flight.RoadY * worldScale, 0.4f), new Vector3(18f, 0.04f, 0.04f), _matLaneRoad);
        _airLane = SpawnBar("AirLane", new Vector3(0f, -Flight.AirY * worldScale, 0.4f), new Vector3(18f, 0.04f, 0.04f), _matLaneAir);
        var midLane = SpawnBar("MidLane", new Vector3(0f, -Flight.MidY * worldScale, 0.45f), new Vector3(18f, 0.025f, 0.04f), NewMat(new Color(0.70f, 0.42f, 1f)));
        _laneRoadT = _roadLane != null ? _roadLane.transform : null;
        _laneAirT = _airLane != null ? _airLane.transform : null;
        _laneMidT = midLane != null ? midLane.transform : null;
        RebuildJudges();
    }

    void EnsureCamera()
    {
        var cam = Camera.main;
        if (cam == null)
        {
            var tagged = GameObject.FindGameObjectWithTag("MainCamera");
            if (tagged != null) cam = tagged.GetComponent<Camera>();
        }
        if (cam == null)
        {
            foreach (var c in FindObjectsOfType<Camera>())
            {
                cam = c;
                break;
            }
        }
        if (cam == null)
        {
            var go = new GameObject("Main Camera");
            cam = go.AddComponent<Camera>();
            go.AddComponent<AudioListener>();
            go.tag = "MainCamera";
            go.transform.position = new Vector3(0f, 0f, -10f);
        }
        cam.enabled = true;
        cam.targetDisplay = 0;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.22f, 0.24f, 0.28f);
        cam.orthographic = true;
        cam.orthographicSize = Flight.CameraSize;
        cam.depth = -1f;
        _mainCamera = cam;
    }

    Material NewMat(Color color)
    {
        var m = _shader != null ? new Material(_shader) : new Material(Shader.Find("Standard"));
        m.color = color;
        if (m.HasProperty("_Color")) m.SetColor("_Color", color);
        return m;
    }

    Material NewFadeMat(Color color)
    {
        var sh = Shader.Find("Sprites/Default") ?? _shader ?? Shader.Find("Unlit/Color");
        var m = new Material(sh);
        m.color = color;
        if (m.HasProperty("_Color")) m.SetColor("_Color", color);
        m.renderQueue = 3000;
        return m;
    }

    static Renderer SpawnBar(string name, Vector3 pos, Vector3 scale, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.position = pos;
        go.transform.localScale = scale;
        var col = go.GetComponent<Collider>();
        if (col) Destroy(col);
        var r = go.GetComponent<Renderer>();
        if (r != null && mat != null) r.sharedMaterial = mat;
        return r;
    }

    void RebuildJudges()
    {
        if (_matAir == null) return;
        DropJudge(_airJudge);
        DropJudge(_roadJudge);
        var oneKey = SceneTimeline.RulesFor(_scene).OneKey;
        _airJudge = oneKey ? null : SpawnJudgeRing("JudgeAir", Flight.AirY, _matAir, true);
        _roadJudge = SpawnJudgeRing("JudgeRoad", Flight.RoadY, _matRoad, false);
        if (_laneAirT != null) _laneAirT.gameObject.SetActive(!oneKey && (_bg == null));
        if (_laneMidT != null) _laneMidT.gameObject.SetActive(!oneKey && (_bg == null));
    }

    static void DropJudge(Transform root)
    {
        if (root == null) return;
        // 静图判定圈由 SpriteQuad 创建独占材质；方块兜底使用舞台共享材质。
        if (FindNamed(root, "out") != null)
            DestroyOwnedVisual(root);
        else
            Destroy(root.gameObject);
    }

    Transform SpawnJudgeRing(string name, float yDown, Material rim, bool air)
    {
        var root = new GameObject(name).transform;
        root.position = Stage(Flight.HitX, yDown, 0.08f);
        if (TryJudgeSprites(root, air)) return root;
        var r = 0.48f * worldScale;
        for (var q = 0; q < 4; q++)
        {
            var deg = q * 90f + 45f;
            var rad = deg * Mathf.Deg2Rad;
            var t = Prim(PrimitiveType.Cube, root, new Vector3(0.30f, 0.055f, 0.055f), _matGuide, "arc" + q);
            t.localPosition = new Vector3(Mathf.Cos(rad) * r, Mathf.Sin(rad) * r, 0f);
            t.localRotation = Quaternion.Euler(0f, 0f, deg + 90f);
        }
        Prim(PrimitiveType.Sphere, root, Vector3.one * 0.11f, rim, "dot");
        return root;
    }

    bool TryJudgeSprites(Transform root, bool air)
    {
        if (!MdLooks.TrySprite(_scene, "judge_out", out var outer) || outer == null) return false;
        var ring = SpriteQuad(root, outer, MdLooks.JudgeOutBox * worldScale, "out");
        ring.localPosition = new Vector3(0f, 0f, 0.01f);
        TintSprite(ring, air ? MdLooks.HitPointAirOut : MdLooks.HitPointRoadOut);
        PaintLayer(ring, 2880, -16);
        if (MdLooks.TrySprite(_scene, "judge_in", out var inner) && inner != null)
        {
            var dot = SpriteQuad(root, inner, MdLooks.JudgeInBox * worldScale, "in");
            dot.localPosition = new Vector3(0f, 0f, -0.01f);
            TintSprite(dot, air ? MdLooks.HitPointAirIn : MdLooks.HitPointRoadIn);
            PaintLayer(dot, 2890, -14);
        }
        return true;
    }

    static void TintSprite(Transform t, Color c)
    {
        if (t == null) return;
        var r = t.GetComponent<Renderer>();
        if (r == null || r.material == null) return;
        r.material.color = c;
        if (r.material.HasProperty("_Color")) r.material.SetColor("_Color", c);
    }

    static Transform FindNamed(Transform root, string name)
    {
        if (root == null) return null;
        for (var i = 0; i < root.childCount; i++)
        {
            var c = root.GetChild(i);
            if (c.name == name) return c;
        }
        return null;
    }

    void AnimateJudge(Transform root, bool catchOn, bool air)
    {
        if (root == null) return;
        // 外圈 HitPointOut=三段弧，逆时针转；内圈 HitPointIn=更小的完整环。standby 只调 alpha。
        var outer = FindNamed(root, "out");
        if (outer != null)
        {
            outer.Rotate(0f, 0f, 160f * Time.unscaledDeltaTime, Space.Self);
            var c = air ? MdLooks.HitPointAirOut : MdLooks.HitPointRoadOut;
            if (catchOn) c.a = Mathf.Min(1f, c.a * 1.15f);
            TintSprite(outer, c);
        }
        var inner = FindNamed(root, "in");
        if (inner == null) return;
        inner.localRotation = Quaternion.identity;
        FitTransformBox(inner, MdLooks.JudgeInBox * worldScale);
        Texture2D tex = null;
        if (catchOn) MdLooks.TrySprite(_scene, "judge_catch", out tex);
        if (tex == null) MdLooks.TrySprite(_scene, "judge_in", out tex);
        if (tex != null)
        {
            var r = inner.GetComponent<Renderer>();
            if (r != null && r.material != null)
            {
                MdMat.SetTex(r.material, tex);
            }
        }
        var ic = air ? MdLooks.HitPointAirIn : MdLooks.HitPointRoadIn;
        if (catchOn) ic.a = 1f;
        TintSprite(inner, ic);
    }
}
