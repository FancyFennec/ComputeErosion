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
    ComputeShader waterAndVelocityShader;
    ComputeShader addWaterShader;
    ComputeShader erosionAndDecompositionShader;
    ComputeShader sedimentTransportAndEvaporationShader;

    int perlinNoiseHandle;
    int fluxHandle;
    int waterAndVelocityHandle;
    int addWaterHandle;
    int erosionAndDecompositionHandle;
    int sedimentTransportAndEvaporationHandle;

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
		perlinNoiseShader.SetTexture(perlinNoiseHandle, "height", height);
		perlinNoiseShader.SetInt("resolution", resolution);
		perlinNoiseShader.Dispatch(perlinNoiseHandle, resolution / 8, resolution / 8, 1);

		rend = terrain.GetComponent<Renderer>();
		rend.enabled = true;
		rend.sharedMaterial.SetTexture("Texture2D_f24a80a3f47f4c20844d82524f9db08d", height);
	}

	private void InitialiseShaders()
	{
		perlinNoiseShader = (ComputeShader)Resources.Load("PerlinNoise");
		fluxShader = (ComputeShader)Resources.Load("Flux");
		waterAndVelocityShader = (ComputeShader)Resources.Load("WaterAndVelocity");
		addWaterShader = (ComputeShader)Resources.Load("AddWater");
        erosionAndDecompositionShader = (ComputeShader)Resources.Load("ErosionAndDecomposition");
        sedimentTransportAndEvaporationShader = (ComputeShader)Resources.Load("SedimentTransportationAndEvaporation");

        perlinNoiseHandle = perlinNoiseShader.FindKernel("CSMain");
        fluxHandle = fluxShader.FindKernel("CSMain");
        waterAndVelocityHandle = waterAndVelocityShader.FindKernel("CSMain");
        addWaterHandle = addWaterShader.FindKernel("CSMain");
        erosionAndDecompositionHandle = erosionAndDecompositionShader.FindKernel("CSMain");
        sedimentTransportAndEvaporationHandle = sedimentTransportAndEvaporationShader.FindKernel("CSMain");
    }

	private void OnSceneGUI()
    {
        UpdateDTime();

        if (!editingTerrain)
		{
            if (flux == null)
            {
                ResetFlux();
                ResetWater();
                ResetSediment();
                ResetSetVel();

                addWaterShader.SetTexture(addWaterHandle, "water", water);

                fluxShader.SetTexture(fluxHandle, "height", height);
                fluxShader.SetTexture(fluxHandle, "flux", flux);
                fluxShader.SetTexture(fluxHandle, "water", water);
                fluxShader.SetInt("resolution", resolution);

                waterAndVelocityShader.SetTexture(waterAndVelocityHandle, "flux", flux);
                waterAndVelocityShader.SetTexture(waterAndVelocityHandle, "water", water);
                waterAndVelocityShader.SetTexture(waterAndVelocityHandle, "vel", vel);
                waterAndVelocityShader.SetInt("resolution", resolution);

                erosionAndDecompositionShader.SetTexture(erosionAndDecompositionHandle, "height", height);
                erosionAndDecompositionShader.SetTexture(erosionAndDecompositionHandle, "vel", vel);
                erosionAndDecompositionShader.SetTexture(erosionAndDecompositionHandle, "water", water);
                erosionAndDecompositionShader.SetTexture(erosionAndDecompositionHandle, "sed", sediment);

                sedimentTransportAndEvaporationShader.SetTexture(sedimentTransportAndEvaporationHandle, "water", water);
                sedimentTransportAndEvaporationShader.SetTexture(sedimentTransportAndEvaporationHandle, "vel", vel);
                sedimentTransportAndEvaporationShader.SetTexture(sedimentTransportAndEvaporationHandle, "sed", sediment);
                sedimentTransportAndEvaporationShader.SetInt("resolution", resolution);

                rend.sharedMaterial.SetTexture("Texture2D_6e077698234c43b5858f766869fb4614", water);
            }

            addWaterShader.Dispatch(addWaterHandle, resolution / 8, resolution / 8, 1);

            fluxShader.SetFloat("dTime", dTime);
            fluxShader.Dispatch(fluxHandle, resolution / 8, resolution / 8, 1);

            waterAndVelocityShader.SetFloat("dTime", dTime);
            waterAndVelocityShader.Dispatch(waterAndVelocityHandle, resolution / 8, resolution / 8, 1);

            erosionAndDecompositionShader.SetFloat("dTime", dTime);
            erosionAndDecompositionShader.Dispatch(erosionAndDecompositionHandle, resolution / 8, resolution / 8, 1);

            sedimentTransportAndEvaporationShader.SetFloat("dTime", dTime);
            sedimentTransportAndEvaporationShader.Dispatch(sedimentTransportAndEvaporationHandle, resolution / 8, resolution / 8, 1);
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

		perlinNoiseShader.Dispatch(perlinNoiseHandle, resolution / 8, resolution / 8, 1);
	}

	private void RenderResolutionField()
	{
		int newRes = EditorGUILayout.IntField("Resolution ", resolution);
        if(newRes != resolution)
		{
            ResetHeight();
            perlinNoiseShader.SetTexture(perlinNoiseHandle, "height", height);

            resolution = newRes;
            perlinNoiseShader.SetInt("resolution", resolution);

            rend.sharedMaterial.SetTexture("Texture2D_f24a80a3f47f4c20844d82524f9db08d", height);
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
        perlinNoiseShader.SetBuffer(perlinNoiseHandle, "octaves", octavesComputeBuffer);
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
		//dTime = currentTime - lastTime;
        dTime = 1000.0f / 60f;
        lastTime = currentTime;
    }

    private void ResetHeight()
    {
        if (height != null) height.Release();
        height = new RenderTexture(resolution, resolution, 32, RenderTextureFormat.RFloat) { enableRandomWrite = true };
        height.Create();
    }

    private void ResetWater()
    {
        if (water != null) water.Release();
        water = new RenderTexture(resolution, resolution, 32, RenderTextureFormat.RGFloat) { enableRandomWrite = true };
        water.Create();
    }

    private void ResetSediment()
    {
        if (sediment != null) sediment.Release();
        sediment = new RenderTexture(resolution, resolution, 32, RenderTextureFormat.RGFloat) { enableRandomWrite = true };
        sediment.Create();
    }

    private void ResetFlux()
    {
        if (flux != null) flux.Release();
        flux = new RenderTexture(resolution, resolution, 32, RenderTextureFormat.ARGBFloat) { enableRandomWrite = true };
        flux.Create();
    }

    private void ResetSetVel()
    {
        if (vel != null) vel.Release();
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
