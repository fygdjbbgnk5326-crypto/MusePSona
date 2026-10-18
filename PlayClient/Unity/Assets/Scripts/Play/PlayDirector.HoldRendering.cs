using System.Collections.Generic;
using System.IO;
using System.Linq;
using MusePSona.Play;
using UnityEngine;

/// <summary>Hold 头尾星、长条网格、裁切与透明度渲染。</summary>
public sealed partial class PlayDirector
{
    internal sealed class HoldLayer
    {
        public Transform Transform;
        public Mesh Mesh;
        public readonly Vector3[] Vertices = new Vector3[4];
        public readonly Vector2[] Uv = new Vector2[4];
    }

    static readonly int[] HoldTriangles = { 0, 2, 1, 0, 3, 2 };

    void PlaceHoldNotes(Actor a, PlayNote n, float headX, float y, float len, float chartNow)
    {
        if (!a.HoldNoteTexturesReady)
        {
            var lane = n.Pathway == 1 ? "air" : "ground";
            MdLooks.TrySprite(_scene, lane + "_hold_note_up", out a.HoldNoteUpTexture);
            MdLooks.TrySprite(_scene, lane + "_hold_note_down", out a.HoldNoteDownTexture);
            a.HoldNoteTexturesReady = true;
        }
        var up = a.HoldNoteUpTexture;
        var down = a.HoldNoteDownTexture;
        if (up == null && down == null)
        {
            if (a.Extra != null) a.Extra.gameObject.SetActive(false);
            return;
        }
        if (up == null) up = down;
        if (down == null) down = up;
        if (a.Extra == null)
        {
            var go = new GameObject("holdFx");
            a.Extra = go.transform;
        }
        // 不要挂 Body：条子 localScale.x=剩余长度，子物体会跟 Simple 一起被压扁。
        a.Extra.SetParent(a.Root, false);
        a.Extra.gameObject.SetActive(true);
        a.Extra.localRotation = Quaternion.identity;
        a.Extra.localScale = Vector3.one;
        var bob = Mathf.Sin(chartNow * (2f * Mathf.PI) * 2f) * MdLooks.HoldNoteBob * worldScale;
        a.Extra.position = new Vector3(headX * worldScale + len * 0.5f, y + bob, 0.22f);
        var tex = up != null ? up : down;
        var tileH = Mathf.Max(0.05f, MdLooks.HoldBarBox * worldScale);
        var tileW = Mathf.Max(0.05f, tileH * tex.width / Mathf.Max(1, tex.height) * MdLooks.HoldNoteSpanMul);
        var bar = Mathf.Max(0.02f, len);
        var fullLen = Mathf.Max(bar, ((float)n.EndSec - (float)n.TimeSec) * Flight.LaneSpeed(n.ApproachRate) * worldScale);
        var uFull = fullLen / tileW;
        var u0 = uFull - bar / tileW;
        var u1 = uFull;
        PlaceHoldNoteLayer(a, false, a.Extra, "noteFixed", up != null ? up : down,
            bar, tileH, u0, u1, 2810);
        if (down != null && down != up)
            PlaceHoldNoteLayer(a, true, a.Extra, "noteDown", down,
                bar, tileH, u0, u1, 2811);
        if (!a.HoldNoteLayoutReady)
        {
            if (down == null || down == up)
                HideNamedChild(a.Extra, "noteDown");
            HideHoldNoteJunk(a.Extra);
            a.HoldNoteLayoutReady = true;
        }
    }

    void PlaceHoldNoteLayer(Actor actor, bool lower, Transform root, string name,
        Texture2D tex, float len, float tileH, float u0, float u1, int queue)
    {
        var cache = lower ? actor.HoldNoteDown : actor.HoldNoteUp;
        if (cache == null || cache.Transform == null)
        {
            Transform layer = null;
            for (var i = 0; i < root.childCount; i++)
            {
                if (root.GetChild(i).name == name) { layer = root.GetChild(i); break; }
            }
            if (layer == null)
                layer = SpriteQuad(root, tex, 1f, name);
            cache = new HoldLayer { Transform = layer };
            var mf = layer.GetComponent<MeshFilter>();
            if (mf != null)
            {
                cache.Mesh = new Mesh { name = "HoldNoteMesh" };
                cache.Mesh.MarkDynamic();
                cache.Mesh.vertices = cache.Vertices;
                cache.Mesh.uv = cache.Uv;
                cache.Mesh.triangles = HoldTriangles;
                mf.sharedMesh = cache.Mesh;
            }
            tex.wrapMode = TextureWrapMode.Repeat;
            var r = layer.GetComponent<Renderer>();
            if (r != null)
            {
                var sh = Shader.Find("MusePSona/MdHoldNote") ?? Shader.Find("MusePSona/MdSprite");
                var material = r.material;
                if (material != null)
                {
                    if (sh != null && material.shader != sh) material.shader = sh;
                    MdMat.SetTex(material, tex);
                    MdMat.SetScale(material, Vector2.one);
                    MdMat.SetOffset(material, Vector2.zero);
                    if (material.HasProperty("_AtlasRect"))
                        material.SetVector("_AtlasRect", new Vector4(0f, 0f, 1f, 1f));
                    if (material.HasProperty("_Color")) material.SetColor("_Color", Color.white);
                }
            }
            PaintLayer(layer, queue, -16);
            if (lower) actor.HoldNoteDown = cache;
            else actor.HoldNoteUp = cache;
        }
        var layerTransform = cache.Transform;
        layerTransform.gameObject.SetActive(true);
        layerTransform.localPosition = Vector3.zero;
        layerTransform.localRotation = Quaternion.identity;
        layerTransform.localScale = Vector3.one;
        FillHoldNoteMesh(cache, len, tileH, u0, u1);
    }

    static void FillHoldNoteMesh(HoldLayer layer, float len, float h, float u0, float u1)
    {
        var mesh = layer.Mesh;
        if (mesh == null) return;
        var hw = len * 0.5f;
        var hh = h * 0.5f;
        layer.Vertices[0] = new Vector3(-hw, -hh, 0f);
        layer.Vertices[1] = new Vector3(hw, -hh, 0f);
        layer.Vertices[2] = new Vector3(hw, hh, 0f);
        layer.Vertices[3] = new Vector3(-hw, hh, 0f);
        layer.Uv[0] = new Vector2(u0, 0f);
        layer.Uv[1] = new Vector2(u1, 0f);
        layer.Uv[2] = new Vector2(u1, 1f);
        layer.Uv[3] = new Vector2(u0, 1f);
        mesh.vertices = layer.Vertices;
        mesh.uv = layer.Uv;
        mesh.bounds = new Bounds(Vector3.zero, new Vector3(len, h, 0.01f));
    }

    static void HideHoldNoteJunk(Transform root)
    {
        for (var i = 0; i < root.childCount; i++)
        {
            var c = root.GetChild(i);
            var n = c.name;
            if (n == "noteFx" || n == "noteStrip" || n == "noteWorld" || n == "noteBob"
                || n.StartsWith("noteTile", System.StringComparison.Ordinal))
                c.gameObject.SetActive(false);
        }
    }

    static void HideNamedChild(Transform root, string name)
    {
        for (var i = 0; i < root.childCount; i++)
        {
            var c = root.GetChild(i);
            if (c.name == name) c.gameObject.SetActive(false);
        }
    }

    void SetActorAlpha(Actor a, float alpha)
    {
        alpha = Mathf.Clamp01(alpha);
        if (Mathf.Abs(a.Alpha - alpha) < 0.001f) return;
        a.Alpha = alpha;
        ApplyAlpha(a.Body, alpha);
        ApplyAlpha(a.Extra, alpha);
        ApplyAlpha(a.Head, alpha);
        ApplyAlpha(a.Tail, alpha);
    }

    void ApplyAlpha(Transform t, float alpha)
    {
        if (t == null) return;
        _rendererScratch.Clear();
        t.GetComponentsInChildren(true, _rendererScratch);
        for (var i = 0; i < _rendererScratch.Count; i++)
        {
            var r = _rendererScratch[i];
            if (r == null) continue;
            var material = r.material;
            if (material == null) continue;
            var c = material.color;
            c.a = alpha;
            material.color = c;
        }
    }
}

