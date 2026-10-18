using UnityEngine;

/// <summary>Unlit/Color 没有 _MainTex，不要碰 Material.mainTexture。</summary>
public static class MdMat
{
    public static Texture Tex(Material m)
    {
        if (m == null || !m.HasProperty("_MainTex")) return null;
        return m.GetTexture("_MainTex");
    }

    public static void SetTex(Material m, Texture tex)
    {
        if (m == null || !m.HasProperty("_MainTex")) return;
        m.SetTexture("_MainTex", tex);
    }

    public static void SetScale(Material m, Vector2 scale)
    {
        if (m == null || !m.HasProperty("_MainTex")) return;
        m.SetTextureScale("_MainTex", scale);
    }

    public static void SetOffset(Material m, Vector2 offset)
    {
        if (m == null || !m.HasProperty("_MainTex")) return;
        m.SetTextureOffset("_MainTex", offset);
    }
}
