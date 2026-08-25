using System.IO;
using System.Linq;
using ThreeKingdoms;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class FinalStageSetup
{
    private const string SourceRoot=@"C:\Users\shanghai\.codex\generated_images\01a031c2-ef92-7770-9fd1-31ee8482e711";
    private const string ArtRoot="Assets/Art/Backgrounds/FinalStage";
    private const string BaseScene="Assets/Scenes/SC_Stage01_AncientStreet.unity";
    public const string EntranceScene="Assets/Scenes/SC_FinalStage_Entrance.unity";
    public const string ApproachScene="Assets/Scenes/SC_FinalStage_StormGate.unity";
    public const string BossScene="Assets/Scenes/SC_FinalBoss_CaoCao.unity";

    [MenuItem("Three Kingdoms/Final Stage/Build Storm Gate + Cao Cao Boss Rooms")]
    public static void Setup()
    {
        EnsureFolder("Assets/Art");EnsureFolder("Assets/Art/Backgrounds");EnsureFolder(ArtRoot);
        Sprite[] backgrounds=new Sprite[3];
        for(int i=0;i<3;i++)backgrounds[i]=ImportBackground(i+1);
        CreateEntranceScene(backgrounds[0]);
        CreateApproachScene(backgrounds[1]);
        CreateBossScene(backgrounds[2]);
        EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(EntranceScene,true),new EditorBuildSettingsScene(ApproachScene,true),new EditorBuildSettingsScene(BossScene,true)};
        AssetDatabase.SaveAssets();Debug.Log("FINAL_STAGE_SETUP_OK scenes=3 backgrounds=3 flow=1_portal_to_2_two_waves_portal_to_3 boss=CaoCao");
    }

    public static void CaptureEvidence()
    {
        const string outputRoot=@"C:\Users\shanghai\Desktop\三国战纪\Screenshots\FinalStage";
        Directory.CreateDirectory(outputRoot);
        CaptureScene(EntranceScene,4.2f,Path.Combine(outputRoot,"01_InitialArea.png"));
        CaptureScene(ApproachScene,4.2f,Path.Combine(outputRoot,"02_SealedGate.png"));
        CaptureScene(BossScene,4.2f,Path.Combine(outputRoot,"03_CaoCaoBossRoom.png"));
        Debug.Log("FINAL_STAGE_CAPTURE_OK count=3 output="+outputRoot);
    }

    private static void CaptureScene(string scenePath,float cameraX,string outputPath)
    {
        EditorSceneManager.OpenScene(scenePath,OpenSceneMode.Single);
        Camera camera=Object.FindFirstObjectByType<Camera>();
        if(camera==null)throw new MissingComponentException("Camera missing in "+scenePath);
        Vector3 position=camera.transform.position;position.x=cameraX;camera.transform.position=position;
        camera.orthographic=true;camera.orthographicSize=5.1f;camera.clearFlags=CameraClearFlags.SolidColor;
        const int width=1280,height=720;
        var renderTexture=new RenderTexture(width,height,24,RenderTextureFormat.ARGB32);
        var texture=new Texture2D(width,height,TextureFormat.RGBA32,false);
        RenderTexture oldActive=RenderTexture.active;RenderTexture oldTarget=camera.targetTexture;
        try
        {
            camera.targetTexture=renderTexture;RenderTexture.active=renderTexture;camera.Render();
            texture.ReadPixels(new Rect(0,0,width,height),0,0);texture.Apply();File.WriteAllBytes(outputPath,texture.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture=oldTarget;RenderTexture.active=oldActive;Object.DestroyImmediate(texture);Object.DestroyImmediate(renderTexture);
        }
    }

    private static Sprite ImportBackground(int index)
    {
        string assetPath=$"{ArtRoot}/final_stage_{index}.png",source=Path.Combine(SourceRoot,index+".png"),absolute=Path.GetFullPath(assetPath);
        if(!File.Exists(source))throw new FileNotFoundException("Generated final-stage image is missing",source);
        Directory.CreateDirectory(Path.GetDirectoryName(absolute));File.Copy(source,absolute,true);AssetDatabase.ImportAsset(assetPath,ImportAssetOptions.ForceSynchronousImport);
        var importer=AssetImporter.GetAtPath(assetPath) as TextureImporter;if(importer==null)throw new FileLoadException("TextureImporter missing",assetPath);
        importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;importer.spritePixelsPerUnit=100f;importer.mipmapEnabled=false;
        importer.alphaIsTransparency=false;importer.sRGBTexture=true;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.filterMode=FilterMode.Bilinear;importer.maxTextureSize=2048;importer.SaveAndReimport();
        Sprite sprite=AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);if(sprite==null)throw new FileLoadException("Final-stage Sprite import failed",assetPath);return sprite;
    }

    private static void CreateEntranceScene(Sprite background)
    {
        Scene scene=EditorSceneManager.OpenScene(BaseScene,OpenSceneMode.Single);EditorSceneManager.SaveScene(scene,EntranceScene);scene=SceneManager.GetActiveScene();
        ConfigureStage(scene,StageManager.StageMode.FinalEntrance,"SC_FinalStage_StormGate",9.1f,7.55f,-.15f,-2.55f,-4.15f,-.2f,-1.75f,-1.7f);
        ConfigureBackground(new[]{background},4.2f,18.12f);ConfigureCamera(4.2f,4.2f);
        ConfigureNavigation(new[]{new Vector2(-.8f,-4.15f),new Vector2(9.1f,-4.15f),new Vector2(9.1f,-.48f),new Vector2(7.5f,-.28f),new Vector2(3f,-.18f),new Vector2(-.8f,-.38f)});
        CreatePortalPrompt("EntranceGatePortal",7.55f,-1.1f,"ENTER  STORM  GATE  >>");
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene,EntranceScene);
    }

    private static void CreateApproachScene(Sprite background)
    {
        Scene scene=EditorSceneManager.OpenScene(BaseScene,OpenSceneMode.Single);EditorSceneManager.SaveScene(scene,ApproachScene);scene=SceneManager.GetActiveScene();
        ConfigureStage(scene,StageManager.StageMode.FinalApproach,"SC_FinalBoss_CaoCao",9.1f,7.65f,-.15f,-2.55f,-4.15f,-.42f,-1.75f,-1.7f);
        ConfigureBackground(new[]{background},4.2f,18.12f);ConfigureCamera(4.2f,4.2f);
        ConfigureNavigation(new[]{new Vector2(-.8f,-4.15f),new Vector2(9.1f,-4.15f),new Vector2(9.1f,-.55f),new Vector2(6.7f,-.42f),new Vector2(2f,-.42f),new Vector2(-.8f,-.55f)});
        CreatePortalPrompt("BossGatePortal",7.65f,-1.1f,"ENTER  FINAL  BOSS  ROOM  >>");RemoveObject("EntranceGatePortal");
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene,ApproachScene);
    }

    private static void CreateBossScene(Sprite background)
    {
        Scene scene=EditorSceneManager.OpenScene(BaseScene,OpenSceneMode.Single);EditorSceneManager.SaveScene(scene,BossScene);scene=SceneManager.GetActiveScene();
        ConfigureStage(scene,StageManager.StageMode.FinalBoss,string.Empty,9.2f,99f,-.15f,-2.85f,-4.35f,.7f,-1.75f,-2.15f);
        ConfigureBackground(new[]{background},4.2f,18.12f);ConfigureCamera(4.2f,4.2f);
        ConfigureNavigation(new[]{new Vector2(-.8f,-4.35f),new Vector2(9.2f,-4.35f),new Vector2(9.2f,.7f),new Vector2(-.8f,.7f)});RemoveObject("BossGatePortal");RemoveObject("EntranceGatePortal");
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene,BossScene);
    }

    private static void ConfigureStage(Scene scene,StageManager.StageMode mode,string nextScene,float exitX,float portalX,float spawnX,float spawnDepth,float minDepth,float maxDepth,float enemyDepth,float bossDepth)
    {
        StageManager stage=Object.FindFirstObjectByType<StageManager>();if(stage==null)throw new MissingComponentException("StageManager missing in "+scene.path);
        var serialized=new SerializedObject(stage);serialized.FindProperty("stageMode").enumValueIndex=(int)mode;serialized.FindProperty("nextSceneName").stringValue=nextScene;
        serialized.FindProperty("exitX").floatValue=exitX;serialized.FindProperty("portalTriggerX").floatValue=portalX;serialized.FindProperty("playerSpawnX").floatValue=spawnX;serialized.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(stage);
        serialized.Update();serialized.FindProperty("playerSpawnDepth").floatValue=spawnDepth;serialized.FindProperty("depthMin").floatValue=minDepth;serialized.FindProperty("depthMax").floatValue=maxDepth;
        serialized.FindProperty("enemySpawnDepth").floatValue=enemyDepth;serialized.FindProperty("bossSpawnDepth").floatValue=bossDepth;serialized.FindProperty("arenaCenterX").floatValue=4.2f;serialized.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(stage);
    }

    private static void ConfigureBackground(Sprite[] sprites,float firstX,float width)
    {
        StageVisualBuilder old=Object.FindFirstObjectByType<StageVisualBuilder>();if(old==null)throw new MissingComponentException("StageVisualBuilder missing");old.enabled=false;EditorUtility.SetDirty(old);
        FinalStageBackground flat=old.GetComponent<FinalStageBackground>();if(flat==null)flat=old.gameObject.AddComponent<FinalStageBackground>();var serialized=new SerializedObject(flat);
        SerializedProperty sections=serialized.FindProperty("sections");sections.arraySize=sprites.Length;for(int i=0;i<sprites.Length;i++)sections.GetArrayElementAtIndex(i).objectReferenceValue=sprites[i];
        serialized.FindProperty("firstCenterX").floatValue=firstX;serialized.FindProperty("sectionWorldWidth").floatValue=width;serialized.FindProperty("worldHeight").floatValue=10.2f;serialized.ApplyModifiedPropertiesWithoutUndo();flat.Rebuild();EditorUtility.SetDirty(flat);
    }

    private static void ConfigureCamera(float min,float max)
    {
        BeatEmUpCamera camera=Object.FindFirstObjectByType<BeatEmUpCamera>();if(camera==null)throw new MissingComponentException("BeatEmUpCamera missing");var serialized=new SerializedObject(camera);
        serialized.FindProperty("minX").floatValue=min;serialized.FindProperty("maxX").floatValue=max;serialized.FindProperty("cameraY").floatValue=0f;serialized.FindProperty("depthFollow").floatValue=0f;serialized.ApplyModifiedPropertiesWithoutUndo();camera.transform.position=new Vector3(min,0f,-10f);
        Camera unityCamera=camera.GetComponent<Camera>();unityCamera.orthographic=true;unityCamera.orthographicSize=5.1f;EditorUtility.SetDirty(unityCamera);EditorUtility.SetDirty(camera);
    }

    private static void ConfigureNavigation(Vector2[] points)
    {
        StageNavigationMap map=Object.FindFirstObjectByType<StageNavigationMap>();if(map==null)return;var serialized=new SerializedObject(map);SerializedProperty polygon=serialized.FindProperty("walkablePolygon");
        polygon.arraySize=points.Length;for(int i=0;i<points.Length;i++)polygon.GetArrayElementAtIndex(i).vector2Value=points[i];serialized.FindProperty("blockedZones").arraySize=0;serialized.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(map);
    }

    private static void CreatePortalPrompt(string name,float x,float y,string label)
    {
        RemoveObject(name);var go=new GameObject(name);go.transform.position=new Vector3(x,y,0f);var text=go.AddComponent<TextMesh>();
        text.text=label;text.anchor=TextAnchor.MiddleCenter;text.alignment=TextAlignment.Center;text.fontSize=48;text.characterSize=.028f;text.color=new Color(.35f,.85f,1f,.95f);go.GetComponent<MeshRenderer>().sortingOrder=1700;
    }
    private static void RemoveObject(string name){GameObject found=GameObject.Find(name);if(found!=null)Object.DestroyImmediate(found);}
    private static void EnsureFolder(string path){if(AssetDatabase.IsValidFolder(path))return;int slash=path.LastIndexOf('/');EnsureFolder(path.Substring(0,slash));AssetDatabase.CreateFolder(path.Substring(0,slash),path.Substring(slash+1));}
}
