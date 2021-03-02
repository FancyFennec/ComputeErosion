using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ErodingTerrain))]
public class ErosionEditor : Editor
{
    ErodingTerrain terrain;
    ComputeShader shader;

    Renderer rend;

    int kernelHandle;

    private void OnEnable()
	{
		terrain = (ErodingTerrain)target;
        shader = (ComputeShader)Resources.Load("PerlinNoiseComputeShader");
        kernelHandle = shader.FindKernel("CSMain");

        initialiseHeightMap();

        rend = terrain.GetComponent<Renderer>();
		rend.enabled = true;
	}

	private void OnSceneGUI()
    {
        shader.SetFloat("time", (float) EditorApplication.timeSinceStartup);

        shader.Dispatch(kernelHandle, terrain.resolution / 8, terrain.resolution / 8, 1);

        rend.material.SetTexture("Texture2D_f24a80a3f47f4c20844d82524f9db08d", terrain.heightMap);
    }

    public override void OnInspectorGUI()
    {
        terrain.resolution = EditorGUILayout.IntField("Resolution ", terrain.resolution);

        shader.SetInt("resolution", terrain.resolution);
        initialiseHeightMap();
    }

    private void initialiseHeightMap()
    {
        terrain.heightMap = new RenderTexture(terrain.resolution, terrain.resolution, 32) { enableRandomWrite = true };
        terrain.heightMap.format = RenderTextureFormat.RFloat;
        terrain.heightMap.Create();

        shader.SetTexture(kernelHandle, "heightMap", terrain.heightMap);
    }
}
