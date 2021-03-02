using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ComputeShaderTest : MonoBehaviour
{

    public ComputeShader shader;

    int resolution = 1024;

    Renderer rend;
    public RenderTexture materialTex;
    RenderTexture fluxTex;
    RenderTexture velTex;

    int kernelHandle;

    void Start()
    {
        materialTex = new RenderTexture(resolution, resolution, 32) { enableRandomWrite = true };
        materialTex.format = RenderTextureFormat.ARGBFloat;
        materialTex.Create();

        fluxTex = new RenderTexture(resolution, resolution, 24) { enableRandomWrite = true };
        fluxTex.Create();

        velTex = new RenderTexture(resolution, resolution, 24) { enableRandomWrite = true };
        velTex.Create();

        rend = GetComponent<Renderer>();
        rend.enabled = true;

        kernelHandle = shader.FindKernel("CSMain");
    }

    // Update is called once per frame
    void Update()
    {
        shader.SetFloat("time", Time.realtimeSinceStartup);
        shader.SetTexture(kernelHandle, "material", materialTex);
        shader.SetTexture(kernelHandle, "flux", fluxTex);
        shader.SetTexture(kernelHandle, "vel", velTex);
        shader.Dispatch(kernelHandle, resolution / 8, resolution / 8, 1);

        rend.material.SetTexture("Texture2D_f24a80a3f47f4c20844d82524f9db08d", materialTex);
    }
}
