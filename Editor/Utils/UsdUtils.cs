using System.IO;
using System.Linq;
using Unity.Formats.USD;
using UnityEngine;
using USD.NET;
using Object = UnityEngine.Object;

namespace UnityEditor.ObjectCapture.Usd
{
    static class UsdUtils
    {
#if UNITY_EDITOR
        internal static string ImportAsPrefab(Scene scene)
        {
            var path = scene.FilePath;

            // Time-varying data is not supported and often scenes are written without "Default" time
            // values, which makes setting an arbitrary time safer (because if only default was authored
            // the time will be ignored and values will resolve to default time automatically).
            scene.Time = 1.0;

            var importOptions = new SceneImportOptions();
            importOptions.projectAssetPath = GetSelectedAssetPath();
            importOptions.usdRootPath = GetDefaultRoot(scene);
            importOptions.materialImportMode = MaterialImportMode.ImportPreviewSurface;

            var prefabPath = GetPrefabPath(path, importOptions.projectAssetPath);
            var clipName = Path.GetFileNameWithoutExtension(path);

            var go = new GameObject(GetObjectName(importOptions.usdRootPath, path));
            try
            {
                UsdToGameObject(go, scene, importOptions);
                SceneImporter.SavePrefab(go, prefabPath, clipName, importOptions);
                return prefabPath;
            }
            finally
            {
                Object.DestroyImmediate(go);
                scene.Close();
            }
        }

        /// <summary>
        /// Returns the selected object path or "Assets/" if no object is selected.
        /// </summary>
        static string GetSelectedAssetPath()
        {
            var selectedAsset = Selection.GetFiltered(typeof(Object), SelectionMode.Assets);
            foreach (var obj in selectedAsset)
            {
                var path = AssetDatabase.GetAssetPath(obj.GetInstanceID());
                if (string.IsNullOrEmpty(path))
                {
                    continue;
                }

                if (File.Exists(path))
                {
                    path = Path.GetDirectoryName(path);
                }

                if (!path.EndsWith("/"))
                {
                    path += "/";
                }

                return path;
            }

            return "Assets/";
        }
#endif

        static pxr.SdfPath GetDefaultRoot(Scene scene)
        {
            // We can't safely assume the default prim is the model root, because Alembic files will
            // always have a default prim set arbitrarily.

            // If there is only one root prim, reference this prim.
            var children = scene.Stage.GetPseudoRoot().GetChildren().ToList();
            if (children.Count == 1)
            {
                return children[0].GetPath();
            }

            // Otherwise there are 0 or many root prims, in this case the best option is to reference
            // them all, to avoid confusion.
            return pxr.SdfPath.AbsoluteRootPath();
        }

        static GameObject UsdToGameObject(GameObject parent,
            Scene scene,
            SceneImportOptions importOptions)
        {
            try
            {
                SceneImporter.ImportUsd(parent, scene, new PrimMap(), importOptions);
            }
            finally
            {
                scene.Close();
            }

            return parent;
        }

        static string GetObjectName(pxr.SdfPath rootPrimName, string path)
        {
            return pxr.UsdCs.TfIsValidIdentifier(rootPrimName.GetName())
                ? rootPrimName.GetName()
                : GetObjectName(path);
        }

        static string GetObjectName(string path)
        {
            return IntrinsicTypeConverter.MakeValidIdentifier(Path.GetFileNameWithoutExtension(path));
        }

        static string GetPrefabName(string path)
        {
            var fileName = GetObjectName(path);
            return $"{fileName}_prefab";
        }

        static string GetPrefabPath(string usdPath, string dataPath)
        {
            var invalidChars = Path.GetInvalidFileNameChars();
            var prefabName = string.Join("_", GetPrefabName(usdPath).Split(invalidChars,
                System.StringSplitOptions.RemoveEmptyEntries)).TrimEnd('.');
            var prefabPath = dataPath + prefabName + ".prefab";
            prefabPath = AssetDatabase.GenerateUniqueAssetPath(prefabPath);
            return prefabPath;
        }
    }
}
