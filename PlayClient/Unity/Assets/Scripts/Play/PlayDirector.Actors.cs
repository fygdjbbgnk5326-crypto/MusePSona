using System.Collections.Generic;
using System.IO;
using System.Linq;
using MusePSona.Play;
using UnityEngine;

/// <summary>飞近物件的实例、资源与渲染层管理。</summary>
public sealed partial class PlayDirector
{
    sealed class Actor
    {
        public Transform Root;
        public Transform Body;
        public Transform Extra;
        public Transform Head;
        public Transform Tail;
        public NoteCategory Category;
        public NoteSpecial Special;
        public float Spin;
        public Material Tint;
        public bool LooksSprite;
        public Vector3 BaseScale;
        public MdSpineView Spine;
        public string BmsId;
        public decimal TimeSec;
        public bool OutAnchored;
        public Vector3 OutRootPos;
        public float HitBodyY;
        public bool HitBodyYReady;
        public bool HitOnTrack;
        public bool GearOnLine;
        public float FlyScale;
        public string LooksScene;
        public string InClip;
        public float InClipDuration;
        public bool InClipReady;
        internal HoldLayer HoldNoteUp;
        internal HoldLayer HoldNoteDown;
        public float Alpha = -1f;
        public Texture2D HoldNoteUpTexture;
        public Texture2D HoldNoteDownTexture;
        public bool HoldNoteTexturesReady;
        public bool HoldNoteLayoutReady;
    }

    Actor SpawnActor(PlayNote n)
    {
        var root = new GameObject(ActorName(n)).transform;
        var a = new Actor
        {
            Root = root,
            Category = n.Category,
            Special = n.Special,
            BmsId = n.BmsId,
            TimeSec = n.TimeSec
        };
        a.LooksScene = _scene;
        var lane = n.Pathway == 1 ? _matAir : _matRoad;
        if (TryLooks(a, n))
        {
            if (n.Special == NoteSpecial.Pendulum)
                a.Extra = PendulumRope(root, n.Inverted);
            ApplyActorLayer(a);
            return a;
        }

        if (n.Special == NoteSpecial.Pendulum)
        {
            a.Body = Prim(PrimitiveType.Sphere, root, Vector3.one * 0.48f, _matPendulum, "head");
            a.Extra = PendulumRope(root, n.Inverted);
            ApplyActorLayer(a);
            return a;
        }

        if (n.Special == NoteSpecial.Raider)
        {
            a.Body = Prim(PrimitiveType.Cube, root, new Vector3(0.58f, 0.22f, 0.16f), _matRaider, "body");
            a.Extra = Prim(PrimitiveType.Cube, root, new Vector3(0.42f, 0.08f, 0.08f), lane, "tail");
            a.Extra.localPosition = new Vector3(0.42f, n.Inverted ? -0.12f : 0.12f, 0.02f);
            ApplyActorLayer(a);
            return a;
        }

        switch (n.Category)
        {
            case NoteCategory.Hold:
                a.Body = Prim(PrimitiveType.Cube, root, new Vector3(1f, MdLooks.HoldBarBox, 0.12f), _matHold, "bar");
                a.Head = Prim(PrimitiveType.Cube, root, Vector3.one * MdLooks.HoldEndBox, lane, "head");
                a.Tail = Prim(PrimitiveType.Cube, root, Vector3.one * MdLooks.HoldEndBox, lane, "tail");
                a.Head.rotation = Quaternion.Euler(0f, 0f, 45f);
                a.Tail.rotation = Quaternion.Euler(0f, 0f, 45f);
                break;
            case NoteCategory.Gear:
                a.Body = Prim(PrimitiveType.Cylinder, root, new Vector3(0.42f, 0.07f, 0.42f), _matGear, "disc");
                a.Body.localRotation = Quaternion.Euler(90f, 0f, 0f);
                for (var i = 0; i < 8; i++)
                {
                    var rad = i * Mathf.PI * 0.25f;
                    var tooth = Prim(PrimitiveType.Cube, a.Body, new Vector3(0.09f, 0.08f, 0.16f), _matGear, "tooth" + i);
                    tooth.localPosition = new Vector3(Mathf.Cos(rad) * 0.24f, 0f, Mathf.Sin(rad) * 0.24f);
                    tooth.localRotation = Quaternion.Euler(0f, -i * 45f, 0f);
                }
                a.Extra = Prim(PrimitiveType.Cylinder, root, new Vector3(0.22f, 0.09f, 0.22f), _matGearCore, "hub");
                a.Extra.localRotation = Quaternion.Euler(90f, 0f, 0f);
                break;
            case NoteCategory.Masher:
                a.Body = Prim(PrimitiveType.Sphere, root, Vector3.one * 0.78f, _matMasher, "body");
                a.Extra = Prim(PrimitiveType.Sphere, root, Vector3.one * 0.42f, _matGear, "ring");
                break;
            case NoteCategory.Boss:
                a.Body = Prim(PrimitiveType.Cube, root, Vector3.one * 0.62f, _matBoss, "body");
                break;
            case NoteCategory.Ghost:
                a.Tint = NewFadeMat(new Color(0.72f, 0.88f, 0.95f, 1f));
                a.Body = Prim(PrimitiveType.Sphere, root, Vector3.one * 0.4f, a.Tint, "body");
                break;
            case NoteCategory.Heart:
                a.Body = Prim(PrimitiveType.Sphere, root, Vector3.one * 0.32f, _matHeart, "lobe");
                a.Extra = Prim(PrimitiveType.Sphere, root, Vector3.one * 0.32f, _matHeart, "lobe2");
                a.Extra.localPosition = new Vector3(0.16f, 0f, 0f);
                break;
            case NoteCategory.Music:
                a.Body = Prim(PrimitiveType.Cube, root, new Vector3(0.14f, 0.48f, 0.14f), _matMusic, "stem");
                a.Extra = Prim(PrimitiveType.Sphere, root, Vector3.one * 0.22f, _matMusic, "note");
                a.Extra.localPosition = new Vector3(-0.12f, -0.18f, 0f);
                break;
            default:
                if (n.BmsId == "0E")
                {
                    a.Body = Prim(PrimitiveType.Sphere, root, Vector3.one * 0.34f, lane, "a");
                    a.Extra = Prim(PrimitiveType.Sphere, root, Vector3.one * 0.34f, lane, "b");
                    a.Extra.localPosition = new Vector3(0f, 0.30f, 0.02f);
                    break;
                }
                var s = MonsterScale(n.BmsId);
                a.Body = Prim(PrimitiveType.Cube, root, Vector3.one * s, lane, "body");
                break;
        }
        ApplyActorLayer(a);
        return a;
    }

    static void PaintLayer(Transform t, int queue, int order)
    {
        if (t == null) return;
        foreach (var r in t.GetComponentsInChildren<Renderer>(true))
        {
            if (r == null) continue;
            r.sortingOrder = order;
            var mat = r.sharedMaterial;
            if (mat != null) mat.renderQueue = queue;
        }
    }

    static void ApplyActorLayer(Actor a)
    {
        if (a.Category == NoteCategory.Hold)
        {
            PaintLayer(a.Body, 2800, -20);
            PaintLayer(a.Head, 2820, -12);
            PaintLayer(a.Tail, 2820, -12);
            return;
        }
        if (a.Category == NoteCategory.Masher)
        {
            PaintLayer(a.Root, 3000, -10);
            if (a.Spine != null) a.Spine.SetLayer(3000, -10);
            return;
        }
        var queue = 3100;
        var order = 4;
        if (a.Category == NoteCategory.Gear)
        {
            queue = 3200;
            order = 12;
        }
        else if (a.Category == NoteCategory.Ghost)
        {
            queue = 3050;
            order = 2;
        }
        if (a.Spine != null) a.Spine.SetLayer(queue, order);
        PaintLayer(a.Root, queue, order);
    }

    bool TryLooks(Actor a, PlayNote n)
    {
        if (IsHoldStar(n))
            return TrySpriteActor(a, n);
        return TrySpineActor(a, n) || TrySpriteActor(a, n);
    }

    static bool IsHoldStar(PlayNote n) => n.BmsId == MuseDashChart.BmsTables.IdHoldStar;

    bool TrySpineActor(Actor a, PlayNote n)
    {
        var pack = MdSpine.Load(string.IsNullOrEmpty(_scene) ? "scene_01" : _scene, n);
        if (pack == null) return false;
        a.Spine = MdSpineView.Attach(a.Root, pack);
        return true;
    }

    bool TrySpriteActor(Actor a, PlayNote n)
    {
        var pack = _looks ?? MdLooks.For(_scene);
        if (pack == null) return false;
        var star = IsHoldStar(n);
        var key = star
            ? ((n.Pathway == 1 ? "air" : "ground") + "_hold_head")
            : MdLooks.SpriteKey(n);
        if (!pack.Sprites.TryGetValue(key, out var tex)) return false;
        a.LooksSprite = true;
        var box = star ? MdLooks.HoldEndBox : MdLooks.SpriteBox(n);
        if (n.Category == NoteCategory.Hold)
        {
            var lane = n.Pathway == 1 ? "air" : "ground";
            pack.Sprites.TryGetValue(lane + "_hold_body", out var bar);
            a.Body = SpriteQuad(a.Root, bar ?? tex, 1f, "bar");
            a.Head = SpriteQuad(a.Root, tex, MdLooks.HoldEndBox, "head");
            a.Tail = SpriteQuad(a.Root, tex, MdLooks.HoldEndBox, "tail");
            a.BaseScale = a.Head.localScale;
            return true;
        }
        a.Body = SpriteQuad(a.Root, tex, box, "body");
        a.BaseScale = a.Body.localScale;
        if (n.Category == NoteCategory.Ghost)
            a.Tint = a.Body.GetComponent<Renderer>().material;
        return true;
    }

    Transform SpriteQuad(Transform parent, Texture2D tex, float box, string name)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = name;
        go.transform.SetParent(parent, false);
        var col = go.GetComponent<Collider>();
        if (col) Destroy(col);
        var aspect = tex.height > 0 ? (float)tex.width / tex.height : 1f;
        go.transform.localScale = FitBox(box, aspect);
        var sh = Shader.Find("MusePSona/MdSprite")
            ?? Shader.Find("MusePSona/SpinePMA")
            ?? Shader.Find("Sprites/Default")
            ?? Shader.Find("Unlit/Transparent");
        var mat = new Material(sh);
        MdMat.SetTex(mat, tex);
        mat.color = Color.white;
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", Color.white);
        if (mat.HasProperty("_UseClip")) mat.SetFloat("_UseClip", 0f);
        if (mat.HasProperty("_ClipValue")) mat.SetFloat("_ClipValue", 1f);
        if (mat.HasProperty("_ClipMirror")) mat.SetFloat("_ClipMirror", 0f);
        mat.renderQueue = 3100;
        var r = go.GetComponent<Renderer>();
        if (r != null)
        {
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.material = mat;
        }
        return go.transform;
    }

    static Vector3 FitBox(float box, float aspect)
    {
        if (aspect < 1e-4f) aspect = 1f;
        return aspect >= 1f
            ? new Vector3(box, box / aspect, 1f)
            : new Vector3(box * aspect, box, 1f);
    }

    /// <summary>
    /// 头尾星钉在轨道线上，并绕贴图重心自转。0102_*_top 不透明重心约在几何中心下方 5.5/149，
    /// 绕四边形中心转会把星星甩离轨道。
    /// </summary>
    static void PlaceHoldStar(Transform star, float x, float y, float box, float spin)
    {
        if (star == null) return;
        var aspect = 1f;
        var rend = star.GetComponent<Renderer>();
        var tex = rend != null ? MdMat.Tex(rend.material) : null;
        if (tex != null && tex.height > 0) aspect = (float)tex.width / tex.height;
        var h = aspect >= 1f ? box / Mathf.Max(1e-4f, aspect) : box;
        var dy = -(5.5f / 149f) * h;
        var rot = Quaternion.Euler(0f, 0f, spin);
        star.rotation = rot;
        star.position = new Vector3(x, y, 0.20f) - rot * new Vector3(0f, dy, 0f);
    }

    static void FitTransformBox(Transform t, float box)
    {
        if (t == null) return;
        var r = t.GetComponent<Renderer>();
        var tex = r != null ? MdMat.Tex(r.material) : null;
        float aspect = 1f;
        if (tex != null && tex.height > 0) aspect = (float)tex.width / tex.height;
        else if (Mathf.Abs(t.localScale.y) > 1e-6f) aspect = t.localScale.x / t.localScale.y;
        t.localScale = FitBox(box, aspect);
    }

    static void DropActor(Actor a)
    {
        if (a.LooksSprite && a.Root != null)
        {
            foreach (var r in a.Root.GetComponentsInChildren<Renderer>())
            {
                if (r == null || r.sharedMaterial == null) continue;
                var material = r.sharedMaterial;
                r.sharedMaterial = null;
                Destroy(material);
            }
        }
        else if (a.Extra != null)
        {
            var lr = a.Extra.GetComponent<LineRenderer>();
            if (lr != null) Destroy(lr.material);
        }
        DestroyHoldLayerResources(a.HoldNoteUp);
        DestroyHoldLayerResources(a.HoldNoteDown);
        if (a.Root != null) Destroy(a.Root.gameObject);
        if (a.Tint != null && !a.LooksSprite) Destroy(a.Tint);
    }

    static void DestroyHoldLayerResources(HoldLayer layer)
    {
        if (layer == null) return;
        if (layer.Transform != null)
        {
            var r = layer.Transform.GetComponent<Renderer>();
            if (r != null && r.sharedMaterial != null)
            {
                var material = r.sharedMaterial;
                r.sharedMaterial = null;
                Destroy(material);
            }
        }
        if (layer.Mesh != null) Destroy(layer.Mesh);
    }

    /// <summary>销毁 SpriteQuad 及它独占的运行时材质。</summary>
    static void DestroyOwnedVisual(Transform t)
    {
        if (t == null) return;
        var materials = new HashSet<Material>();
        foreach (var r in t.GetComponentsInChildren<Renderer>(true))
        {
            if (r == null || r.sharedMaterial == null) continue;
            var material = r.sharedMaterial;
            r.sharedMaterial = null;
            if (materials.Add(material)) Destroy(material);
        }
        Destroy(t.gameObject);
    }

    static string ActorName(PlayNote n)
    {
        var lane = n.Pathway == 1 ? "air" : "road";
        if (n.Special != NoteSpecial.None) return lane + "_" + n.Special;
        return lane + "_" + n.Category;
    }

    static float MonsterScale(string id) => id switch
    {
        "01" or "02" or "03" => 0.32f,
        "0A" or "0B" => 0.55f,
        _ => 0.42f
    };

    static Transform PendulumRope(Transform parent, bool inverted)
    {
        var go = new GameObject("rope");
        go.transform.SetParent(parent, false);
        var lr = go.AddComponent<LineRenderer>();
        lr.positionCount = 2;
        lr.useWorldSpace = true;
        lr.startWidth = 0.055f;
        lr.endWidth = 0.028f;
        lr.numCapVertices = 5;
        lr.alignment = LineAlignment.View;
        lr.textureMode = LineTextureMode.Stretch;
        var sh = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
        var mat = new Material(sh);
        var col = inverted ? new Color(0.78f, 0.42f, 0.88f) : new Color(0.52f, 0.28f, 0.68f);
        mat.color = col;
        MdMat.SetTex(mat, Texture2D.whiteTexture);
        lr.material = mat;
        lr.startColor = col;
        lr.endColor = col;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
        lr.sortingOrder = 4;
        return go.transform;
    }

    static Transform Prim(PrimitiveType type, Transform parent, Vector3 scale, Material mat, string name)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localScale = scale;
        var col = go.GetComponent<Collider>();
        if (col) Destroy(col);
        var r = go.GetComponent<Renderer>();
        if (r != null && mat != null) r.sharedMaterial = mat;
        return go.transform;
    }

    Vector3 Stage(float x, float yDown, float z = 0f)
        => new Vector3(x * worldScale, -yDown * worldScale, z);
}

