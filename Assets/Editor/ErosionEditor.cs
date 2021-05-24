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
    ErosionShader fluxShader;
    ErosionShader waterAndVelocityShader;
    ErosionShader addWaterShader;
    ErosionShader erosionAndDecompositionShader;
    ErosionShader sedimentTransportAndEvaporationShader;
    ErosionShader materialTransportShader;

    int perlinNoiseHandle;

    Renderer rend;

    KeyValuePair<string, RenderTexture> height;
    KeyValuePair<string, RenderTexture> water;
    KeyValuePair<string, RenderTexture> sediment;
    KeyValuePair<string, RenderTexture> flux;
    KeyValuePair<string, RenderTexture> terrainFlux;
    KeyValuePair<string, RenderTexture> vel;

    List<Octave> octaves = new List<Octave>() { new Octave(1f, 1f) };
    ComputeBuffer octavesComputeBuffer;

    public int resolution = 1024;
    float xOffset = 0f;
    float yOffset = 0f;

    KeyValuePair<string, float> waterIncrease = new KeyValuePair<string, float>("waterIncrease", 0.0003f);
    KeyValuePair<string, float> evaporationConstant = new KeyValuePair<string, float>("Ke", 0.00003f);

    KeyValuePair<string, float> capacityConstant = new KeyValuePair<string, float>("Kc", 1.9f);
    KeyValuePair<string, float> solubilityConstant = new KeyValuePair<string, float>("Ks", 0.01f);
    KeyValuePair<string, float> depositionConstant = new KeyValuePair<string, float>("Kd", 0.01f);

    float dTime = 1000.0f / 60f;

    private void OnEnable()
	{
		terrain = (ErodingTerrain)target;

		InitialiseHeight();
		InitialiseOctaves();
		InitialiseErosionTextures();
		InitialiseShaders();

		SetPerlinNoiseShaderValues();

		SetRenderShaderTextures();
	}

	private void OnSceneGUI()
    {
        if (!editingTerrain)
        {
            addWaterShader.SetFloat("time", ((float)EditorApplication.timeSinceStartup % 10));
            addWaterShader.Dispatch();

            fluxShader.SetFloat("dTime", dTime);
            fluxShader.Dispatch();

            waterAndVelocityShader.SetFloat("dTime", dTime);
            waterAndVelocityShader.Dispatch();

            erosionAndDecompositionShader.SetFloat("dTime", dTime);
            erosionAndDecompositionShader.Dispatch();

            sedimentTransportAndEvaporationShader.SetFloat("dTime", dTime);
            sedimentTransportAndEvaporationShader.Dispatch();

            materialTransportShader.SetFloat("dTime", dTime);
            materialTransportShader.Dispatch();
        }
    }

    public override void OnInspectorGUI()
	{
        if(editingTerrain != EditorGUILayout.Toggle("EditTerrain", editingTerrain))
		{
            editingTerrain = !editingTerrain;
            if(editingTerrain)
			{
                InitialiseHeight();
                perlinNoiseShader.Dispatch(perlinNoiseHandle, resolution / 8, resolution / 8, 1);
            }
            InitialiseErosionTextures();
        }

		if (editingTerrain)
		{
			RenderTerrainEditor();
        } else
		{
            // TODO: add erosion control
            GUILayout.Label("Water Control");
            GUILayout.Label("Water Increase");
            float newWaterIncrease = EditorGUILayout.Slider(waterIncrease.Value, 0f, 0.005f);
            if (waterIncrease.Value != newWaterIncrease)
            {
                waterIncrease = new KeyValuePair<string, float>(waterIncrease.Key, newWaterIncrease);
                addWaterShader.SetFloat(waterIncrease);
            }
            GUILayout.Label("Evaporation");
            float newKe = EditorGUILayout.Slider(evaporationConstant.Value, 0.0000001f, 0.001f);
            if(evaporationConstant.Value != newKe)
			{
                evaporationConstant = new KeyValuePair<string, float>(evaporationConstant.Key, newKe);
                sedimentTransportAndEvaporationShader.SetFloat(evaporationConstant);
            }
            GUILayout.Label("Erosion Control");
            GUILayout.Label("Capacity");
            float newCapacity = EditorGUILayout.Slider(capacityConstant.Value, 0.0000001f, 3f);
            if (capacityConstant.Value != newCapacity)
            {
                capacityConstant = new KeyValuePair<string, float>(capacityConstant.Key, newCapacity);
                erosionAndDecompositionShader.SetFloat(capacityConstant);
            }
            GUILayout.Label("Solubility");
            float newSedimentCapacity = EditorGUILayout.Slider(solubilityConstant.Value, 0.0000001f, 0.1f);
            if (solubilityConstant.Value != newSedimentCapacity)
            {
                solubilityConstant = new KeyValuePair<string, float>(solubilityConstant.Key, newSedimentCapacity);
                erosionAndDecompositionShader.SetFloat(solubilityConstant);
            }
            GUILayout.Label("Decomposition");
            float newDepositionCapacity = EditorGUILayout.Slider(depositionConstant.Value, 0.0000001f, 0.1f);
            if (depositionConstant.Value != newDepositionCapacity)
            {
                depositionConstant = new KeyValuePair<string, float>(depositionConstant.Key, newDepositionCapacity);
                erosionAndDecompositionShader.SetFloat(depositionConstant);
            }
            
        }
	}

    private void SetPerlinNoiseShaderValues()
    {
        SetOffset();
        SetOctaves();

        perlinNoiseShader.SetTexture(perlinNoiseHandle, height.Key, height.Value);
        perlinNoiseShader.SetInt("resolution", resolution);
        perlinNoiseShader.Dispatch(perlinNoiseHandle, resolution / 8, resolution / 8, 1);
    }

    private void InitialiseErosionTextures()
    {
        InitialiseWater();
        InitialiseFlux();
        InitialiseTerrainFlux();
        InitialiseVel();
        InitialiseSediment();
    }

    private void InitialiseShaders()
    {
        perlinNoiseShader = (ComputeShader)Resources.Load("PerlinNoise");
        perlinNoiseHandle = perlinNoiseShader.FindKernel("CSMain");

        addWaterShader = new ErosionShaderBuilder().withName("AddWater").withResolution(resolution)
            .withTexture(water)
            .withFloat(waterIncrease)
            .build();
        fluxShader = new ErosionShaderBuilder().withName("Flux").withResolution(resolution)
            .withTexture(height)
            .withTexture(water)
            .withTexture(flux)
            .build();
        waterAndVelocityShader = new ErosionShaderBuilder().withName("WaterAndVelocity").withResolution(resolution)
            .withTexture(water)
            .withTexture(flux)
            .withTexture(vel)
            .build();
        erosionAndDecompositionShader = new ErosionShaderBuilder().withName("ErosionAndDecomposition").withResolution(resolution)
            .withTexture(height)
           .withTexture(water)
           .withTexture(vel)
           .withTexture(sediment)
           .withFloat(capacityConstant)
           .withFloat(solubilityConstant)
           .withFloat(depositionConstant)
           .build();
        sedimentTransportAndEvaporationShader = new ErosionShaderBuilder().withName("SedimentTransportationAndEvaporation").withResolution(resolution)
            .withTexture(height)
           .withTexture(water)
           .withTexture(vel)
           .withTexture(sediment)
           .withTexture(terrainFlux)
           .withFloat(evaporationConstant)
           .build();
        materialTransportShader = new ErosionShaderBuilder().withName("MaterialTransport").withResolution(resolution)
            .withTexture(height)
           .withTexture(terrainFlux)
           .withTexture(sediment)
           .build();
    }

    private void SetRenderShaderTextures()
    {
        rend = terrain.GetComponent<Renderer>();
        rend.enabled = true;

        rend.sharedMaterial.SetTexture("Texture2D_f24a80a3f47f4c20844d82524f9db08d", height.Value);
        rend.sharedMaterial.SetTexture("Texture2D_6e077698234c43b5858f766869fb4614", water.Value);
    }

    private void RenderTerrainEditor()
	{
		RenderResolutionField();
		RenderOffsetSliders();
		RenderOctaveSliders();
	}

	private void RenderResolutionField()
	{
		int newRes = EditorGUILayout.IntField("Resolution ", resolution);
        if(newRes != resolution)
		{
            resolution = newRes;
            ScalableBufferManager.ResizeBuffers(resolution, resolution);
            perlinNoiseShader.SetInt("resolution", resolution);

            perlinNoiseShader.Dispatch(perlinNoiseHandle, resolution / 8, resolution / 8, 1);
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

            perlinNoiseShader.Dispatch(perlinNoiseHandle, resolution / 8, resolution / 8, 1);
        }
    }

	private void RenderOctaveSliders()
	{
        List<Octave> octavesToRender = new List<Octave>(octaves);

        bool somethingChanged = false;

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
                somethingChanged = true;
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
            somethingChanged = true;
        }

        if(somethingChanged)
		{
            InitialiseOctaves();
            SetOctaves();

            perlinNoiseShader.Dispatch(perlinNoiseHandle, resolution / 8, resolution / 8, 1);
        }
    }

    private void InitialiseOctaves()
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

    private void InitialiseHeight()
    {
        if(!default(KeyValuePair<string, RenderTexture>).Equals(height))
		{
			ClearTexture(height.Value);
		}
		else
		{
            height = InitialiseTexture("height", RenderTextureFormat.RFloat);
        }
    }

	private void InitialiseWater()
    {
        if (!default(KeyValuePair<string, RenderTexture>).Equals(water))
        {
            ClearTexture(water.Value);
        }
        else
        {
            water = InitialiseTexture("water", RenderTextureFormat.RGFloat);
        }
    }

    private void InitialiseFlux()
    {
        if (!default(KeyValuePair<string, RenderTexture>).Equals(flux))
        {
            ClearTexture(flux.Value);
        }
        else
        {
            flux = InitialiseTexture("flux", RenderTextureFormat.ARGBFloat);
        }
    }

    private void InitialiseTerrainFlux()
    {
        if (!default(KeyValuePair<string, RenderTexture>).Equals(terrainFlux))
        {
            ClearTexture(terrainFlux.Value);
        }
        else
        {
            terrainFlux = InitialiseTexture("terrainFlux", RenderTextureFormat.ARGBFloat);
        }
    }

    private void InitialiseVel()
    {
        if (!default(KeyValuePair<string, RenderTexture>).Equals(vel))
        {
            ClearTexture(vel.Value);
        }
        else
        {
            vel = InitialiseTexture("vel", RenderTextureFormat.RGFloat);
        }
    }

    private void InitialiseSediment()
    {
        if (!default(KeyValuePair<string, RenderTexture>).Equals(sediment))
        {
            ClearTexture(sediment.Value);
        }
        else
        {
            sediment = InitialiseTexture("sed", RenderTextureFormat.RGFloat);
        }
    }

    private KeyValuePair<string, RenderTexture> InitialiseTexture(string name, RenderTextureFormat format)
	{
        RenderTexture texture = new RenderTexture(resolution, resolution, 32, format)
        {
            enableRandomWrite = true,
            useDynamicScale = true
        };
        texture.Create();
        return new KeyValuePair<string, RenderTexture>(name, texture);
    }

    private void ClearTexture(RenderTexture texture)
    {
        RenderTexture rt = RenderTexture.active;
        RenderTexture.active = texture;
        GL.Clear(true, true, Color.clear);
        RenderTexture.active = rt;
    }
}
