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
    ComputeShader perlinNoiseShader;
    List<Octave> octaves = new List<Octave>() { new Octave(1f, 1f) };
    ComputeBuffer computeBuffer;

    Renderer rend;
    RenderTexture heightMap;

    int kernelHandle;

    private void OnEnable()
	{
		terrain = (ErodingTerrain)target;
        perlinNoiseShader = (ComputeShader)Resources.Load("PerlinNoiseComputeShader");
        kernelHandle = perlinNoiseShader.FindKernel("CSMain");

        initialiseHeightMap();

        rend = terrain.GetComponent<Renderer>();
		rend.enabled = true;
	}

	private void OnSceneGUI()
    {
        perlinNoiseShader.SetFloat("time", (float) EditorApplication.timeSinceStartup);

        perlinNoiseShader.Dispatch(kernelHandle, resolution / 8, resolution / 8, 1);

        rend.sharedMaterial.SetTexture("Texture2D_f24a80a3f47f4c20844d82524f9db08d", heightMap);
    }

    public override void OnInspectorGUI()
	{
		resolution = EditorGUILayout.IntField("Resolution ", resolution);

		renderOctaveSliders();

		perlinNoiseShader.SetInt("resolution", resolution);

		initialiseHeightMap();
    }

	private void renderOctaveSliders()
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
                if (GUILayout.Button("-"))
                {
                    octaves.RemoveAt(index);
                }
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

	private void initialiseHeightMap()
    {
        if (heightMap != null) heightMap.Release();
        heightMap = new RenderTexture(resolution, resolution, 32, RenderTextureFormat.RFloat) { enableRandomWrite = true };
        heightMap.Create();

        perlinNoiseShader.SetTexture(kernelHandle, "heightMap", heightMap);

        if(computeBuffer != null)
		{
            computeBuffer.Dispose();

        }
        computeBuffer = new ComputeBuffer(octaves.Count, sizeof(float) * 2);
        computeBuffer.SetData(octaves);
        perlinNoiseShader.SetBuffer(kernelHandle, "octaves", computeBuffer);
        perlinNoiseShader.SetInt("octaveCount", octaves.Count);
    }
}
