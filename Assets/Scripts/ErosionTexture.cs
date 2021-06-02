using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

public class ErosionTexture
{

	private string name;
	private int resolution;
	private RenderTextureFormat format;
	public KeyValuePair<string, RenderTexture> texture;

	public ErosionTexture(string name, int resolution, RenderTextureFormat format)
	{
		this.name = name;
		this.resolution = resolution;
		this.format = format;
		InitialiseTexture();
	}

	private void InitialiseTexture()
	{
		RenderTexture texture = new RenderTexture(resolution, resolution, 32, format)
		{
			enableRandomWrite = true,
			useDynamicScale = true
		};
		texture.Create();
		this.texture = new KeyValuePair<string, RenderTexture>(name, texture);
	}

	public void Initialise()
	{
		if (!default(KeyValuePair<string, RenderTexture>).Equals(texture.Value))
		{
			ClearTexture();
		}
		else
		{
			InitialiseTexture();
		}
	}

	private void ClearTexture()
	{
		RenderTexture rt = RenderTexture.active;
		RenderTexture.active = texture.Value;
		GL.Clear(true, true, Color.clear);
		RenderTexture.active = rt;
	}
}