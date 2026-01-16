using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ImageEffect_Generic : MonoBehaviour
{
    private void OnRenderImage(RenderTexture _Source, RenderTexture _Destination)
    {
        // Set the image to draw
        Graphics.Blit(_Source, _Destination, m_Material);
    }

    [SerializeField]
    public Material m_Material = null;
}
