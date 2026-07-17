using UnityEngine;
using Verse;

namespace RimSynapse.Factions
{
    public static class TextureUtility
    {
        public static Texture2D MakeTextureReadableAndTransparent(Texture2D originalTex)
        {
            if (originalTex == null) return null;

            RenderTexture rt = RenderTexture.GetTemporary(
                originalTex.width,
                originalTex.height,
                0,
                RenderTextureFormat.Default,
                RenderTextureReadWrite.Linear);

            Graphics.Blit(originalTex, rt);
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = rt;

            Texture2D readableText = new Texture2D(originalTex.width, originalTex.height);
            readableText.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            readableText.Apply();

            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);

            Color[] pixels = readableText.GetPixels();
            for (int i = 0; i < pixels.Length; i++)
            {
                float brightness = pixels[i].r + pixels[i].g + pixels[i].b;
                if (brightness < 0.15f)
                {
                    pixels[i] = Color.clear;
                }
                else
                {
                    float gray = brightness / 3f;
                    pixels[i] = new Color(1f, 1f, 1f, gray);
                }
            }
            readableText.SetPixels(pixels);
            readableText.Apply();
            return readableText;
        }
    }
}
