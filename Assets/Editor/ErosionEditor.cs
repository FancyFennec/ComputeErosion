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
    ComputeShader shader;
    List<Octave> octaves = new List<Octave>() { new Octave(1f, 1f) };
    ComputeBuffer computeBuffer;

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

        rend.sharedMaterial.SetTexture("Texture2D_f24a80a3f47f4c20844d82524f9db08d", terrain.heightMap);
    }

    public override void OnInspectorGUI()
	{
		terrain.resolution = EditorGUILayout.IntField("Resolution ", terrain.resolution);

		renderOctaveSliders();

		shader.SetInt("resolution", terrain.resolution);
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
        if (terrain.heightMap != null) terrain.heightMap.Release();
        terrain.heightMap = new RenderTexture(terrain.resolution, terrain.resolution, 32) { enableRandomWrite = true };
        terrain.heightMap.format = RenderTextureFormat.RFloat;
        terrain.heightMap.Create();

        shader.SetTexture(kernelHandle, "heightMap", terrain.heightMap);

        if(computeBuffer != null)
		{
            computeBuffer.Dispose();

        }
        computeBuffer = new ComputeBuffer(octaves.Count, sizeof(float) * 2);
        computeBuffer.SetData(octaves);
        shader.SetBuffer(kernelHandle, "octaves", computeBuffer);
        shader.SetInt("octaveCount", octaves.Count);
    }
}
