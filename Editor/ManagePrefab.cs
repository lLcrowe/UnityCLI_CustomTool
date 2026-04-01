using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityCliConnector;

namespace UnityCLI.CustomTools
{
    [UnityCliTool(Name = "prefab", Description = "Manage prefabs. Actions: save, instantiate, apply, unpack, get_status.", Group = "assets")]
    public static class ManagePrefab
    {
        public class Parameters
        {
            [ToolParameter("Action: save, instantiate, apply, unpack, get_status", Required = true)]
            public string Action { get; set; }

            [ToolParameter("Target GameObject instance ID")]
            public int InstanceId { get; set; }

            [ToolParameter("Target GameObject path or name")]
            public string Path { get; set; }

            [ToolParameter("Target GameObject name")]
            public string Name { get; set; }

            [ToolParameter("Asset path for save/instantiate (e.g. Assets/Prefabs/Player.prefab)")]
            public string AssetPath { get; set; }

            [ToolParameter("Parent instance ID or path for instantiate")]
            public string Parent { get; set; }

            [ToolParameter("Unpack completely (default false, root only)")]
            public bool Completely { get; set; }
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
                case "save": return Save(p);
                case "instantiate": return Instantiate(p);
                case "apply": return Apply(p);
                case "unpack": return Unpack(p);
                case "get_status": return GetStatus(p);
                default:
                    return new ErrorResponse($"Unknown action: '{actionResult.Value}'.");
            }
        }

        static object Save(ToolParams p)
        {
            var result = GameObjectResolver.Resolve(p);
            if (!result.IsSuccess) return new ErrorResponse(result.ErrorMessage);

            var assetPath = p.Get("asset_path");
            if (string.IsNullOrEmpty(assetPath))
                return new ErrorResponse("'asset_path' is required (e.g. Assets/Prefabs/MyPrefab.prefab).");

            // 폴더가 없으면 생성
            string dir = System.IO.Path.GetDirectoryName(assetPath).Replace('\\', '/');
            if (!AssetDatabase.IsValidFolder(dir))
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

            bool success;
            var prefab = PrefabUtility.SaveAsPrefabAsset(result.Value, assetPath, out success);

            if (!success)
                return new ErrorResponse($"Failed to save prefab at '{assetPath}'.");

            return new SuccessResponse($"Saved prefab '{assetPath}'.", new
            {
                asset_path = assetPath,
                instance_id = result.Value.GetInstanceID()
            });
        }

        static object Instantiate(ToolParams p)
        {
            var assetPath = p.Get("asset_path");
            if (string.IsNullOrEmpty(assetPath))
                return new ErrorResponse("'asset_path' is required.");

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (prefab == null)
                return new ErrorResponse($"Prefab not found at '{assetPath}'.");

            var parent = GameObjectResolver.ResolveParent(p);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            Undo.RegisterCreatedObjectUndo(instance, $"Instantiate {prefab.name}");

            if (parent != null)
                Undo.SetTransformParent(instance.transform, parent.transform, $"Set parent of {instance.name}");

            var pos = GameObjectResolver.ParseVector3(p.GetRaw("position"));
            if (pos.HasValue) instance.transform.localPosition = pos.Value;

            return new SuccessResponse($"Instantiated '{prefab.name}'.", new
            {
                instance_id = instance.GetInstanceID(),
                name = instance.name,
                path = GameObjectResolver.GetHierarchyPath(instance.transform),
                asset_path = assetPath
            });
        }

        static object Apply(ToolParams p)
        {
            var result = GameObjectResolver.Resolve(p);
            if (!result.IsSuccess) return new ErrorResponse(result.ErrorMessage);

            var go = result.Value;
            if (!PrefabUtility.IsPartOfPrefabInstance(go))
                return new ErrorResponse($"'{go.name}' is not a prefab instance.");

            PrefabUtility.ApplyPrefabInstance(go, InteractionMode.UserAction);

            return new SuccessResponse($"Applied overrides of '{go.name}'.", new
            {
                instance_id = go.GetInstanceID(),
                name = go.name
            });
        }

        static object Unpack(ToolParams p)
        {
            var result = GameObjectResolver.Resolve(p);
            if (!result.IsSuccess) return new ErrorResponse(result.ErrorMessage);

            var go = result.Value;
            if (!PrefabUtility.IsPartOfPrefabInstance(go))
                return new ErrorResponse($"'{go.name}' is not a prefab instance.");

            bool completely = p.GetBool("completely");
            var mode = completely
                ? PrefabUnpackMode.Completely
                : PrefabUnpackMode.OutermostRoot;

            PrefabUtility.UnpackPrefabInstance(go, mode, InteractionMode.UserAction);

            return new SuccessResponse($"Unpacked '{go.name}' ({mode}).", new
            {
                instance_id = go.GetInstanceID(),
                name = go.name,
                mode = mode.ToString()
            });
        }

        static object GetStatus(ToolParams p)
        {
            var result = GameObjectResolver.Resolve(p);
            if (!result.IsSuccess) return new ErrorResponse(result.ErrorMessage);

            var go = result.Value;
            bool isPrefab = PrefabUtility.IsPartOfPrefabInstance(go);
            string assetPath = isPrefab ? PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go) : null;
            bool hasOverrides = isPrefab && PrefabUtility.HasPrefabInstanceAnyOverrides(go, false);

            return new SuccessResponse($"Prefab status of '{go.name}'.", new
            {
                instance_id = go.GetInstanceID(),
                is_prefab_instance = isPrefab,
                asset_path = assetPath,
                has_overrides = hasOverrides
            });
        }
    }
}
