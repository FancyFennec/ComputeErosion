using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ErodingTerrain))]
public class ErosionEditor : Editor
{
    struct Octave
    {
        public Octave(float frequency, float amplitude)
		{
            this.frequency = frequency;
            this.amplitude = amplitude;
		}
               
        public float frequency;
        public float amplitude;
    };

    ErodingTerrain terrain;
    public int resolution = 1024;
    float xOffset = 0f;
    float yOffset = 0f;
    ComputeShader perlinNoiseShader;
    ComputeShader erosionShader;

    List<Octave> octaves = new List<Octave>() { new Octave(1f, 1f) };
    ComputeBuffer octavesComputeBuffer;

    Renderer rend;
    RenderTexture heightMap;

    int pNKernelHandle;
    int eKernelHandle;

    private void OnEnable()
	{
		terrain = (ErodingTerrain)target;
        perlinNoiseShader = (ComputeShader)Resources.Load("PerlinNoiseComputeShader");
        erosionShader = (ComputeShader)Resources.Load("ErosionComputeShader");
        pNKernelHandle = perlinNoiseShader.FindKernel("CSMain");
        eKernelHandle = erosionShader.FindKernel("CSMain");

        SetHeightMap();
        SetOctaves();
        SetOffset();

        rend = terrain.GetComponent<Renderer>();
		rend.enabled = true;
	}

	private void OnSceneGUI()
    {
    }

    public override void OnInspectorGUI()
	{
		resolution = EditorGUILayout.IntField("Resolution ", resolution);
        RenderOffsetSliders();
        RenderOctaveSliders();

        perlinNoiseShader.SetInt("resolution", resolution);
		SetHeightMap();
        SetOffset();
        SetOctaves();

        perlinNoiseShader.Dispatch(pNKernelHandle, resolution / 8, resolution / 8, 1);
        rend.sharedMaterial.SetTexture("Texture2D_f24a80a3f47f4c20844d82524f9db08d", heightMap);
    }

	private void RenderOffsetSliders()
	{
		EditorGUILayout.BeginHorizontal();
		GUILayout.Label("X-Offset");
		xOffset = EditorGUILayout.Slider(xOffset, -5f, +5f);
		GUILayout.Label("Y-Offset");
		yOffset = EditorGUILayout.Slider(yOffset, -5f, +5f);
		EditorGUILayout.EndHorizontal();
	}

	private void RenderOctaveSliders()
	{
        List<Octave> octavesToRender = new List<Octave>(octaves);

        for (int index = 0; index < octavesToRender.Count; index ++)
		{
            Octave octave = octavesToRender[index];
            EditorGUILayout.BeginHorizontal();

            GUILayout.Label("Freq " + index);
            float frequency = Mathf.Exp(EditorGUILayout.Slider(Mathf.Log(octave.frequency + 1), 0f, 5f)) - 1;
            GUILayout.Label("Amp " + index);
            float amplitude = EditorGUILayout.Slider(octave.amplitude, 0f, 1f);

            if (frequency != octave.frequency || amplitude != octave.amplitude)
            {
                octaves[index] = new Octave(frequency, amplitude);
            }

            if (index > 0)
            {
                if (GUILayout.Button("-")) octaves.RemoveAt(index);
            } else
			{
                GUILayout.Label("    ");
            }
            EditorGUILayout.EndHorizontal();
        }

        if (GUILayout.Button("Add Octave")) {
            octaves.Add(new Octave(1f, 1f));
		}
	}

	private void SetHeightMap()
	{
		if (heightMap != null) heightMap.Release();
		heightMap = new RenderTexture(resolution, resolution, 32, RenderTextureFormat.RFloat) { enableRandomWrite = true };
		heightMap.Create();

		perlinNoiseShader.SetTexture(pNKernelHandle, "heightMap", heightMap);
	}

    private void SetOctaves()
	{
		if (octavesComputeBuffer != null) octavesComputeBuffer.Dispose();
		octavesComputeBuffer = new ComputeBuffer(octaves.Count, sizeof(float) * 2);
		octavesComputeBuffer.SetData(octaves);
		perlinNoiseShader.SetBuffer(pNKernelHandle, "octaves", octavesComputeBuffer);
		perlinNoiseShader.SetInt("octaveCount", octaves.Count);
	}

    private void SetOffset()
    {
        perlinNoiseShader.SetFloat("xOffset", xOffset);
        perlinNoiseShader.SetFloat("yOffset", yOffset);
    }
}
