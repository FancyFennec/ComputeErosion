using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEditor;

public class ErosionFloat
{
	public string key { get; private set; }
	public float value { get; set; }
	public string title { get; private set; }
	public float minValue { get; private set; }
	public float maxValue { get; private set; }
	public List<ErosionShader> erosionShaders { get; private set; } = new List<ErosionShader>();

	public ErosionFloat(string key, float value, string title, float minValue, float maxValue)
	{
		this.key = key;
		this.value = value;
		this.title = title;
		this.minValue = minValue;
		this.maxValue = maxValue;
	}

	public void SetConstant()
	{
		erosionShaders.ForEach(s => s.SetFloat(key, value));
	}

	public void addShader(ErosionShader shader)
	{
		erosionShaders.Add(shader);
	}

	public void drawSlider()
	{
		GUILayout.Label(title);
		float newValue = EditorGUILayout.Slider(value, minValue, maxValue);
		if (value != newValue)
		{
			value = newValue;
			SetConstant();
		}
	}
}
