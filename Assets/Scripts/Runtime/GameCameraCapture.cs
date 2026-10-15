using System;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;

namespace DigitalTennis
{
    /// <summary>Render the running game's own camera to PNG. Does not read the desktop or include IMGUI.</summary>
    public static class GameCameraCapture
    {
        public static void Save(Camera camera, string path, int width = 1280, int height = 800, Camera overlay = null)
        {
            if (camera == null || string.IsNullOrEmpty(path)) throw new ArgumentException("A game camera and output path are required.");
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                throw new InvalidOperationException("Camera capture requires a GPU. Do not use -nographics.");
            var previousTarget = camera.targetTexture;
            var previousActive = RenderTexture.active;
            float previousAspect = camera.aspect;
            var overlayTarget = overlay != null ? overlay.targetTexture : null;
            float overlayAspect = overlay != null ? overlay.aspect : 1;
            var target = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
            var pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = target;
                camera.aspect = (float)width / height;
                camera.Render();
                if (overlay != null && overlay != camera)
                {
                    Canvas.ForceUpdateCanvases();
                    overlay.targetTexture = target; overlay.aspect = (float)width / height; overlay.Render();
                }
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                pixels.Apply();
                File.WriteAllBytes(path, pixels.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = previousTarget;
                camera.aspect = previousAspect;
                if (overlay != null && overlay != camera) { overlay.targetTexture = overlayTarget; overlay.aspect = overlayAspect; }
                RenderTexture.active = previousActive;
                RenderTexture.ReleaseTemporary(target);
                UnityEngine.Object.Destroy(pixels);
            }
        }
    }
}
