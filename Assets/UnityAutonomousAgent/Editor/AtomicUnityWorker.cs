using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace UnityAutonomousAgent.Editor
{
    [Serializable] internal sealed class AtomicRequest
    {
        public string requestId;
        public bool rollbackOnFailure = true;
        public string responsePath;
        public AtomicOperation[] operations = Array.Empty<AtomicOperation>();
    }

    [Serializable] internal sealed class AtomicOperation
    {
        public int index;
        public string capability;
        public string action;
        public string argsJson;
    }

    [Serializable] internal sealed class AtomicArgs
    {
        public string name;
        public string target;
        public string parent;
        public string path;
        public string assetPath;
        public string scenePath;
        public string type;
        public string property;
        public string value;
        public string valueType;
        public string shader;
        public string filter;
        public string filterMode;
        public string compression;
        public string mode;
        public string tag;
        public string outputPath;
        public string spriteAssetPath;
        public string subAsset;
        public string parameter;
        public string parameterType;
        public string state;
        public string fromState;
        public string toState;
        public string conditionMode;
        public bool active = true;
        public bool world;
        public bool sprite;
        public bool alphaIsTransparency = true;
        public bool mipmaps;
        public bool development;
        public bool enabled = true;
        public bool loop = true;
        public bool hasExitTime;
        public int layer;
        public int columns = 1;
        public int rows = 1;
        public int maxSize = 2048;
        public float pixelsPerUnit = 100f;
        public float frameRate = 12f;
        public float pivotX = 0.5f;
        public float pivotY = 0.5f;
        public float transitionDuration = 0.05f;
        public float exitTime = 1f;
        public float threshold;
        public float[] position;
        public float[] rotation;
        public float[] scale;
        public float[] color;
        public string[] assetPaths;
        public string[] spriteNames;
        public float[] frameDurationsMs;
    }

    [Serializable] internal sealed class AtomicOperationResult
    {
        public int index;
        public string capability;
        public string action;
        public bool success;
        public string errorCode;
        public string error;
        public string displayName;
        public string type;
        public string scene;
        public string hierarchyPath;
        public string stableId;
        public string assetGuid;
        public string dataJson;
    }

    [Serializable] internal sealed class AtomicResponse
    {
        public bool success;
        public string requestId;
        public string transactionStatus;
        public int failedOperationIndex = -1;
        public string errorCode;
        public string error;
        public AtomicOperationResult[] operations = Array.Empty<AtomicOperationResult>();
    }

    public static class AtomicUnityWorker
    {
        private static readonly List<string> CreatedAssets = new();

        public static void Execute()
        {
            string requestPath = ReadArgument("-unityAtomicRequest");
            if (string.IsNullOrWhiteSpace(requestPath) || !File.Exists(requestPath))
                throw new InvalidOperationException("Missing -unityAtomicRequest JSON path.");
            ProcessRequest(requestPath);
        }

        public static void ProcessRequest(string requestPath)
        {
            AtomicRequest request = JsonUtility.FromJson<AtomicRequest>(File.ReadAllText(requestPath));
            AtomicResponse response = ExecuteRequest(request);
            string responsePath = string.IsNullOrWhiteSpace(request.responsePath)
                ? Path.ChangeExtension(requestPath, ".response.json")
                : request.responsePath;
            Directory.CreateDirectory(Path.GetDirectoryName(responsePath) ?? ".");
            string temporary = responsePath + ".tmp";
            File.WriteAllText(temporary, JsonUtility.ToJson(response, true));
            if (File.Exists(responsePath)) File.Delete(responsePath);
            File.Move(temporary, responsePath);
        }

        private static AtomicResponse ExecuteRequest(AtomicRequest request)
        {
            var response = new AtomicResponse { requestId = request.requestId, transactionStatus = "BEGIN" };
            var results = new List<AtomicOperationResult>();
            CreatedAssets.Clear();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName($"Unity Autonomous Agent {request.requestId}");
            try
            {
                foreach (AtomicOperation operation in request.operations ?? Array.Empty<AtomicOperation>())
                {
                    AtomicOperationResult result;
                    try
                    {
                        result = ExecuteOperation(operation);
                    }
                    catch (Exception exception)
                    {
                        result = Failure(operation, Classify(exception), exception.Message);
                    }
                    results.Add(result);
                    if (!result.success)
                    {
                        response.failedOperationIndex = operation.index;
                        response.errorCode = result.errorCode;
                        response.error = result.error;
                        if (request.rollbackOnFailure)
                        {
                            Undo.RevertAllDownToGroup(undoGroup);
                            foreach (string asset in CreatedAssets.AsEnumerable().Reverse())
                                AssetDatabase.DeleteAsset(asset);
                            response.transactionStatus = "ROLLBACK";
                        }
                        else response.transactionStatus = "FAILED";
                        response.operations = results.ToArray();
                        response.success = false;
                        AssetDatabase.SaveAssets();
                        return response;
                    }
                }
                Undo.CollapseUndoOperations(undoGroup);
                AssetDatabase.SaveAssets();
                if (SceneManager.GetActiveScene().IsValid() && SceneManager.GetActiveScene().isDirty)
                    EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
                response.success = true;
                response.transactionStatus = "COMMIT";
            }
            catch (Exception exception)
            {
                if (request.rollbackOnFailure)
                {
                    Undo.RevertAllDownToGroup(undoGroup);
                    foreach (string asset in CreatedAssets.AsEnumerable().Reverse()) AssetDatabase.DeleteAsset(asset);
                    response.transactionStatus = "ROLLBACK";
                }
                response.success = false;
                response.errorCode = Classify(exception);
                response.error = exception.ToString();
            }
            response.operations = results.ToArray();
            return response;
        }

        private static AtomicOperationResult ExecuteOperation(AtomicOperation operation)
        {
            AtomicArgs args = string.IsNullOrWhiteSpace(operation.argsJson)
                ? new AtomicArgs()
                : JsonUtility.FromJson<AtomicArgs>(operation.argsJson);
            return operation.capability switch
            {
                "project.inspect" => ProjectInspect(operation),
                "scene.manage" => ManageScene(operation, args),
                "gameobject.manage" => ManageGameObject(operation, args),
                "component.manage" => ManageComponent(operation, args),
                "prefab.manage" => ManagePrefab(operation, args),
                "material.manage" => ManageMaterial(operation, args),
                "texture.manage" => ManageTexture(operation, args),
                "sprite.manage" => ManageTexture(operation, args),
                "asset.manage" => ManageAsset(operation, args),
                "camera.manage" => ManageCamera(operation, args),
                "animation.manage" => ManageAnimation(operation, args),
                "ui.manage" => ManageUi(operation, args),
                "build.manage" => ManageBuild(operation, args),
                "screenshot.capture" => ManageScreenshot(operation, args),
                "editor.manage" => ManageEditor(operation),
                _ => Failure(operation, "CapabilityMissing", $"Unsupported capability: {operation.capability}"),
            };
        }

        private static AtomicOperationResult ProjectInspect(AtomicOperation operation)
        {
            string[] scenes = EditorBuildSettings.scenes.Select(item => item.path).ToArray();
            return Success(operation, dataJson: JsonArray(scenes), displayName: Application.productName, type: "UnityProject", scene: SceneManager.GetActiveScene().path);
        }

        private static AtomicOperationResult ManageScene(AtomicOperation operation, AtomicArgs args)
        {
            switch (operation.action)
            {
                case "list":
                    return Success(operation, dataJson: JsonArray(EditorBuildSettings.scenes.Select(item => item.path)), type: "SceneList");
                case "get_active":
                case "inspect":
                case "hierarchy":
                {
                    Scene scene = SceneManager.GetActiveScene();
                    string data = operation.action == "hierarchy"
                        ? JsonArray(scene.GetRootGameObjects().SelectMany(Flatten).Select(HierarchyPath))
                        : $"{{\"name\":{Quote(scene.name)},\"path\":{Quote(scene.path)},\"dirty\":{scene.isDirty.ToString().ToLowerInvariant()},\"rootCount\":{scene.rootCount}}}";
                    return Success(operation, dataJson: data, displayName: scene.name, type: "Scene", scene: scene.path, hierarchyPath: "/");
                }
                case "create":
                {
                    Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                    string path = RequireAssetPath(args.scenePath, ".unity");
                    Directory.CreateDirectory(Path.GetDirectoryName(path) ?? "Assets");
                    if (!EditorSceneManager.SaveScene(scene, path)) throw new IOException("Scene save failed: " + path);
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                    CreatedAssets.Add(path);
                    return Success(operation, displayName: scene.name, type: "Scene", scene: path, hierarchyPath: "/", assetGuid: AssetDatabase.AssetPathToGUID(path));
                }
                case "open":
                {
                    string path = RequireAssetPath(args.scenePath, ".unity");
                    Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                    return Success(operation, displayName: scene.name, type: "Scene", scene: path, hierarchyPath: "/", assetGuid: AssetDatabase.AssetPathToGUID(path));
                }
                case "set_active":
                {
                    string path = RequireAssetPath(args.scenePath, ".unity");
                    Scene scene = SceneManager.GetSceneByPath(path);
                    if (!scene.IsValid() || !scene.isLoaded) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                    if (!SceneManager.SetActiveScene(scene)) throw new InvalidOperationException("Failed to set active scene: " + path);
                    return Success(operation, displayName: scene.name, type: "Scene", scene: path, hierarchyPath: "/", assetGuid: AssetDatabase.AssetPathToGUID(path));
                }
                case "save":
                    EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
                    return Success(operation, displayName: SceneManager.GetActiveScene().name, type: "Scene", scene: SceneManager.GetActiveScene().path);
                case "save_as":
                {
                    string path = RequireAssetPath(args.scenePath, ".unity");
                    Directory.CreateDirectory(Path.GetDirectoryName(path) ?? "Assets");
                    if (!EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), path)) throw new IOException("Scene save failed: " + path);
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                    CreatedAssets.Add(path);
                    return Success(operation, displayName: SceneManager.GetActiveScene().name, type: "Scene", scene: path, assetGuid: AssetDatabase.AssetPathToGUID(path));
                }
                default: return Failure(operation, "PropertyInvalid", $"Unsupported scene action: {operation.action}");
            }
        }

        private static AtomicOperationResult ManageGameObject(AtomicOperation operation, AtomicArgs args)
        {
            if (operation.action == "create")
            {
                var gameObject = new GameObject(string.IsNullOrWhiteSpace(args.name) ? "GameObject" : args.name);
                Undo.RegisterCreatedObjectUndo(gameObject, "Create GameObject");
                if (!string.IsNullOrWhiteSpace(args.parent)) gameObject.transform.SetParent(FindGameObject(args.parent).transform, false);
                ApplyTransform(gameObject.transform, args);
                gameObject.SetActive(args.active);
                return Identity(operation, gameObject);
            }
            GameObject target = FindGameObject(args.target);
            switch (operation.action)
            {
                case "inspect": return Identity(operation, target, ComponentData(target));
                case "delete":
                {
                    string deletedName = target.name;
                    Undo.DestroyObjectImmediate(target);
                    return Success(operation, displayName: deletedName, type: "GameObject");
                }
                case "rename": Undo.RecordObject(target, "Rename GameObject"); target.name = args.name; return Identity(operation, target);
                case "duplicate":
                {
                    GameObject copy = Object.Instantiate(target, target.transform.parent);
                    copy.name = string.IsNullOrWhiteSpace(args.name) ? target.name + " Copy" : args.name;
                    Undo.RegisterCreatedObjectUndo(copy, "Duplicate GameObject");
                    return Identity(operation, copy);
                }
                case "enable": Undo.RecordObject(target, "Enable GameObject"); target.SetActive(true); return Identity(operation, target);
                case "disable": Undo.RecordObject(target, "Disable GameObject"); target.SetActive(false); return Identity(operation, target);
                case "set_parent": Undo.SetTransformParent(target.transform, FindGameObject(args.parent).transform, "Set Parent"); return Identity(operation, target);
                case "set_sibling": Undo.RecordObject(target.transform, "Set Sibling"); target.transform.SetSiblingIndex(int.Parse(args.value, CultureInfo.InvariantCulture)); return Identity(operation, target);
                case "set_transform": Undo.RecordObject(target.transform, "Set Transform"); ApplyTransform(target.transform, args); return Identity(operation, target);
                case "set_layer": Undo.RecordObject(target, "Set Layer"); target.layer = args.layer; return Identity(operation, target);
                case "set_tag": Undo.RecordObject(target, "Set Tag"); target.tag = args.tag; return Identity(operation, target);
                case "find": return Identity(operation, target);
                default: return Failure(operation, "PropertyInvalid", $"Unsupported GameObject action: {operation.action}");
            }
        }

        private static AtomicOperationResult ManageComponent(AtomicOperation operation, AtomicArgs args)
        {
            GameObject target = FindGameObject(args.target);
            if (operation.action == "list") return Identity(operation, target, ComponentData(target));
            Type type = FindType(args.type);
            Component component = target.GetComponent(type);
            if (operation.action == "add")
            {
                if (component == null) component = Undo.AddComponent(target, type);
                return ComponentIdentity(operation, component);
            }
            if (component == null) return Failure(operation, "ComponentMissing", $"{target.name} has no {args.type}");
            switch (operation.action)
            {
                case "inspect": return ComponentIdentity(operation, component, SerializedData(component));
                case "remove": Undo.DestroyObjectImmediate(component); return Success(operation, displayName: target.name, type: args.type);
                case "enable": SetEnabled(component, true); return ComponentIdentity(operation, component);
                case "disable": SetEnabled(component, false); return ComponentIdentity(operation, component);
                case "set_property": SetSerializedProperty(component, args); return ComponentIdentity(operation, component, SerializedData(component));
                default: return Failure(operation, "PropertyInvalid", $"Unsupported component action: {operation.action}");
            }
        }

        private static AtomicOperationResult ManagePrefab(AtomicOperation operation, AtomicArgs args)
        {
            switch (operation.action)
            {
                case "create":
                {
                    GameObject target = FindGameObject(args.target);
                    string path = RequireAssetPath(args.assetPath, ".prefab");
                    Directory.CreateDirectory(Path.GetDirectoryName(path) ?? "Assets");
                    GameObject prefab = PrefabUtility.SaveAsPrefabAsset(target, path);
                    if (prefab == null) throw new InvalidOperationException("Prefab save failed.");
                    CreatedAssets.Add(path);
                    return Success(operation, displayName: prefab.name, type: "Prefab", assetGuid: AssetDatabase.AssetPathToGUID(path), dataJson: $"{{\"path\":{Quote(path)}}}");
                }
                case "inspect":
                {
                    string path = RequireAssetPath(args.assetPath, ".prefab");
                    GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (prefab == null) throw new FileNotFoundException("Prefab not found", path);
                    return Success(operation, displayName: prefab.name, type: "Prefab", assetGuid: AssetDatabase.AssetPathToGUID(path), dataJson: ComponentData(prefab));
                }
                case "instantiate":
                {
                    string path = RequireAssetPath(args.assetPath, ".prefab");
                    GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (prefab == null) throw new FileNotFoundException("Prefab not found", path);
                    GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, SceneManager.GetActiveScene());
                    Undo.RegisterCreatedObjectUndo(instance, "Instantiate Prefab");
                    if (!string.IsNullOrWhiteSpace(args.parent)) instance.transform.SetParent(FindGameObject(args.parent).transform, false);
                    return Identity(operation, instance);
                }
                case "locate_source":
                {
                    GameObject target = FindGameObject(args.target);
                    string path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(target);
                    return Identity(operation, target, $"{{\"source\":{Quote(path)}}}");
                }
                case "unpack":
                {
                    GameObject target = FindGameObject(args.target);
                    PrefabUtility.UnpackPrefabInstance(target, PrefabUnpackMode.OutermostRoot, InteractionMode.AutomatedAction);
                    return Identity(operation, target);
                }
                case "save":
                case "overwrite":
                {
                    GameObject target = FindGameObject(args.target);
                    string path = RequireAssetPath(args.assetPath, ".prefab");
                    Directory.CreateDirectory(Path.GetDirectoryName(path) ?? "Assets");
                    GameObject prefab = PrefabUtility.SaveAsPrefabAsset(target, path);
                    if (prefab == null) throw new InvalidOperationException("Prefab save failed.");
                    return Success(operation, displayName: prefab.name, type: "Prefab", assetGuid: AssetDatabase.AssetPathToGUID(path), dataJson: $"{{\"path\":{Quote(path)}}}");
                }
                case "apply":
                {
                    GameObject target = FindGameObject(args.target);
                    if (!PrefabUtility.IsPartOfPrefabInstance(target)) throw new InvalidOperationException("Target is not a prefab instance.");
                    PrefabUtility.ApplyPrefabInstance(PrefabUtility.GetOutermostPrefabInstanceRoot(target), InteractionMode.AutomatedAction);
                    return Identity(operation, target);
                }
                default: return Failure(operation, "PropertyInvalid", $"Unsupported prefab action: {operation.action}");
            }
        }

        private static AtomicOperationResult ManageMaterial(AtomicOperation operation, AtomicArgs args)
        {
            string path = RequireAssetPath(args.assetPath, ".mat");
            if (operation.action == "create")
            {
                Shader shader = Shader.Find(string.IsNullOrWhiteSpace(args.shader) ? "Sprites/Default" : args.shader);
                if (shader == null) throw new InvalidOperationException("Shader not found: " + args.shader);
                var material = new Material(shader) { name = string.IsNullOrWhiteSpace(args.name) ? Path.GetFileNameWithoutExtension(path) : args.name };
                Directory.CreateDirectory(Path.GetDirectoryName(path) ?? "Assets");
                AssetDatabase.CreateAsset(material, path);
                CreatedAssets.Add(path);
                return Success(operation, displayName: material.name, type: "Material", assetGuid: AssetDatabase.AssetPathToGUID(path));
            }
            Material target = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (target == null) throw new FileNotFoundException("Material not found", path);
            if (operation.action == "inspect")
                return Success(operation, displayName: target.name, type: "Material", assetGuid: AssetDatabase.AssetPathToGUID(path), dataJson: $"{{\"shader\":{Quote(target.shader?.name ?? "")}}}");
            if (operation.action == "set_property")
            {
                Undo.RecordObject(target, "Set Material Property");
                if (args.valueType == "color") target.SetColor(args.property, ParseColor(args));
                else if (args.valueType == "texture") target.SetTexture(args.property, AssetDatabase.LoadAssetAtPath<Texture>(args.value));
                else target.SetFloat(args.property, float.Parse(args.value, CultureInfo.InvariantCulture));
                EditorUtility.SetDirty(target);
                return Success(operation, displayName: target.name, type: "Material", assetGuid: AssetDatabase.AssetPathToGUID(path));
            }
            return Failure(operation, "PropertyInvalid", $"Unsupported material action: {operation.action}");
        }

        private static AtomicOperationResult ManageTexture(AtomicOperation operation, AtomicArgs args)
        {
            string path = RequireAssetPath(args.assetPath, null);
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return Failure(operation, "ObjectNotFound", "Texture importer not found: " + path);
            if (operation.action == "inspect")
            {
                string data = $"{{\"textureType\":{Quote(importer.textureType.ToString())},\"spriteMode\":{Quote(importer.spriteImportMode.ToString())},\"filterMode\":{Quote(importer.filterMode.ToString())},\"compression\":{Quote(importer.textureCompression.ToString())},\"alphaIsTransparency\":{importer.alphaIsTransparency.ToString().ToLowerInvariant()},\"mipmaps\":{importer.mipmapEnabled.ToString().ToLowerInvariant()},\"maxSize\":{importer.maxTextureSize},\"pixelsPerUnit\":{importer.spritePixelsPerUnit.ToString(CultureInfo.InvariantCulture)}}}";
                return Success(operation, displayName: Path.GetFileName(path), type: "TextureImporter", assetGuid: AssetDatabase.AssetPathToGUID(path), dataJson: data);
            }
            if (operation.action == "configure_importer")
            {
                importer.textureType = args.sprite ? TextureImporterType.Sprite : TextureImporterType.Default;
                importer.alphaSource = args.sprite ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
                importer.alphaIsTransparency = args.alphaIsTransparency;
                importer.mipmapEnabled = args.mipmaps;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.maxTextureSize = args.maxSize;
                importer.spritePixelsPerUnit = args.pixelsPerUnit;
                importer.spriteImportMode = args.mode.Equals("multiple", StringComparison.OrdinalIgnoreCase) ? SpriteImportMode.Multiple : SpriteImportMode.Single;
                importer.spritePivot = new Vector2(args.pivotX, args.pivotY);
                if (Enum.TryParse(args.filterMode, true, out FilterMode filter)) importer.filterMode = filter;
                importer.textureCompression = args.compression == "uncompressed" ? TextureImporterCompression.Uncompressed : TextureImporterCompression.Compressed;
                importer.SaveAndReimport();
                return Success(operation, displayName: Path.GetFileName(path), type: "TextureImporter", assetGuid: AssetDatabase.AssetPathToGUID(path));
            }
            if (operation.action == "slice_grid")
            {
                if (args.columns < 1 || args.rows < 1) throw new ArgumentException("columns and rows must be positive.");
                Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (texture == null) throw new FileNotFoundException("Texture not found", path);
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Multiple;
                float width = texture.width / (float)args.columns;
                float height = texture.height / (float)args.rows;
                var names = new List<string>();
                var rects = new List<Rect>();
                int index = 0;
                for (int row = 0; row < args.rows; row++)
                for (int column = 0; column < args.columns; column++)
                {
                    string generatedName = Path.GetFileNameWithoutExtension(path) + "_" + index.ToString("00", CultureInfo.InvariantCulture);
                    string name = args.spriteNames != null && index < args.spriteNames.Length && !string.IsNullOrWhiteSpace(args.spriteNames[index]) ? args.spriteNames[index] : generatedName;
                    names.Add(name);
                    rects.Add(new Rect(column * width, texture.height - ((row + 1) * height), width, height));
                    index++;
                }
                var serialized = new SerializedObject(importer);
                SerializedProperty spriteArray = serialized.FindProperty("m_SpriteSheet.m_Sprites") ?? serialized.FindProperty("spriteSheet.sprites");
                if (spriteArray == null) throw new InvalidOperationException("TextureImporter sprite array property unavailable.");
                spriteArray.arraySize = names.Count;
                for (int spriteIndex = 0; spriteIndex < names.Count; spriteIndex++)
                {
                    SerializedProperty element = spriteArray.GetArrayElementAtIndex(spriteIndex);
                    SetRelativeString(element, "m_Name", names[spriteIndex]);
                    SetRelativeRect(element, "m_Rect", rects[spriteIndex]);
                    SetRelativeInt(element, "m_Alignment", (int)SpriteAlignment.Custom);
                    SetRelativeVector2(element, "m_Pivot", new Vector2(args.pivotX, args.pivotY));
                    SetRelativeString(element, "m_SpriteID", GUID.Generate().ToString());
                    SerializedProperty internalId = element.FindPropertyRelative("m_InternalID");
                    if (internalId != null) internalId.longValue = Math.Abs((long)names[spriteIndex].GetHashCode()) + 21300000L;
                }
                serialized.ApplyModifiedPropertiesWithoutUndo();
                importer.SaveAndReimport();
                return Success(operation, displayName: Path.GetFileName(path), type: "SpriteSheet", assetGuid: AssetDatabase.AssetPathToGUID(path), dataJson: JsonArray(names));
            }
            if (operation.action == "list_sprites")
            {
                Sprite[] sprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().OrderBy(item => item.name, StringComparer.Ordinal).ToArray();
                return Success(operation, displayName: Path.GetFileName(path), type: "SpriteList", assetGuid: AssetDatabase.AssetPathToGUID(path), dataJson: JsonArray(sprites.Select(item => item.name)));
            }
            return Failure(operation, "PropertyInvalid", $"Unsupported texture action: {operation.action}");
        }

        private static AtomicOperationResult ManageAsset(AtomicOperation operation, AtomicArgs args)
        {
            switch (operation.action)
            {
                case "search":
                    return Success(operation, type: "AssetSearch", dataJson: JsonArray(AssetDatabase.FindAssets(args.filter ?? "").Select(AssetDatabase.GUIDToAssetPath)));
                case "inspect":
                {
                    string path = RequireAssetPath(args.assetPath, null);
                    Object asset = AssetDatabase.LoadMainAssetAtPath(path);
                    if (asset == null) throw new FileNotFoundException("Asset not found", path);
                    return Success(operation, displayName: asset.name, type: asset.GetType().FullName, assetGuid: AssetDatabase.AssetPathToGUID(path), dataJson: $"{{\"path\":{Quote(path)}}}");
                }
                case "guid":
                {
                    string path = RequireAssetPath(args.assetPath, null);
                    return Success(operation, displayName: Path.GetFileName(path), type: "Asset", assetGuid: AssetDatabase.AssetPathToGUID(path));
                }
                case "import": AssetDatabase.ImportAsset(RequireAssetPath(args.assetPath, null), ImportAssetOptions.ForceUpdate); return Success(operation, type: "Asset");
                case "refresh": AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport); return Success(operation, type: "AssetDatabase");
                case "create_folder":
                {
                    string path = RequireAssetPath(args.assetPath, null).TrimEnd('/');
                    Directory.CreateDirectory(path);
                    AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                    return Success(operation, displayName: Path.GetFileName(path), type: "Folder", assetGuid: AssetDatabase.AssetPathToGUID(path));
                }
                case "list_subassets":
                {
                    string path = RequireAssetPath(args.assetPath, null);
                    return Success(operation, displayName: Path.GetFileName(path), type: "SubAssets", assetGuid: AssetDatabase.AssetPathToGUID(path), dataJson: JsonArray(AssetDatabase.LoadAllAssetsAtPath(path).Where(item => item != null).OrderBy(item => item.name, StringComparer.Ordinal).Select(item => item.name)));
                }
                case "move":
                case "rename":
                {
                    string source = RequireAssetPath(args.assetPath, null);
                    string destination = RequireAssetPath(args.path, null);
                    string error = AssetDatabase.MoveAsset(source, destination);
                    if (!string.IsNullOrEmpty(error)) throw new InvalidOperationException(error);
                    return Success(operation, displayName: Path.GetFileName(destination), type: "Asset", assetGuid: AssetDatabase.AssetPathToGUID(destination));
                }
                case "delete":
                {
                    string path = RequireAssetPath(args.assetPath, null);
                    if (!path.StartsWith("Assets/AgentGenerated/", StringComparison.Ordinal)) throw new InvalidOperationException("Atomic delete is restricted to Assets/AgentGenerated.");
                    bool deleted = AssetDatabase.DeleteAsset(path);
                    return deleted ? Success(operation, displayName: Path.GetFileName(path), type: "Asset") : Failure(operation, "ObjectNotFound", path);
                }
                default: return Failure(operation, "PropertyInvalid", $"Unsupported asset action: {operation.action}");
            }
        }

        private static AtomicOperationResult ManageCamera(AtomicOperation operation, AtomicArgs args)
        {
            if (operation.action == "create")
            {
                var gameObject = new GameObject(string.IsNullOrWhiteSpace(args.name) ? "Camera" : args.name, typeof(Camera));
                Undo.RegisterCreatedObjectUndo(gameObject, "Create Camera");
                ApplyTransform(gameObject.transform, args);
                return ComponentIdentity(operation, gameObject.GetComponent<Camera>());
            }
            Camera camera = FindGameObject(args.target).GetComponent<Camera>();
            if (camera == null) return Failure(operation, "ComponentMissing", "Camera missing");
            if (operation.action == "inspect") return ComponentIdentity(operation, camera, SerializedData(camera));
            return Failure(operation, "PropertyInvalid", $"Unsupported camera action: {operation.action}");
        }

        private static AtomicOperationResult ManageAnimation(AtomicOperation operation, AtomicArgs args)
        {
            if (operation.action == "create_clip")
            {
                string path = RequireAssetPath(args.assetPath, ".anim");
                Sprite[] frames;
                if (args.assetPaths != null && args.assetPaths.Length > 0)
                {
                    frames = args.assetPaths.Select(assetPath =>
                    {
                        string framePath = RequireAssetPath(assetPath, null);
                        return AssetDatabase.LoadAssetAtPath<Sprite>(framePath)
                            ?? AssetDatabase.LoadAllAssetsAtPath(framePath).OfType<Sprite>().FirstOrDefault()
                            ?? throw new FileNotFoundException("Sprite frame not found", framePath);
                    }).ToArray();
                }
                else
                {
                    string spritePath = RequireAssetPath(args.spriteAssetPath, null);
                    IEnumerable<Sprite> sprites = AssetDatabase.LoadAllAssetsAtPath(spritePath).OfType<Sprite>();
                    if (args.spriteNames != null && args.spriteNames.Length > 0)
                    {
                        var order = args.spriteNames.Select((name, index) => new { name, index }).ToDictionary(item => item.name, item => item.index, StringComparer.Ordinal);
                        sprites = sprites.Where(item => order.ContainsKey(item.name)).OrderBy(item => order[item.name]);
                    }
                    else sprites = sprites.OrderBy(item => item.name, StringComparer.Ordinal);
                    frames = sprites.ToArray();
                }
                if (frames.Length == 0) throw new InvalidOperationException("No Sprite frames matched the request.");
                AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                if (clip == null)
                {
                    clip = new AnimationClip { name = Path.GetFileNameWithoutExtension(path) };
                    Directory.CreateDirectory(Path.GetDirectoryName(path) ?? "Assets");
                    AssetDatabase.CreateAsset(clip, path);
                    CreatedAssets.Add(path);
                }
                clip.frameRate = args.frameRate <= 0 ? 12f : args.frameRate;
                ObjectReferenceKeyframe[] keyframes;
                bool variableTiming = args.frameDurationsMs != null && args.frameDurationsMs.Length == frames.Length;
                if (variableTiming)
                {
                    var timed = new List<ObjectReferenceKeyframe>();
                    float time = 0f;
                    for (int frameIndex = 0; frameIndex < frames.Length; frameIndex++)
                    {
                        timed.Add(new ObjectReferenceKeyframe { time = time, value = frames[frameIndex] });
                        time += Math.Max(1f, args.frameDurationsMs[frameIndex]) / 1000f;
                    }
                    timed.Add(new ObjectReferenceKeyframe { time = time, value = frames[frames.Length - 1] });
                    keyframes = timed.ToArray();
                }
                else keyframes = frames.Select((sprite, index) => new ObjectReferenceKeyframe { time = index / clip.frameRate, value = sprite }).ToArray();
                var binding = new EditorCurveBinding { path = args.path ?? "", propertyName = "m_Sprite", type = typeof(SpriteRenderer) };
                AnimationUtility.SetObjectReferenceCurve(clip, binding, keyframes);
                var serialized = new SerializedObject(clip);
                SerializedProperty loop = serialized.FindProperty("m_AnimationClipSettings.m_LoopTime");
                if (loop != null) loop.boolValue = args.loop;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(clip);
                return Success(operation, displayName: clip.name, type: "AnimationClip", assetGuid: AssetDatabase.AssetPathToGUID(path), dataJson: $"{{\"frameRate\":{clip.frameRate.ToString(CultureInfo.InvariantCulture)},\"frames\":{frames.Length},\"variableTiming\":{variableTiming.ToString().ToLowerInvariant()},\"loop\":{args.loop.ToString().ToLowerInvariant()}}}");
            }
            if (operation.action == "create_controller")
            {
                string path = RequireAssetPath(args.assetPath, ".controller");
                AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
                if (controller == null)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(path) ?? "Assets");
                    controller = AnimatorController.CreateAnimatorControllerAtPath(path);
                    CreatedAssets.Add(path);
                }
                return Success(operation, displayName: controller.name, type: "AnimatorController", assetGuid: AssetDatabase.AssetPathToGUID(path));
            }
            if (operation.action == "add_parameter")
            {
                string path = RequireAssetPath(args.assetPath, ".controller");
                AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path) ?? throw new FileNotFoundException("AnimatorController not found", path);
                if (!controller.parameters.Any(item => item.name == args.parameter))
                {
                    if (!Enum.TryParse(args.parameterType, true, out AnimatorControllerParameterType type)) throw new ArgumentException("Invalid parameterType: " + args.parameterType);
                    controller.AddParameter(args.parameter, type);
                }
                return Success(operation, displayName: controller.name, type: "AnimatorController", assetGuid: AssetDatabase.AssetPathToGUID(path));
            }
            if (operation.action == "add_state")
            {
                string path = RequireAssetPath(args.assetPath, ".controller");
                AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path) ?? throw new FileNotFoundException("AnimatorController not found", path);
                AnimatorStateMachine machine = controller.layers[Math.Max(0, Math.Min(args.layer, controller.layers.Length - 1))].stateMachine;
                AnimatorState state = machine.states.Select(item => item.state).FirstOrDefault(item => item.name == args.state) ?? machine.AddState(args.state);
                if (!string.IsNullOrWhiteSpace(args.path)) state.motion = AssetDatabase.LoadAssetAtPath<Motion>(RequireAssetPath(args.path, ".anim"));
                if (machine.defaultState == null) machine.defaultState = state;
                EditorUtility.SetDirty(controller);
                return Success(operation, displayName: state.name, type: "AnimatorState", assetGuid: AssetDatabase.AssetPathToGUID(path));
            }
            if (operation.action == "set_default_state")
            {
                string path = RequireAssetPath(args.assetPath, ".controller");
                AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path) ?? throw new FileNotFoundException("AnimatorController not found", path);
                AnimatorStateMachine machine = controller.layers[Math.Max(0, Math.Min(args.layer, controller.layers.Length - 1))].stateMachine;
                machine.defaultState = RequireState(machine, args.state);
                EditorUtility.SetDirty(controller);
                return Success(operation, displayName: args.state, type: "AnimatorState", assetGuid: AssetDatabase.AssetPathToGUID(path));
            }
            if (operation.action == "add_transition")
            {
                string path = RequireAssetPath(args.assetPath, ".controller");
                AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path) ?? throw new FileNotFoundException("AnimatorController not found", path);
                AnimatorStateMachine machine = controller.layers[Math.Max(0, Math.Min(args.layer, controller.layers.Length - 1))].stateMachine;
                AnimatorState source = RequireState(machine, args.fromState);
                AnimatorState destination = RequireState(machine, args.toState);
                AnimatorStateTransition transition = source.transitions.FirstOrDefault(item => item.destinationState == destination) ?? source.AddTransition(destination);
                transition.hasExitTime = args.hasExitTime;
                transition.exitTime = args.exitTime;
                transition.duration = args.transitionDuration;
                if (!string.IsNullOrWhiteSpace(args.parameter) && transition.conditions.All(item => item.parameter != args.parameter))
                {
                    if (!Enum.TryParse(args.conditionMode, true, out AnimatorConditionMode mode)) throw new ArgumentException("Invalid conditionMode: " + args.conditionMode);
                    transition.AddCondition(mode, args.threshold, args.parameter);
                }
                EditorUtility.SetDirty(controller);
                return Success(operation, displayName: args.fromState + "->" + args.toState, type: "AnimatorTransition", assetGuid: AssetDatabase.AssetPathToGUID(path));
            }
            if (operation.action == "inspect_controller")
            {
                string path = RequireAssetPath(args.assetPath, ".controller");
                AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path) ?? throw new FileNotFoundException("AnimatorController not found", path);
                string[] states = controller.layers.SelectMany(layer => layer.stateMachine.states).Select(item => item.state.name).OrderBy(item => item, StringComparer.Ordinal).ToArray();
                return Success(operation, displayName: controller.name, type: "AnimatorController", assetGuid: AssetDatabase.AssetPathToGUID(path), dataJson: $"{{\"parameters\":{JsonArray(controller.parameters.Select(item => item.name))},\"states\":{JsonArray(states)}}}");
            }
            Animator animator = FindGameObject(args.target).GetComponent<Animator>();
            if (animator == null) return Failure(operation, "ComponentMissing", "Animator missing");
            if (operation.action == "inspect_animator") return ComponentIdentity(operation, animator, SerializedData(animator));
            if (operation.action == "assign_controller")
            {
                Undo.RecordObject(animator, "Assign Animator Controller");
                animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(args.assetPath);
                if (animator.runtimeAnimatorController == null) throw new FileNotFoundException("AnimatorController not found", args.assetPath);
                return ComponentIdentity(operation, animator);
            }
            return Failure(operation, "PropertyInvalid", $"Unsupported animation action: {operation.action}");
        }

        private static AtomicOperationResult ManageUi(AtomicOperation operation, AtomicArgs args)
        {
            GameObject target = FindGameObject(args.target);
            if (operation.action == "inspect_uidocument")
            {
                UIDocument document = target.GetComponent<UIDocument>();
                if (document == null) return Failure(operation, "ComponentMissing", "UIDocument missing");
                return ComponentIdentity(operation, document, $"{{\"panelSettings\":{Quote(AssetDatabase.GetAssetPath(document.panelSettings))},\"visualTree\":{Quote(AssetDatabase.GetAssetPath(document.visualTreeAsset))}}}");
            }
            return Failure(operation, "PropertyInvalid", $"Unsupported UI action: {operation.action}");
        }

        private static AtomicOperationResult ManageBuild(AtomicOperation operation, AtomicArgs args)
        {
            if (operation.action == "inspect" || operation.action == "list_scenes")
            {
                string data = $"{{\"activeTarget\":{Quote(EditorUserBuildSettings.activeBuildTarget.ToString())},\"development\":{EditorUserBuildSettings.development.ToString().ToLowerInvariant()},\"scenes\":{JsonArray(EditorBuildSettings.scenes.Select(item => item.path))}}}";
                return Success(operation, type: "BuildSettings", dataJson: data);
            }
            if (operation.action == "set_development")
            {
                EditorUserBuildSettings.development = args.development;
                return Success(operation, type: "BuildSettings", dataJson: $"{{\"development\":{args.development.ToString().ToLowerInvariant()}}}");
            }
            if (operation.action == "set_scenes")
            {
                string[] paths = args.assetPaths ?? Array.Empty<string>();
                foreach (string path in paths) RequireAssetPath(path, ".unity");
                EditorBuildSettings.scenes = paths.Select(path => new EditorBuildSettingsScene(path, true)).ToArray();
                return Success(operation, type: "BuildSettings", dataJson: JsonArray(paths));
            }
            if (operation.action == "build_windows_x64")
            {
                if (string.IsNullOrWhiteSpace(args.outputPath) || !Path.IsPathRooted(args.outputPath) || !args.outputPath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    throw new ArgumentException("outputPath must be an absolute .exe path.");
                Directory.CreateDirectory(Path.GetDirectoryName(args.outputPath) ?? throw new ArgumentException("outputPath has no directory."));
                var options = new BuildPlayerOptions
                {
                    scenes = EditorBuildSettings.scenes.Where(item => item.enabled).Select(item => item.path).ToArray(),
                    locationPathName = args.outputPath,
                    target = BuildTarget.StandaloneWindows64,
                    options = args.development ? BuildOptions.Development : BuildOptions.None,
                };
                var report = BuildPipeline.BuildPlayer(options);
                bool success = report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded;
                string data = $"{{\"result\":{Quote(report.summary.result.ToString())},\"errors\":{report.summary.totalErrors},\"warnings\":{report.summary.totalWarnings},\"outputPath\":{Quote(args.outputPath)}}}";
                return success ? Success(operation, displayName: Path.GetFileName(args.outputPath), type: "WindowsPlayer", dataJson: data) : Failure(operation, "BuildFailed", data);
            }
            return Failure(operation, "PropertyInvalid", $"Unsupported build action: {operation.action}");
        }

        private static AtomicOperationResult ManageScreenshot(AtomicOperation operation, AtomicArgs args)
        {
            if (operation.action != "capture_camera") return Failure(operation, "PropertyInvalid", $"Unsupported screenshot action: {operation.action}");
            if (string.IsNullOrWhiteSpace(args.outputPath) || !Path.IsPathRooted(args.outputPath) || !args.outputPath.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("outputPath must be an absolute .png path.");
            Camera camera = string.IsNullOrWhiteSpace(args.target) ? Camera.main : FindGameObject(args.target).GetComponent<Camera>();
            if (camera == null) throw new MissingReferenceException("Capture camera not found.");
            int width = args.columns > 1 ? args.columns : 1280;
            int height = args.rows > 1 ? args.rows : 720;
            var renderTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            RenderTexture previous = RenderTexture.active;
            RenderTexture previousTarget = camera.targetTexture;
            try
            {
                camera.targetTexture = renderTexture;
                RenderTexture.active = renderTexture;
                camera.Render();
                texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                texture.Apply();
                Directory.CreateDirectory(Path.GetDirectoryName(args.outputPath) ?? throw new ArgumentException("outputPath has no directory."));
                File.WriteAllBytes(args.outputPath, texture.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previous;
                Object.DestroyImmediate(texture);
                Object.DestroyImmediate(renderTexture);
            }
            return Success(operation, displayName: Path.GetFileName(args.outputPath), type: "Screenshot", dataJson: $"{{\"path\":{Quote(args.outputPath)},\"width\":{width},\"height\":{height}}}");
        }

        private static AtomicOperationResult ManageEditor(AtomicOperation operation)
        {
            if (operation.action == "refresh") { AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport); return Success(operation, type: "Editor"); }
            if (operation.action == "compile_status") return Success(operation, type: "CompileStatus", dataJson: $"{{\"isCompiling\":{EditorApplication.isCompiling.ToString().ToLowerInvariant()},\"isUpdating\":{EditorApplication.isUpdating.ToString().ToLowerInvariant()}}}");
            return Failure(operation, "PropertyInvalid", $"Unsupported editor action: {operation.action}");
        }

        private static void SetSerializedProperty(Component component, AtomicArgs args)
        {
            var serialized = new SerializedObject(component);
            SerializedProperty property = serialized.FindProperty(args.property);
            if (property == null) throw new ArgumentException("Serialized property not found: " + args.property);
            Undo.RecordObject(component, "Set Serialized Property");
            switch (property.propertyType)
            {
                case SerializedPropertyType.Boolean: property.boolValue = bool.Parse(args.value); break;
                case SerializedPropertyType.Integer: property.intValue = int.Parse(args.value, CultureInfo.InvariantCulture); break;
                case SerializedPropertyType.Float: property.floatValue = float.Parse(args.value, CultureInfo.InvariantCulture); break;
                case SerializedPropertyType.String: property.stringValue = args.value ?? ""; break;
                case SerializedPropertyType.Enum:
                    property.enumValueIndex = int.TryParse(args.value, out int index) ? index : Array.IndexOf(property.enumDisplayNames, args.value);
                    break;
                case SerializedPropertyType.Vector2: property.vector2Value = ParseVector2(args.value); break;
                case SerializedPropertyType.Vector3: property.vector3Value = ParseVector3(args.value); break;
                case SerializedPropertyType.Vector4: property.vector4Value = ParseVector4(args.value); break;
                case SerializedPropertyType.Color: property.colorValue = ParseColor(args); break;
                case SerializedPropertyType.ObjectReference:
                    if (string.IsNullOrWhiteSpace(args.subAsset))
                    {
                        property.objectReferenceValue = AssetDatabase.LoadMainAssetAtPath(args.value);
                    }
                    else
                    {
                        Object[] candidates = AssetDatabase.LoadAllAssetsAtPath(args.value)
                            .Where(item => item != null && item.name == args.subAsset).ToArray();
                        string expectedType = property.type ?? string.Empty;
                        property.objectReferenceValue = candidates.FirstOrDefault(item =>
                            expectedType.IndexOf(item.GetType().Name, StringComparison.OrdinalIgnoreCase) >= 0)
                            ?? candidates.FirstOrDefault();
                    }
                    if (property.objectReferenceValue == null) throw new FileNotFoundException("Object reference asset not found", args.value + "#" + args.subAsset);
                    break;
                default: throw new NotSupportedException("Serialized property type unsupported: " + property.propertyType);
            }
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(component);
        }

        private static void SetEnabled(Component component, bool enabled)
        {
            PropertyInfo property = component.GetType().GetProperty("enabled", BindingFlags.Instance | BindingFlags.Public);
            if (property == null || property.PropertyType != typeof(bool)) throw new InvalidOperationException("Component has no enabled property.");
            Undo.RecordObject(component, "Set Component Enabled");
            property.SetValue(component, enabled);
        }

        private static Type FindType(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Component type is required.");
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type exact = assembly.GetType(name, false, true);
                if (exact != null && typeof(Component).IsAssignableFrom(exact)) return exact;
                Type shortName = null;
                try { shortName = assembly.GetTypes().FirstOrDefault(item => item.Name.Equals(name, StringComparison.OrdinalIgnoreCase)); }
                catch (ReflectionTypeLoadException exception) { shortName = exception.Types.FirstOrDefault(item => item != null && item.Name.Equals(name, StringComparison.OrdinalIgnoreCase)); }
                if (shortName != null && typeof(Component).IsAssignableFrom(shortName)) return shortName;
            }
            throw new TypeLoadException("Component type not found: " + name);
        }

        private static AnimatorState RequireState(AnimatorStateMachine machine, string name)
        {
            AnimatorState state = machine.states.Select(item => item.state).FirstOrDefault(item => item.name == name);
            if (state == null) throw new MissingReferenceException("Animator state not found: " + name);
            return state;
        }

        private static void SetRelativeString(SerializedProperty parent, string name, string value)
        {
            SerializedProperty property = parent.FindPropertyRelative(name);
            if (property != null) property.stringValue = value ?? string.Empty;
        }

        private static void SetRelativeInt(SerializedProperty parent, string name, int value)
        {
            SerializedProperty property = parent.FindPropertyRelative(name);
            if (property != null) property.intValue = value;
        }

        private static void SetRelativeRect(SerializedProperty parent, string name, Rect value)
        {
            SerializedProperty property = parent.FindPropertyRelative(name);
            if (property != null) property.rectValue = value;
        }

        private static void SetRelativeVector2(SerializedProperty parent, string name, Vector2 value)
        {
            SerializedProperty property = parent.FindPropertyRelative(name);
            if (property != null) property.vector2Value = value;
        }

        private static GameObject FindGameObject(string target)
        {
            if (string.IsNullOrWhiteSpace(target)) throw new ArgumentException("GameObject target is required.");
            if (GlobalObjectId.TryParse(target, out GlobalObjectId globalId))
            {
                Object resolved = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(globalId);
                if (resolved is GameObject gameObject) return gameObject;
                if (resolved is Component component) return component.gameObject;
            }
            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
                foreach (GameObject gameObject in Flatten(root))
                    if (HierarchyPath(gameObject).Equals(target, StringComparison.Ordinal) || gameObject.name.Equals(target, StringComparison.Ordinal))
                        return gameObject;
            throw new MissingReferenceException("GameObject not found: " + target);
        }

        private static IEnumerable<GameObject> Flatten(GameObject root)
        {
            yield return root;
            foreach (Transform child in root.transform)
                foreach (GameObject descendant in Flatten(child.gameObject)) yield return descendant;
        }

        private static string HierarchyPath(GameObject gameObject)
        {
            var names = new Stack<string>();
            Transform current = gameObject.transform;
            while (current != null) { names.Push(current.name); current = current.parent; }
            return "/" + string.Join("/", names);
        }

        private static void ApplyTransform(Transform transform, AtomicArgs args)
        {
            if (args.position?.Length >= 3) { Vector3 value = new(args.position[0], args.position[1], args.position[2]); if (args.world) transform.position = value; else transform.localPosition = value; }
            if (args.rotation?.Length >= 3) { Vector3 value = new(args.rotation[0], args.rotation[1], args.rotation[2]); if (args.world) transform.eulerAngles = value; else transform.localEulerAngles = value; }
            if (args.scale?.Length >= 3) transform.localScale = new Vector3(args.scale[0], args.scale[1], args.scale[2]);
        }

        private static AtomicOperationResult Identity(AtomicOperation operation, GameObject gameObject, string dataJson = "")
        {
            return Success(operation, displayName: gameObject.name, type: "GameObject", scene: gameObject.scene.path, hierarchyPath: HierarchyPath(gameObject), stableId: GlobalObjectId.GetGlobalObjectIdSlow(gameObject).ToString(), dataJson: dataJson);
        }

        private static AtomicOperationResult ComponentIdentity(AtomicOperation operation, Component component, string dataJson = "")
        {
            return Success(operation, displayName: component.gameObject.name, type: component.GetType().FullName, scene: component.gameObject.scene.path, hierarchyPath: HierarchyPath(component.gameObject), stableId: GlobalObjectId.GetGlobalObjectIdSlow(component).ToString(), dataJson: dataJson);
        }

        private static string ComponentData(GameObject gameObject) => JsonArray(gameObject.GetComponents<Component>().Where(item => item != null).Select(item => item.GetType().FullName));

        private static string SerializedData(Object target)
        {
            var serialized = new SerializedObject(target);
            SerializedProperty iterator = serialized.GetIterator();
            var names = new List<string>();
            if (iterator.NextVisible(true)) do { names.Add(iterator.propertyPath); } while (iterator.NextVisible(false));
            return JsonArray(names);
        }

        private static AtomicOperationResult Success(AtomicOperation operation, string displayName = "", string type = "", string scene = "", string hierarchyPath = "", string stableId = "", string assetGuid = "", string dataJson = "")
        {
            return new AtomicOperationResult { index = operation.index, capability = operation.capability, action = operation.action, success = true, displayName = displayName, type = type, scene = scene, hierarchyPath = hierarchyPath, stableId = stableId, assetGuid = assetGuid, dataJson = dataJson };
        }

        private static AtomicOperationResult Failure(AtomicOperation operation, string errorCode, string error)
        {
            return new AtomicOperationResult { index = operation.index, capability = operation.capability, action = operation.action, success = false, errorCode = errorCode, error = error };
        }

        private static string Classify(Exception exception)
        {
            return exception switch
            {
                MissingReferenceException => "ObjectNotFound",
                FileNotFoundException => "ObjectNotFound",
                TypeLoadException => "ComponentMissing",
                ArgumentException => "PropertyInvalid",
                NotSupportedException => "PropertyInvalid",
                _ => "Tool",
            };
        }

        private static string RequireAssetPath(string path, string extension)
        {
            string normalized = (path ?? "").Replace('\\', '/');
            if (!normalized.StartsWith("Assets/", StringComparison.Ordinal) || normalized.Contains("../")) throw new ArgumentException("Path must stay under Assets/: " + path);
            if (!string.IsNullOrEmpty(extension) && !normalized.EndsWith(extension, StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Expected " + extension + ": " + path);
            return normalized;
        }

        private static Vector2 ParseVector2(string value) { float[] v = ParseFloats(value, 2); return new Vector2(v[0], v[1]); }
        private static Vector3 ParseVector3(string value) { float[] v = ParseFloats(value, 3); return new Vector3(v[0], v[1], v[2]); }
        private static Vector4 ParseVector4(string value) { float[] v = ParseFloats(value, 4); return new Vector4(v[0], v[1], v[2], v[3]); }
        private static Color ParseColor(AtomicArgs args) { float[] v = args.color?.Length >= 3 ? args.color : ParseFloats(args.value, 4, 1f); return new Color(v[0], v[1], v[2], v.Length > 3 ? v[3] : 1f); }
        private static float[] ParseFloats(string value, int count, float defaultLast = 0f)
        {
            string[] parts = (value ?? "").Split(',');
            var result = Enumerable.Repeat(defaultLast, count).ToArray();
            for (int index = 0; index < Math.Min(parts.Length, count); index++) result[index] = float.Parse(parts[index], CultureInfo.InvariantCulture);
            return result;
        }

        private static string Quote(string value) => "\"" + (value ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n") + "\"";
        private static string JsonArray(IEnumerable<string> values) => "[" + string.Join(",", values.Select(Quote)) + "]";

        private static string ReadArgument(string key)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int index = 0; index < args.Length - 1; index++) if (args[index].Equals(key, StringComparison.OrdinalIgnoreCase)) return args[index + 1];
            return string.Empty;
        }
    }

    [InitializeOnLoad]
    public static class AtomicBridgePump
    {
        private static double nextPoll;

        static AtomicBridgePump() => EditorApplication.update += Update;

        private static void Update()
        {
            if (EditorApplication.timeSinceStartup < nextPoll || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            nextPoll = EditorApplication.timeSinceStartup + 0.25;
            string inbox = Path.Combine(Directory.GetParent(Application.dataPath)?.FullName ?? ".", "Library", "UnityAutonomousAgent", "bridge-inbox");
            if (!Directory.Exists(inbox)) return;
            foreach (string request in Directory.GetFiles(inbox, "*.json").OrderBy(path => path))
            {
                try
                {
                    AtomicUnityWorker.ProcessRequest(request);
                    string processed = Path.Combine(inbox, "processed");
                    Directory.CreateDirectory(processed);
                    string completed = Path.Combine(processed, Path.GetFileName(request));
                    if (File.Exists(completed)) File.Delete(completed);
                    File.Move(request, completed);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                    string failed = request + ".failed";
                    if (File.Exists(failed)) File.Delete(failed);
                    File.Move(request, failed);
                }
            }
        }
    }
}
