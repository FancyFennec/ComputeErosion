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

    private bool editingTerrain = true;

    ErodingTerrain terrain;

    ComputeShader perlinNoiseShader;
    ComputeShader fluxShader;
    ComputeShader waterAndVelShader;
    ComputeShader addWaterShader;

    int pNKernelHandle;
    int fKernelHandle;
    int wAndVKernelHandle;
    int addWKernelHandle;

    Renderer rend;

    RenderTexture height;
    RenderTexture water;
    RenderTexture sediment;
    RenderTexture flux;
    RenderTexture vel;

    List<Octave> octaves = new List<Octave>() { new Octave(1f, 1f) };
    ComputeBuffer octavesComputeBuffer;

    public int resolution = 1024;
    float xOffset = 0f;
    float yOffset = 0f;

    float dTime = 0f;
    float lastTime = 0f;

    private void OnEnable()
	{
		lastTime = (float)EditorApplication.timeSinceStartup;

		terrain = (ErodingTerrain)target;

		InitialiseShaders();

		ResetHeight();
		ResetOctaves();

		SetOffset();
		SetOctaves();
		perlinNoiseShader.SetTexture(pNKernelHandle, "height", height);
		perlinNoiseShader.SetInt("resolution", resolution);
		perlinNoiseShader.Dispatch(pNKernelHandle, resolution / 8, resolution / 8, 1);

		rend = terrain.GetComponent<Renderer>();
		rend.enabled = true;
		rend.sharedMaterial.SetTexture("Texture2D_f24a80a3f47f4c20844d82524f9db08d", height);
	}

	private void InitialiseShaders()
	{
		perlinNoiseShader = (ComputeShader)Resources.Load("PerlinNoiseComputeShader");
		pNKernelHandle = perlinNoiseShader.FindKernel("CSMain");
		fluxShader = (ComputeShader)Resources.Load("FluxComputeShader");
		fKernelHandle = fluxShader.FindKernel("CSMain");
		waterAndVelShader = (ComputeShader)Resources.Load("WaterAndVelComputeShader");
		wAndVKernelHandle = waterAndVelShader.FindKernel("CSMain");
		addWaterShader = (ComputeShader)Resources.Load("AddWaterComputeShader");
		addWKernelHandle = addWaterShader.FindKernel("CSMain");
	}

	private void OnSceneGUI()
    {
        UpdateDTime();

        if (!editingTerrain)
		{
            if (flux == null)
            {
                ResetSetFlux();
                ResetSetWater();
                ResetSetSediment();
                ResetSetVel();

                addWaterShader.SetTexture(addWKernelHandle, "water", water);

                fluxShader.SetTexture(fKernelHandle, "height", height);
                fluxShader.SetTexture(fKernelHandle, "flux", flux);
                fluxShader.SetTexture(fKernelHandle, "water", water);

                waterAndVelShader.SetTexture(fKernelHandle, "flux", flux);
                waterAndVelShader.SetTexture(wAndVKernelHandle, "water", water);
                waterAndVelShader.SetTexture(wAndVKernelHandle, "vel", vel);

                rend.sharedMaterial.SetTexture("Texture2D_6e077698234c43b5858f766869fb4614", water);
            }

            addWaterShader.Dispatch(addWKernelHandle, resolution / 8, resolution / 8, 1);

            fluxShader.SetFloat("dTime", dTime);
            fluxShader.Dispatch(fKernelHandle, resolution / 8, resolution / 8, 1);

            waterAndVelShader.SetFloat("dTime", dTime);
            waterAndVelShader.Dispatch(wAndVKernelHandle, resolution / 8, resolution / 8, 1);
        }
    }

    public override void OnInspectorGUI()
	{
		editingTerrain = EditorGUILayout.Toggle("EditTerrain", editingTerrain);

		if (editingTerrain)
		{
			RenderTerrainEditor();
			ClearFlux();
        }
	}

    private void RenderTerrainEditor()
	{
		RenderResolutionField();
		RenderOffsetSliders();
		RenderOctaveSliders();

		perlinNoiseShader.Dispatch(pNKernelHandle, resolution / 8, resolution / 8, 1);

		rend.sharedMaterial.SetTexture("Texture2D_f24a80a3f47f4c20844d82524f9db08d", height);
	}

	private void RenderResolutionField()
	{
		int newRes = EditorGUILayout.IntField("Resolution ", resolution);
        if(newRes != resolution)
		{
            ResetHeight();
            perlinNoiseShader.SetTexture(pNKernelHandle, "height", height);

            resolution = newRes;
            perlinNoiseShader.SetInt("resolution", resolution);
        }
	}

	private void RenderOffsetSliders()
	{
		EditorGUILayout.BeginHorizontal();
		GUILayout.Label("X-Offset");
		float newXOffset = EditorGUILayout.Slider(xOffset, -5f, +5f);
		GUILayout.Label("Y-Offset");
        float newyOffset = EditorGUILayout.Slider(yOffset, -5f, +5f);
		EditorGUILayout.EndHorizontal();

        if(newXOffset != xOffset || newyOffset != yOffset)
		{
            xOffset = newXOffset;
            yOffset = newyOffset;
            SetOffset();
        }
    }

	private void RenderOctaveSliders()
	{
        List<Octave> octavesToRender = new List<Octave>(octaves);

        bool somethinChanged = false;

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
                somethinChanged = true;
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
            somethinChanged = true;
        }

        if(somethinChanged)
		{
             ResetOctaves();
        SetOctaves();
		}
    }

    private void ResetOctaves()
	{
		if (octavesComputeBuffer != null) octavesComputeBuffer.Dispose();
		octavesComputeBuffer = new ComputeBuffer(octaves.Count, sizeof(float) * 2);
		octavesComputeBuffer.SetData(octaves);
	}

    private void SetOctaves()
    {
        perlinNoiseShader.SetBuffer(pNKernelHandle, "octaves", octavesComputeBuffer);
        perlinNoiseShader.SetInt("octaveCount", octaves.Count);
    }

    private void SetOffset()
    {
        perlinNoiseShader.SetFloat("xOffset", xOffset);
        perlinNoiseShader.SetFloat("yOffset", yOffset);
    }

    private void UpdateDTime()
    {
		float currentTime = (float) EditorApplication.timeSinceStartup;
		dTime = currentTime - lastTime;
        lastTime = currentTime;
    }

    private void ResetHeight()
    {
        if (height != null) height.Release();
        height = new RenderTexture(resolution, resolution, 32, RenderTextureFormat.RFloat) { enableRandomWrite = true };
        height.Create();
    }

    private void ResetSetWater()
    {
        if (water != null) water.Release();
        water = new RenderTexture(resolution, resolution, 32, RenderTextureFormat.RGFloat) { enableRandomWrite = true };
        water.Create();
    }

    private void ResetSetSediment()
    {
        if (sediment != null) sediment.Release();
        sediment = new RenderTexture(resolution, resolution, 32, RenderTextureFormat.RFloat) { enableRandomWrite = true };
        sediment.Create();
    }

    private void ResetSetFlux()
    {
        if (flux != null) flux.Release();
        flux = new RenderTexture(resolution, resolution, 32, RenderTextureFormat.ARGBFloat) { enableRandomWrite = true };
        flux.Create();
    }

    private void ResetSetVel()
    {
        if (vel != null) flux.Release();
        vel = new RenderTexture(resolution, resolution, 32, RenderTextureFormat.RGFloat) { enableRandomWrite = true };
        vel.Create();
    }

    private void ClearFlux()
    {
        if (flux != null)
        {
            flux.Release();
            flux = null;
        }
    }
}
