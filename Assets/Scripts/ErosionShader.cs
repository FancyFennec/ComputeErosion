using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

public class ErosionShader
{
	readonly ComputeShader shader;
	readonly int resolution;
	readonly List<KeyValuePair<String, RenderTexture>> textures;
	readonly List<KeyValuePair<String, int>> ints;
	readonly List<KeyValuePair<String, float>> floats;
	
	int handle;

	public ErosionShader(
		string shaderName,
		int resolution,
		params KeyValuePair<String, RenderTexture>[] textures)
	{
		shader = (ComputeShader)Resources.Load(shaderName);
		this.resolution = resolution;
		handle = shader.FindKernel("CSMain");
		this.textures = new List < KeyValuePair < String, RenderTexture >> (textures);
		this.ints = new List<KeyValuePair<String, int>>() { new KeyValuePair<String, int>("resolution", resolution)};
		this.floats = new List<KeyValuePair<String, float>>();

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

	public void SetInts()
	{
		ints.ForEach(numb =>
		{
			shader.SetInt(numb.Key, numb.Value);
		});
	}

	public void SetFloat(string key, float value)
	{
		shader.SetFloat(key, value);
	}

	public void SetFloats()
	{
		floats.ForEach(numb =>
		{
			shader.SetFloat(numb.Key, numb.Value);
		});
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