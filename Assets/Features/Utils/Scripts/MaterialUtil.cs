using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;
using UnityEngine;

namespace Features.Utils.Scripts
{
    public class MaterialWrapper<TProp> where TProp : Enum
    {
        private readonly Dictionary<TProp, int> _propMap;
        public readonly Material Material;

        public MaterialWrapper(Material material)
        {
            Material = material;
            _propMap = Enum.GetValues(typeof(TProp)).Cast<TProp>()
                .ToDictionary(t => t, t => Shader.PropertyToID(t.ToString()));
        }

        public int GetPropertyId(TProp prop)
        {
            return _propMap[prop];
        }

        public void SetFloat(TProp prop, float value)
        {
            Material.SetFloat(_propMap[prop], value);
        }

        public void SetBool(string keyword, bool value)
        {
            if (value)
                Material.EnableKeyword(keyword);
            else
                Material.DisableKeyword(keyword);
        }

        public bool GetBool(string keyword)
        {
            return Material.IsKeywordEnabled(keyword);
        }

        public float GetFloat(TProp prop)
        {
            return Material.GetFloat(_propMap[prop]);
        }

        public void SetInt(TProp prop, int value)
        {
            Material.SetInt(_propMap[prop], value);
        }

        public void SetVector(TProp prop, in float4 value)
        {
            Material.SetVector(_propMap[prop], value);
        }

        public void SetColor(TProp prop, in Color32 value)
        {
            Material.SetColor(_propMap[prop], value);
        }

        public void SetTexture(TProp prop, Texture value)
        {
            Material.SetTexture(_propMap[prop], value);
        }

        public void SetBuffer(TProp prop, GraphicsBuffer value)
        {
            Material.SetBuffer(_propMap[prop], value);
        }

        public void SetMatrix(TProp prop, Matrix4x4 value)
        {
            Material.SetMatrix(_propMap[prop], value);
        }
    }
}