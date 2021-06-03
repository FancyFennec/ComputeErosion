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
		textures = new List<ErosionTexture>();
		consts = new List<ErosionFloat>();
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

	public ErosionShaderBuilder withConst(ErosionFloat constant)
	{
		consts.Add(constant);
		return this;
	}

	public ErosionShaderBuilder withTexture(ErosionTexture texture)
	{
		textures.Add(texture);
		return this;
	}

	public ErosionShader build()
	{
		ErosionShader erosionShader = new ErosionShader(name, resolution, textures, consts);
		consts.ForEach(constant => constant.addShader(erosionShader));
		erosionShader.SetTextures();
		erosionShader.Dispatch();
		return erosionShader;
	}
}

public class ErosionShader
{
	protected ComputeShader shader;
	protected int resolution;
	protected List<ErosionTexture> textures;
	protected List<ErosionFloat> consts;

	int handle;

	public ErosionShader() { }

	public ErosionShader(
		string shaderName,
		int resolution,
		params ErosionTexture[] textures)
	{
		shader = (ComputeShader)Resources.Load(shaderName);
		this.resolution = resolution;
		handle = shader.FindKernel("CSMain");
		this.textures = new List<ErosionTexture>(textures);
		this.consts = new List<ErosionFloat>();

		Initialise();
	}

	public ErosionShader(
		string shaderName,
		int resolution,
		List<ErosionTexture> textures,
		List<ErosionFloat> consts) : this(shaderName, resolution, textures.ToArray())
	{
		this.consts.AddRange(new List<ErosionFloat>(consts));

		Initialise();
	}

	public void Initialise()
	{
		SetTextures();
		shader.SetInt("resolution", resolution);
	}

	public void SetTextures()
	{
		textures.ForEach(tex =>
		{
			shader.SetTexture(handle, tex.key, tex.texture);
		});
	}

	public void SetInt(string key, int value)
	{
		shader.SetInt(key, value);
	}

	public void SetFloat(string key, float value)
	{
		shader.SetFloat(key, value);
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