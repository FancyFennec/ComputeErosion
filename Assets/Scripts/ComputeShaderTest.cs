using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ComputeShaderTest : MonoBehaviour
{

    public ComputeShader shader;

    int resolution = 1024;

    Renderer rend;
    public RenderTexture heightMap;

    int kernelHandle;

    void Start()
    {
        heightMap = new RenderTexture(resolution, resolution, 32) { enableRandomWrite = true };
        heightMap.format = RenderTextureFormat.RFloat;
        heightMap.Create();

        rend = GetComponent<Renderer>();
        rend.enabled = true;

        kernelHandle = shader.FindKernel("CSMain");
    }

    // Update is called once per frame
    void Update()
    {
        shader.SetFloat("time", Time.realtimeSinceStartup);
        shader.SetInt("resolution", resolution);
        shader.SetTexture(kernelHandle, "heightMap", heightMap);

        shader.Dispatch(kernelHandle, resolution / 8, resolution / 8, 1);

        rend.material.SetTexture("Texture2D_f24a80a3f47f4c20844d82524f9db08d", heightMap);
    }
}
