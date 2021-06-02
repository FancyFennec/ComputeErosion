using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class ErosionShaderConstants
{
    public const string PERLIN_NOISE = "PerlinNoise";
    public const string ADD_WATER = "AddWater";
    public const string FLUX = "Flux";
    public const string WATER_AND_VELOCIY = "WaterAndVelocity";
    public const string EROSION_AND_DECOMPOSITION = "ErosionAndDecomposition";
    public const string SEDIMENT_TRANSPORT_AND_EVAPORATION = "SedimentTransportationAndEvaporation";
    public const string MATERIAL_TRANSPORT = "MaterialTransport";
}

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
    static int resolution = 1024;
    int perlinNoiseHandle;
    Renderer rend;

    List<Octave> octaves = new List<Octave>() { new Octave(1f, 1f) };
    ComputeBuffer octavesComputeBuffer;
    ComputeShader perlinNoiseShader;

    ErosionShader fluxShader;
    ErosionShader waterAndVelocityShader;
    ErosionShader addWaterShader;
    ErosionShader erosionAndDecompositionShader;
    ErosionShader sedimentTransportAndEvaporationShader;
    ErosionShader materialTransportShader;

    ErosionTexture height;
    ErosionTexture water;
    ErosionTexture flux;
    ErosionTexture terrainFlux;
    ErosionTexture velocity;
    ErosionTexture sediment;
    

    float xOffset = 0f;
    float yOffset = 0f;

    KeyValuePair<string, float> waterIncrease = new KeyValuePair<string, float>("waterIncrease", 0.000003f);
    KeyValuePair<string, float> evaporationConstant = new KeyValuePair<string, float>("Ke", 0.00003f);

    KeyValuePair<string, float> capacityConstant = new KeyValuePair<string, float>("Kc", 1.9f);
    KeyValuePair<string, float> solubilityConstant = new KeyValuePair<string, float>("Ks", 0.01f);
    KeyValuePair<string, float> depositionConstant = new KeyValuePair<string, float>("Kd", 0.01f);
    KeyValuePair<string, float> minAngle = new KeyValuePair<string, float>("minAngle", 0.05f);
    KeyValuePair<string, float> dTime = new KeyValuePair<string, float>("dTime", 1000.0f / 60f);

    private void OnEnable()
	{
		terrain = (ErodingTerrain)target;

        InitialiseOctaves();
		InitialiseTextures();
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
            fluxShader.Dispatch();
            waterAndVelocityShader.Dispatch();
            erosionAndDecompositionShader.Dispatch();
            sedimentTransportAndEvaporationShader.Dispatch();
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
                height.Initialise();
                perlinNoiseShader.Dispatch(perlinNoiseHandle, resolution / 8, resolution / 8, 1);
            }
            InitialiseTextures();
        }

		if (editingTerrain)
		{
			RenderTerrainEditor();
        } else
		{
            GUILayout.Label("Water Control");
            GUILayout.Label("Time Step");
            float newDTime = EditorGUILayout.Slider(dTime.Value, 0f, 100f);
            if (dTime.Value != newDTime)
            {
                dTime = new KeyValuePair<string, float>(dTime.Key, newDTime);
                fluxShader.SetFloat(dTime);
                waterAndVelocityShader.SetFloat(dTime);
                erosionAndDecompositionShader.SetFloat(dTime);
                sedimentTransportAndEvaporationShader.SetFloat(dTime);
                materialTransportShader.SetFloat(dTime);
            }
            GUILayout.Label("Water Increase");
            float newWaterIncrease = EditorGUILayout.Slider(waterIncrease.Value, 0f, 0.00005f);
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
            GUILayout.Label("Min Angle");
            float newMinAngle = EditorGUILayout.Slider(minAngle.Value, 0.0f, 1.0f);
            if (minAngle.Value != newMinAngle)
            {
                minAngle = new KeyValuePair<string, float>(minAngle.Key, newMinAngle);
                erosionAndDecompositionShader.SetFloat(minAngle);
            }
        }
	}

    private void SetPerlinNoiseShaderValues()
    {
        SetOffset();
        SetOctaves();

        perlinNoiseShader.SetTexture(perlinNoiseHandle, height.texture.Key, height.texture.Value);
        perlinNoiseShader.SetInt("resolution", resolution);
        perlinNoiseShader.Dispatch(perlinNoiseHandle, resolution / 8, resolution / 8, 1);
    }

    private void InitialiseTextures()
    {
        height = new ErosionTexture("height", resolution, RenderTextureFormat.RFloat);
        water = new ErosionTexture("water", resolution, RenderTextureFormat.RGFloat);
        flux = new ErosionTexture("flux", resolution, RenderTextureFormat.ARGBFloat);
        terrainFlux = new ErosionTexture("terrainFlux", resolution, RenderTextureFormat.ARGBFloat);
        velocity = new ErosionTexture("vel", resolution, RenderTextureFormat.RGFloat);
        sediment = new ErosionTexture("sed", resolution, RenderTextureFormat.RGFloat);
    }   

    private void InitialiseShaders()
    {
        perlinNoiseShader = (ComputeShader)Resources.Load(ErosionShaderConstants.PERLIN_NOISE);
        perlinNoiseHandle = perlinNoiseShader.FindKernel("CSMain");

        addWaterShader = new ErosionShaderBuilder().withName(ErosionShaderConstants.ADD_WATER).withResolution(resolution)
            .withTexture(water.texture)
            .withFloat(waterIncrease)
            .build();
        fluxShader = new ErosionShaderBuilder().withName(ErosionShaderConstants.FLUX).withResolution(resolution)
            .withTexture(height.texture)
            .withTexture(water.texture)
            .withTexture(flux.texture)
            .withFloat(dTime)
            .build();
        waterAndVelocityShader = new ErosionShaderBuilder().withName(ErosionShaderConstants.WATER_AND_VELOCIY).withResolution(resolution)
            .withTexture(water.texture)
            .withTexture(flux.texture)
            .withTexture(velocity.texture)
            .withFloat(dTime)
            .build();
        erosionAndDecompositionShader = new ErosionShaderBuilder().withName(ErosionShaderConstants.EROSION_AND_DECOMPOSITION).withResolution(resolution)
            .withTexture(height.texture)
           .withTexture(water.texture)
           .withTexture(velocity.texture)
           .withTexture(sediment.texture)
           .withFloat(capacityConstant)
           .withFloat(solubilityConstant)
           .withFloat(depositionConstant)
           .withFloat(minAngle)
           .withFloat(dTime)
           .build();
        sedimentTransportAndEvaporationShader = new ErosionShaderBuilder().withName(ErosionShaderConstants.SEDIMENT_TRANSPORT_AND_EVAPORATION).withResolution(resolution)
            .withTexture(height.texture)
           .withTexture(water.texture)
           .withTexture(velocity.texture)
           .withTexture(sediment.texture)
           .withTexture(terrainFlux.texture)
           .withFloat(evaporationConstant)
           .withFloat(dTime)
           .build();
        materialTransportShader = new ErosionShaderBuilder().withName(ErosionShaderConstants.MATERIAL_TRANSPORT).withResolution(resolution)
            .withTexture(height.texture)
           .withTexture(terrainFlux.texture)
           .withTexture(sediment.texture)
           .withFloat(dTime)
           .build();
    }

    private void SetRenderShaderTextures()
    {
        rend = terrain.GetComponent<Renderer>();
        rend.enabled = true;

        rend.sharedMaterial.SetTexture("Texture2D_f24a80a3f47f4c20844d82524f9db08d", height.texture.Value);
        rend.sharedMaterial.SetTexture("Texture2D_6e077698234c43b5858f766869fb4614", water.texture.Value);
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
		float newXOffset = EditorGUILayout.Slider(xOffset, -1f, 1f);
		GUILayout.Label("Y-Offset");
        float newyOffset = EditorGUILayout.Slider(yOffset, -1f, 1f);
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
}
