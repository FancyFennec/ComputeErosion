using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ErodingTerrain : MonoBehaviour
{
    public RenderTexture heightMap;
    public int resolution = 1024;

    void Start()
    {
        Renderer rend = GetComponent<Renderer>();
        rend.sharedMaterial.SetTexture("Texture2D_f24a80a3f47f4c20844d82524f9db08d", heightMap);
    }

    void Update()
    {
    }
}
