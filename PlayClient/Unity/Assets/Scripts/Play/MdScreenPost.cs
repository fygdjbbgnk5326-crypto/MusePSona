using UnityEngine;

/// <summary>对相机成片做滤镜。叠一层色块采不到画面，像素、黑白、色散对不上官机。</summary>
public sealed class MdScreenPost : MonoBehaviour
{
    public Material Mat;

    void OnRenderImage(RenderTexture src, RenderTexture dst)
    {
        if (Mat == null || !Mat.shader.isSupported)
        {
            Graphics.Blit(src, dst);
            return;
        }
        Graphics.Blit(src, dst, Mat);
    }
}
