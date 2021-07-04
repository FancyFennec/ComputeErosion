using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

public class ErosionTexture
{
	public string key { get; }
	private int resolution { get; }
	private RenderTextureFormat format { get; }
	public RenderTexture texture { get; private set; }

	public ErosionTexture(string key, int resolution, RenderTextureFormat format)
	{
		this.key = key;
		this.resolution = resolution;
		this.format = format;
		InitialiseTexture();
	}

	private void InitialiseTexture()
	{
		RenderTexture texture = new RenderTexture(resolution, resolution, 0, format)
		{
			enableRandomWrite = true,
			useDynamicScale = true
		};
		texture.Create();
		texture.DiscardContents(true, true);
		this.texture = texture;
	}

	public void Initialise()
	{
		if (!default(KeyValuePair<string, RenderTexture>).Equals(texture))
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
		RenderTexture.active = texture;
		GL.Clear(true, true, Color.clear);
		RenderTexture.active = rt;
	}
}