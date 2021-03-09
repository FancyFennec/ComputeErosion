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

    struct Flux
    {
        public Vector4 f;
        public Vector2 v;
    };

    private bool editingTerrain = true;

    ErodingTerrain terrain;

    ComputeShader perlinNoiseShader;
    ComputeShader erosionShader;

    Renderer rend;

    RenderTexture height;
    RenderTexture water;
    RenderTexture sediment;

    List<Octave> octaves = new List<Octave>() { new Octave(1f, 1f) };
    ComputeBuffer octavesComputeBuffer;

    Flux[] flux;
    ComputeBuffer fluxBuffer;

    public int resolution = 1024;
    float xOffset = 0f;
    float yOffset = 0f;

    int pNKernelHandle;
    int erKernelHandle;

    float dTime = 0f;
    float lastTime = 0f;

    private void OnEnable()
	{
        lastTime = (float) EditorApplication.timeSinceStartup;

        terrain = (ErodingTerrain)target;

        perlinNoiseShader = (ComputeShader)Resources.Load("PerlinNoiseComputeShader");
        pNKernelHandle = perlinNoiseShader.FindKernel("CSMain");

        erosionShader = (ComputeShader)Resources.Load("ErosionComputeShader");
        erKernelHandle = erosionShader.FindKernel("CSMain");

        SetHeight();
        SetOctaves();
        SetOffset();

        SetWater();
        SetSediment();

        rend = terrain.GetComponent<Renderer>();
		rend.enabled = true;
	}

	private void OnSceneGUI()
    {
        UpdateDTime();

        if (!editingTerrain)
		{
            if(flux == null) SetFlux();
            erosionShader.SetFloat("dTime", dTime);
            erosionShader.SetInt("resolution", resolution);
            erosionShader.SetTexture(erKernelHandle, "height", height);
            erosionShader.Dispatch(erKernelHandle, resolution / 8, resolution / 8, 1);
        }
    }

    public override void OnInspectorGUI()
	{
		editingTerrain = EditorGUILayout.Toggle("EditTerrain", editingTerrain);

		if (editingTerrain)
		{
            RenderTerrainEditor();
        }
    }

	private void RenderTerrainEditor()
	{
		resolution = EditorGUILayout.IntField("Resolution ", resolution);
		RenderOffsetSliders();
		RenderOctaveSliders();

		perlinNoiseShader.SetInt("resolution", resolution);
		SetHeight();
		SetOffset();
		SetOctaves();

		perlinNoiseShader.Dispatch(pNKernelHandle, resolution / 8, resolution / 8, 1);
        rend.sharedMaterial.SetTexture("Texture2D_f24a80a3f47f4c20844d82524f9db08d", height);
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

	private void SetHeight()
	{
		if (height != null) height.Release();
		height = new RenderTexture(resolution, resolution, 32, RenderTextureFormat.RFloat) { enableRandomWrite = true };
		height.Create();

		perlinNoiseShader.SetTexture(pNKernelHandle, "height", height);
	}

    private void SetWater()
    {
        if (water != null) water.Release();
        water = new RenderTexture(resolution, resolution, 32, RenderTextureFormat.RFloat) { enableRandomWrite = true };
        water.Create();

        erosionShader.SetTexture(erKernelHandle, "water", water);
    }

    private void SetSediment()
    {
        if (sediment != null) sediment.Release();
        sediment = new RenderTexture(resolution, resolution, 32, RenderTextureFormat.RFloat) { enableRandomWrite = true };
        sediment.Create();

        erosionShader.SetTexture(erKernelHandle, "sediment", sediment);
    }

    private void SetOctaves()
	{
		if (octavesComputeBuffer != null) octavesComputeBuffer.Dispose();
		octavesComputeBuffer = new ComputeBuffer(octaves.Count, sizeof(float) * 2);
		octavesComputeBuffer.SetData(octaves);
		perlinNoiseShader.SetBuffer(pNKernelHandle, "octaves", octavesComputeBuffer);
		perlinNoiseShader.SetInt("octaveCount", octaves.Count);
	}

    private void SetFlux()
    {
        flux = new Flux[resolution * resolution];
        if (fluxBuffer != null) fluxBuffer.Dispose();
        fluxBuffer = new ComputeBuffer(resolution * resolution, sizeof(float) * 6);
        fluxBuffer.SetData(flux);
        erosionShader.SetBuffer(erKernelHandle, "flux", fluxBuffer); 
    }

    private void SetOffset()
    {
        perlinNoiseShader.SetFloat("xOffset", xOffset);
        perlinNoiseShader.SetFloat("yOffset", yOffset);
    }

    private void UpdateDTime()
    {
		float newTime = (float)EditorApplication.timeSinceStartup;
		dTime = newTime - lastTime;
        lastTime = newTime;
    }
}
