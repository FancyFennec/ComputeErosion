using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

public class ErosionShaderBuilder : ErosionShader
{
	private string name;
	public ErosionShaderBuilder()
	{
		textures = new List<KeyValuePair<String, RenderTexture>>();
		ints = new List<KeyValuePair<String, int>>();
		floats = new List<KeyValuePair<String, float>>();
	}
	public ErosionShaderBuilder withResolution(int resolution)
	{
		this.resolution = resolution;
		return this;
	}
	public ErosionShaderBuilder withName(string name)
	{
		this.name = name;
		return this;
	}

	public ErosionShaderBuilder withInt(string key, int value)
	{
		return withInt(new KeyValuePair<string, int>(key, value));
	}

	public ErosionShaderBuilder withInt(KeyValuePair<string, int> value)
	{
		ints.Add(value);
		return this;
	}

	public ErosionShaderBuilder withFloat(string key, float value)
	{
		return withFloat(new KeyValuePair<string, float>(key, value));
	}

	public ErosionShaderBuilder withFloat(KeyValuePair<string, float> value)
	{
		floats.Add(value);
		return this;
	}

	public ErosionShaderBuilder withTexture(string key, RenderTexture value)
	{
		return withTexture(new KeyValuePair<string, RenderTexture>(key, value));
	}

	public ErosionShaderBuilder withTexture(KeyValuePair<string, RenderTexture> texture)
	{
		textures.Add(texture);
		return this;
	}

	public ErosionShader build()
	{
		ErosionShader erosionShader = new ErosionShader(name, resolution, textures, ints, floats);
		erosionShader.SetFloats();
		erosionShader.SetInts();
		erosionShader.SetTextures();
		erosionShader.Dispatch();
		return erosionShader;
	}
}

public class ErosionShader
{
	protected ComputeShader shader;
	protected int resolution;
	protected List<KeyValuePair<string, RenderTexture>> textures;
	protected List<KeyValuePair<string, int>> ints;
	protected List<KeyValuePair<string, float>> floats;

	int handle;

	public ErosionShader() { }

	public ErosionShader(
		string shaderName,
		int resolution,
		params KeyValuePair<String, RenderTexture>[] textures)
	{
		shader = (ComputeShader)Resources.Load(shaderName);
		this.resolution = resolution;
		handle = shader.FindKernel("CSMain");
		this.textures = new List<KeyValuePair<string, RenderTexture>>(textures);
		this.ints = new List<KeyValuePair<string, int>>() { new KeyValuePair<string, int>("resolution", resolution) };
		this.floats = new List<KeyValuePair<string, float>>();

		Initialise();
	}

	public ErosionShader(
		string shaderName,
		int resolution,
		List<KeyValuePair<String, RenderTexture>> textures,
		List<KeyValuePair<String, int>> ints,
		List<KeyValuePair<String, float>> floats) : this(shaderName, resolution, textures.ToArray())
	{
		this.ints.AddRange(new List<KeyValuePair<string, int>>(ints));
		this.floats.AddRange(new List<KeyValuePair<string, float>>(floats));

		Initialise();
	}

	public void Initialise()
	{
		SetTextures();
		SetInts();
		SetInts();
	}

	public void SetTextures()
	{
		textures.ForEach(tex =>
		{
			shader.SetTexture(handle, tex.Key, tex.Value);
		});
	}

	public void SetInt(string key, int value)
	{
		shader.SetInt(key, value);
	}

	public void SetInt(KeyValuePair<string, int> pair)
	{
		shader.SetInt(pair.Key, pair.Value);
	}

	public void SetInts()
	{
		ints.ForEach(SetInt);
	}

	public void SetFloat(string key, float value)
	{
		shader.SetFloat(key, value);
	}

	public void SetFloat(KeyValuePair<string, float> pair)
	{
		shader.SetFloat(pair.Key, pair.Value);
	}

	public void SetFloats()
	{
		floats.ForEach(SetFloat);
	}

	public void Dispatch()
	{
		shader.Dispatch(handle, resolution / 8, resolution / 8, 1);
	}

	public void Dispatch(string key, float value)
	{
		SetFloat(key, value);
		Dispatch();
	}
}