using System.Collections.Generic;
using System.IO;
using System.Linq;
using MusePSona.Play;
using UnityEngine;

/// <summary>Hold 头尾星、长条网格、裁切与透明度渲染。</summary>
public sealed partial class PlayDirector
{
    void PlaceHoldNotes(Actor a, PlayNote n, float headX, float y, float len, float chartNow)
    {
        var lane = n.Pathway == 1 ? "air" : "ground";
        MdLooks.TrySprite(_scene, lane + "_hold_note_up", out var up);
        MdLooks.TrySprite(_scene, lane + "_hold_note_down", out var down);
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
        PlaceHoldNoteLayer(a.Extra, "noteFixed", up != null ? up : down, bar, tileH, u0, u1, 2810);
        if (down != null && down != up)
            PlaceHoldNoteLayer(a.Extra, "noteDown", down, bar, tileH, u0, u1, 2811);
        else
            HideNamedChild(a.Extra, "noteDown");
        HideHoldNoteJunk(a.Extra);
    }

    void PlaceHoldNoteLayer(Transform root, string name, Texture2D tex, float len, float tileH, float u0, float u1, int queue)
    {
        tex.wrapMode = TextureWrapMode.Repeat;
        Transform layer = null;
        for (var i = 0; i < root.childCount; i++)
        {
            if (root.GetChild(i).name == name) { layer = root.GetChild(i); break; }
        }
        if (layer == null)
            layer = SpriteQuad(root, tex, 1f, name);
        layer.gameObject.SetActive(true);
        var r = layer.GetComponent<Renderer>();
        if (r != null)
        {
            var sh = Shader.Find("MusePSona/MdHoldNote") ?? Shader.Find("MusePSona/MdSprite");
            if (sh != null && (r.material == null || r.material.shader != sh))
                r.material = new Material(sh);
            if (r.material != null)
            {
                MdMat.SetTex(r.material, tex);
                MdMat.SetScale(r.material, Vector2.one);
                MdMat.SetOffset(r.material, Vector2.zero);
                if (r.material.HasProperty("_AtlasRect"))
                    r.material.SetVector("_AtlasRect", new Vector4(0f, 0f, 1f, 1f));
                if (r.material.HasProperty("_Color")) r.material.SetColor("_Color", Color.white);
            }
        }
        layer.localPosition = Vector3.zero;
        layer.localRotation = Quaternion.identity;
        layer.localScale = Vector3.one;
        FillHoldNoteMesh(layer, len, tileH, u0, u1);
        PaintLayer(layer, queue, -16);
    }

    static void FillHoldNoteMesh(Transform layer, float len, float h, float u0, float u1)
    {
        var mf = layer.GetComponent<MeshFilter>();
        if (mf == null) return;
        Mesh mesh;
        if (mf.sharedMesh != null && mf.sharedMesh.name == "HoldNoteMesh")
            mesh = mf.mesh;
        else
        {
            mesh = new Mesh { name = "HoldNoteMesh" };
            mf.mesh = mesh;
        }
        var hw = len * 0.5f;
        var hh = h * 0.5f;
        mesh.vertices = new[]
        {
            new Vector3(-hw, -hh, 0f),
            new Vector3(hw, -hh, 0f),
            new Vector3(hw, hh, 0f),
            new Vector3(-hw, hh, 0f)
        };
        mesh.uv = new[]
        {
            new Vector2(u0, 0f),
            new Vector2(u1, 0f),
            new Vector2(u1, 1f),
            new Vector2(u0, 1f)
        };
        mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
        mesh.RecalculateBounds();
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

    static void SetActorAlpha(Actor a, float alpha)
    {
        alpha = Mathf.Clamp01(alpha);
        ApplyAlpha(a.Body, alpha);
        ApplyAlpha(a.Extra, alpha);
        ApplyAlpha(a.Head, alpha);
        ApplyAlpha(a.Tail, alpha);
    }

    static void ApplyAlpha(Transform t, float alpha)
    {
        if (t == null) return;
        foreach (var r in t.GetComponentsInChildren<Renderer>(true))
        {
            if (r == null || r.material == null) continue;
            var c = r.material.color;
            c.a = alpha;
            r.material.color = c;
        }
    }
}

