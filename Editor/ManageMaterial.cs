using System.Globalization;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityCliConnector;

namespace UnityCLI.CustomTools
{
    [UnityCliTool(Name = "material", Description = "Manage materials. Actions: create, get_info, set_color, set_float, set_texture, assign.", Group = "assets")]
    public static class ManageMaterial
    {
        public class Parameters
        {
            [ToolParameter("Action: create, get_info, set_color, set_float, set_texture, assign", Required = true)]
            public string Action { get; set; }

            [ToolParameter("Material asset path (e.g. Assets/Materials/MyMat.mat)")]
            public string Path { get; set; }

            [ToolParameter("Shader name (e.g. Universal Render Pipeline/Lit)")]
            public string Shader { get; set; }

            [ToolParameter("Property name (e.g. _BaseColor, _Metallic)")]
            public string Property { get; set; }

            [ToolParameter("Value to set (float, or color as r,g,b,a)")]
            public string Value { get; set; }

            [ToolParameter("Texture asset path for set_texture")]
            public string TexturePath { get; set; }

            [ToolParameter("Target GameObject for assign")]
            public string Name { get; set; }

            [ToolParameter("Target GameObject instance ID for assign")]
            public int InstanceId { get; set; }

            [ToolParameter("Renderer material slot index (default 0)")]
            public int Slot { get; set; }
        }

        public static object HandleCommand(JObject @params)
        {
            if (@params == null)
                return new ErrorResponse("Parameters cannot be null.");

            var p = new ToolParams(@params);
            var actionResult = p.GetRequired("action");
            if (!actionResult.IsSuccess)
                return new ErrorResponse(actionResult.ErrorMessage);

            switch (actionResult.Value.ToLowerInvariant())
            {
                case "create": return Create(p);
                case "get_info": return GetInfo(p);
                case "set_color": return SetColor(p);
                case "set_float": return SetFloat(p);
                case "set_texture": return SetTexture(p);
                case "assign": return Assign(p);
                default:
                    return new ErrorResponse($"Unknown action: '{actionResult.Value}'.");
            }
        }

        static object Create(ToolParams p)
        {
            var pathResult = p.GetRequired("path", "'path' is required.");
            if (!pathResult.IsSuccess) return new ErrorResponse(pathResult.ErrorMessage);

            string shaderName = p.Get("shader", "Universal Render Pipeline/Lit");
            var shader = UnityEngine.Shader.Find(shaderName);
            if (shader == null)
                return new ErrorResponse($"Shader '{shaderName}' not found.");

            var mat = new Material(shader);

            // 폴더 확인
            string dir = System.IO.Path.GetDirectoryName(pathResult.Value).Replace('\\', '/');
            if (!string.IsNullOrEmpty(dir) && !AssetDatabase.IsValidFolder(dir))
            {
                string[] parts = dir.Split('/');
                string current = parts[0];
                for (int i = 1; i < parts.Length; i++)
                {
                    string next = current + "/" + parts[i];
                    if (!AssetDatabase.IsValidFolder(next))
                        AssetDatabase.CreateFolder(current, parts[i]);
                    current = next;
                }
            }

            AssetDatabase.CreateAsset(mat, pathResult.Value);
            AssetDatabase.SaveAssets();

            return new SuccessResponse($"Created material '{pathResult.Value}'.", new
            {
                path = pathResult.Value,
                shader = shaderName
            });
        }

        static object GetInfo(ToolParams p)
        {
            var mat = LoadMaterial(p);
            if (mat == null) return new ErrorResponse("Material not found.");

            var shaderPropertyCount = mat.shader.GetPropertyCount();
            var properties = new object[shaderPropertyCount];
            for (int i = 0; i < shaderPropertyCount; i++)
            {
                var propName = mat.shader.GetPropertyName(i);
                var propType = mat.shader.GetPropertyType(i);
                properties[i] = new
                {
                    name = propName,
                    type = propType.ToString(),
                    description = mat.shader.GetPropertyDescription(i)
                };
            }

            return new SuccessResponse($"Material info: {mat.name}.", new
            {
                name = mat.name,
                shader = mat.shader.name,
                render_queue = mat.renderQueue,
                property_count = shaderPropertyCount,
                properties
            });
        }

        static object SetColor(ToolParams p)
        {
            var mat = LoadMaterial(p);
            if (mat == null) return new ErrorResponse("Material not found.");

            var propName = p.Get("property", "_BaseColor");
            var valueStr = p.Get("value");
            if (string.IsNullOrEmpty(valueStr))
                return new ErrorResponse("'value' is required (r,g,b or r,g,b,a).");

            var parts = valueStr.Split(',');
            if (parts.Length < 3)
                return new ErrorResponse("Color format: r,g,b or r,g,b,a (0~1).");

            float.TryParse(parts[0].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float r);
            float.TryParse(parts[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float g);
            float.TryParse(parts[2].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float b);
            float a = 1f;
            if (parts.Length >= 4)
                float.TryParse(parts[3].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out a);

            Undo.RecordObject(mat, "Set Material Color");
            mat.SetColor(propName, new Color(r, g, b, a));
            EditorUtility.SetDirty(mat);

            return new SuccessResponse($"Set {propName} on '{mat.name}'.", new
            {
                name = mat.name,
                property = propName,
                color = new[] { r, g, b, a }
            });
        }

        static object SetFloat(ToolParams p)
        {
            var mat = LoadMaterial(p);
            if (mat == null) return new ErrorResponse("Material not found.");

            var propName = p.Get("property");
            if (string.IsNullOrEmpty(propName))
                return new ErrorResponse("'property' is required.");

            var valueStr = p.Get("value");
            if (!float.TryParse(valueStr, NumberStyles.Float, CultureInfo.InvariantCulture, out float val))
                return new ErrorResponse($"Cannot parse '{valueStr}' as float.");

            Undo.RecordObject(mat, "Set Material Float");
            mat.SetFloat(propName, val);
            EditorUtility.SetDirty(mat);

            return new SuccessResponse($"Set {propName}={val} on '{mat.name}'.", new
            {
                name = mat.name,
                property = propName,
                value = val
            });
        }

        static object SetTexture(ToolParams p)
        {
            var mat = LoadMaterial(p);
            if (mat == null) return new ErrorResponse("Material not found.");

            var propName = p.Get("property", "_BaseMap");
            var texPath = p.Get("texture_path");
            if (string.IsNullOrEmpty(texPath))
                return new ErrorResponse("'texture_path' is required.");

            var tex = AssetDatabase.LoadAssetAtPath<Texture>(texPath);
            if (tex == null)
                return new ErrorResponse($"Texture not found at '{texPath}'.");

            Undo.RecordObject(mat, "Set Material Texture");
            mat.SetTexture(propName, tex);
            EditorUtility.SetDirty(mat);

            return new SuccessResponse($"Set {propName} texture on '{mat.name}'.", new
            {
                name = mat.name,
                property = propName,
                texture_path = texPath
            });
        }

        static object Assign(ToolParams p)
        {
            var goResult = GameObjectResolver.Resolve(p);
            if (!goResult.IsSuccess) return new ErrorResponse(goResult.ErrorMessage);

            var matPath = p.Get("path");
            if (string.IsNullOrEmpty(matPath))
                return new ErrorResponse("'path' is required (material asset path).");

            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
                return new ErrorResponse($"Material not found at '{matPath}'.");

            var go = goResult.Value;
            if (!go.TryGetComponent<Renderer>(out var renderer))
                return new ErrorResponse($"'{go.name}' has no Renderer component.");

            int slot = p.GetInt("slot") ?? 0;
            var mats = renderer.sharedMaterials;
            if (slot < 0 || slot >= mats.Length)
                return new ErrorResponse($"Slot {slot} out of range (0~{mats.Length - 1}).");

            Undo.RecordObject(renderer, "Assign Material");
            mats[slot] = mat;
            renderer.sharedMaterials = mats;

            return new SuccessResponse($"Assigned '{mat.name}' to '{go.name}' slot {slot}.", new
            {
                instance_id = go.GetInstanceID(),
                material = mat.name,
                slot
            });
        }

        static Material LoadMaterial(ToolParams p)
        {
            var path = p.Get("path");
            if (string.IsNullOrEmpty(path)) return null;
            return AssetDatabase.LoadAssetAtPath<Material>(path);
        }
    }
}
