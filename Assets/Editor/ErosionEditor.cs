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
    List<ErosionShader> shaders;

    ErosionTexture height;
    ErosionTexture water;
    ErosionTexture flux;
    ErosionTexture terrainFlux;
    ErosionTexture velocity;
    ErosionTexture sediment;
    List<ErosionTexture> textures;

    float xOffset = 0f;
    float yOffset = 0f;

    static ErosionFloat waterIncrease = new ErosionFloat("waterIncrease", 0.000003f, "Water Increase", 0f, 0.00005f);
    static ErosionFloat evaporationConstant = new ErosionFloat("Ke", 0.00003f, "Evaporation", 0.0000001f, 0.001f);
    static ErosionFloat capacityConstant = new ErosionFloat("Kc", 1.9f, "Capacity", 0.0000001f, 3f);
    static ErosionFloat solubilityConstant = new ErosionFloat("Ks", 0.01f, "Solubility", 0.0000001f, 0.1f);
    static ErosionFloat depositionConstant = new ErosionFloat("Kd", 0.01f, "Deposition", 0.0000001f, 0.1f);
    static ErosionFloat minAngle = new ErosionFloat("minAngle", 0.05f, "Min Angle", 0.0f, 1.0f);
    static ErosionFloat dTime = new ErosionFloat("dTime", 1000.0f / 60f, "Time Step", 0f, 100f);
    List<ErosionFloat> constants = new List<ErosionFloat>() { 
        waterIncrease, 
        evaporationConstant, 
        capacityConstant, 
        solubilityConstant, 
        depositionConstant,
        minAngle,
        dTime
    };

    private void OnEnable()
	{
		terrain = (ErodingTerrain)target;

		InitialiseOctaveBuffer();
		InitialiseTextures();
		InitialiseShaders();

        constants.ForEach(c => c.SetConstant());

        SetPerlinNoiseShaderValues();
		SetRenderShaderTextures();
	}

	private void OnSceneGUI()
    {
        if (!editingTerrain)
        {
            addWaterShader.SetFloat("time", ((float)EditorApplication.timeSinceStartup % 10));
            shaders.ForEach(s => s.Dispatch());
        }
    }

    public override void OnInspectorGUI()
	{
        if(editingTerrain != EditorGUILayout.Toggle("EditTerrain", editingTerrain))
		{
            editingTerrain = !editingTerrain;
            if(editingTerrain)
			{
                textures.ForEach(t => t.Initialise());
                perlinNoiseShader.Dispatch(perlinNoiseHandle, resolution / 8, resolution / 8, 1);
            }
        }

		if (editingTerrain)
		{
			RenderTerrainEditor();
        } else
		{
            GUILayout.Label("Water Control");
            dTime.drawSlider();
            waterIncrease.drawSlider();
            evaporationConstant.drawSlider();
            GUILayout.Label("Erosion Control");
            capacityConstant.drawSlider();
            solubilityConstant.drawSlider();
            depositionConstant.drawSlider();
            minAngle.drawSlider();
        }
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
        if (newRes != resolution)
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

        if (newXOffset != xOffset || newyOffset != yOffset)
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

        for (int index = 0; index < octavesToRender.Count; index++)
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
            }
            else
            {
                GUILayout.Label("    ");
            }
            EditorGUILayout.EndHorizontal();
        }

        if (GUILayout.Button("Add Octave"))
        {
            octaves.Add(new Octave(1f, 1f));
            somethingChanged = true;
        }

        if (somethingChanged)
        {
            InitialiseOctaveBuffer();
            SetOctaves();

            perlinNoiseShader.Dispatch(perlinNoiseHandle, resolution / 8, resolution / 8, 1);
        }
    }

    private void SetPerlinNoiseShaderValues()
    {
        SetOffset();
        SetOctaves();

        perlinNoiseShader.SetTexture(perlinNoiseHandle, height.key, height.texture);
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

        textures = new List<ErosionTexture>() { height, water, flux, terrainFlux, velocity, sediment };
    }   

    private void InitialiseShaders()
    {
        perlinNoiseShader = (ComputeShader)Resources.Load(ErosionShaderConstants.PERLIN_NOISE);
        perlinNoiseHandle = perlinNoiseShader.FindKernel("CSMain");

        addWaterShader = new ErosionShaderBuilder().withName(ErosionShaderConstants.ADD_WATER).withResolution(resolution)
            .withTexture(water)
            .withConst(waterIncrease)
            .build();
        fluxShader = new ErosionShaderBuilder().withName(ErosionShaderConstants.FLUX).withResolution(resolution)
            .withTexture(height)
            .withTexture(water)
            .withTexture(flux)
            .withConst(dTime)
            .build();
        waterAndVelocityShader = new ErosionShaderBuilder().withName(ErosionShaderConstants.WATER_AND_VELOCIY).withResolution(resolution)
            .withTexture(water)
            .withTexture(flux)
            .withTexture(velocity)
            .withConst(dTime)
            .build();
        erosionAndDecompositionShader = new ErosionShaderBuilder().withName(ErosionShaderConstants.EROSION_AND_DECOMPOSITION).withResolution(resolution)
            .withTexture(height)
           .withTexture(water)
           .withTexture(velocity)
           .withTexture(sediment)
           .withConst(capacityConstant)
           .withConst(solubilityConstant)
           .withConst(depositionConstant)
           .withConst(minAngle)
           .withConst(dTime)
           .build();
        sedimentTransportAndEvaporationShader = new ErosionShaderBuilder().withName(ErosionShaderConstants.SEDIMENT_TRANSPORT_AND_EVAPORATION).withResolution(resolution)
            .withTexture(height)
           .withTexture(water)
           .withTexture(velocity)
           .withTexture(sediment)
           .withTexture(terrainFlux)
           .withConst(evaporationConstant)
           .withConst(dTime)
           .build();
        materialTransportShader = new ErosionShaderBuilder().withName(ErosionShaderConstants.MATERIAL_TRANSPORT).withResolution(resolution)
            .withTexture(height)
           .withTexture(terrainFlux)
           .withTexture(sediment)
           .withConst(dTime)
           .build();

        shaders = new List<ErosionShader>() { 
            addWaterShader, 
            fluxShader, 
            waterAndVelocityShader, 
            erosionAndDecompositionShader, 
            sedimentTransportAndEvaporationShader,
            materialTransportShader
        };
    }

    private void SetRenderShaderTextures()
    {
        rend = terrain.GetComponent<Renderer>();
        rend.enabled = true;

        rend.sharedMaterial.SetTexture("Texture2D_f24a80a3f47f4c20844d82524f9db08d", height.texture);
        rend.sharedMaterial.SetTexture("Texture2D_6e077698234c43b5858f766869fb4614", water.texture);
    }

    private void InitialiseOctaveBuffer()
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
