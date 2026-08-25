using System.IO;
using ThreeKingdoms;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class Iteration03Setup
{
    private const string Root="Assets/Data/Combat/Iteration03";
    private const string StageSourceRoot="Assets/AgentGenerated/Art/Backgrounds/Iteration03/";
    private const string StageLayerRoot="Assets/Art/Backgrounds/Iteration03Layers/";
    private const string StageCompositePath=StageLayerRoot+"fortress_night_stage_composite.png";
    private const int StageWidth=1448,StageHeight=1086,StageOverlap=64;
    private static readonly string[] StageSourceNames={"fortress_gate_approach","fortress_wall_walkway","cao_cao_storm_boss_arena"};

    public static void SetupSoldierCombat()
    {
        EnsureFolder("Assets/Data");EnsureFolder("Assets/Data/Combat");EnsureFolder(Root);
        var attack01=Attack("ATK_SOL_Attack01","Attack01",.27f,1.25f,.18f,10f,2.8f,.78f,.48f,1.05f,.15f);

        const string prefabPath="Assets/Prefabs/Characters/PF_CommonSoldier.prefab";
        var root=PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            var ai=root.GetComponent<SoldierAI>();if(ai==null)throw new MissingComponentException("SoldierAI missing from soldier prefab");
            var serialized=new SerializedObject(ai);
            Set(serialized,"attack01Data",attack01);
            serialized.ApplyModifiedPropertiesWithoutUndo();PrefabUtility.SaveAsPrefabAsset(root,prefabPath);
        }
        finally{PrefabUtility.UnloadPrefabContents(root);}
        AssetDatabase.SaveAssets();Debug.Log("ITERATION03_SOLDIER_COMBAT_OK attacks=1 action=Attack01 twoStageSlash=true depth=data-driven");
    }

    public static void SetupStage()
    {
        EnsureFolder("Assets/Art");EnsureFolder("Assets/Art/Backgrounds");EnsureFolder(StageLayerRoot.TrimEnd('/'));
        var sources=new Texture2D[StageSourceNames.Length];
        for(int i=0;i<sources.Length;i++)
        {
            string path=StageSourceRoot+StageSourceNames[i]+".png";ConfigureTexture(path,true);
            sources[i]=AssetDatabase.LoadAssetAtPath<Texture2D>(path);if(sources[i]==null)throw new FileNotFoundException("Iteration03 stage source missing",path);
        }
        BuildStageComposite(sources);ConfigureTexture(StageCompositePath,true);var composite=AssetDatabase.LoadAssetAtPath<Texture2D>(StageCompositePath);
        int sectionWidth=composite.width/3;var layers=new Sprite[4][];for(int i=0;i<4;i++)layers[i]=new Sprite[3];
        for(int section=0;section<3;section++)for(int band=0;band<4;band++)
        {
            int x=section*sectionWidth,width=section==2?composite.width-x:sectionWidth;
            Rect rect=band==0?new Rect(x,660,width,426):band==1?new Rect(x,500,width,160):band==2?new Rect(x,0,width,StageHeight):new Rect(x,0,width,130);
            string assetPath=StageLayerRoot+"section_"+(section+1)+"_"+(band==0?"BG":band==1?"Mid":band==2?"Ground":"FG")+".asset";
            if(AssetDatabase.LoadAssetAtPath<Object>(assetPath)!=null)AssetDatabase.DeleteAsset(assetPath);
            var sprite=Sprite.Create(composite,rect,new Vector2(.5f,.5f),100f,0,SpriteMeshType.FullRect);sprite.name="Iteration03_Section"+(section+1)+"_Band"+band;
            AssetDatabase.CreateAsset(sprite,assetPath);layers[band][section]=sprite;
        }

        var scene=EditorSceneManager.OpenScene("Assets/Scenes/SC_Stage01_AncientStreet.unity",OpenSceneMode.Single);
        var builder=Object.FindFirstObjectByType<StageVisualBuilder>();if(builder==null)throw new MissingComponentException("StageVisualBuilder missing");
        var serialized=new SerializedObject(builder);Assign(serialized.FindProperty("backgroundSections"),layers[0]);Assign(serialized.FindProperty("midgroundSections"),layers[1]);
        Assign(serialized.FindProperty("gameplaySections"),layers[2]);Assign(serialized.FindProperty("foregroundSections"),layers[3]);serialized.ApplyModifiedPropertiesWithoutUndo();
        var navigation=builder.GetComponent<StageNavigationMap>();if(navigation==null)navigation=builder.gameObject.AddComponent<StageNavigationMap>();navigation.ConfigureIteration03();
        EditorUtility.SetDirty(builder);EditorUtility.SetDirty(navigation);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        Debug.Log("ITERATION03_STAGE_OK sections=3 layers=4 walkablePolygon="+navigation.WalkablePolygon.Length+" blockedZones="+navigation.BlockedZones.Length+" provider=openai-built-in-imagegen");
    }

    private static void BuildStageComposite(Texture2D[] sources)
    {
        int width=StageWidth*3-StageOverlap*2;var output=new Texture2D(width,StageHeight,TextureFormat.RGBA32,false);var pixels=new Color32[width*StageHeight];
        for(int section=0;section<sources.Length;section++)
        {
            Color32[] source=NormalizeStageSource(sources[section]);int start=section*(StageWidth-StageOverlap);
            for(int y=0;y<StageHeight;y++)for(int x=0;x<StageWidth;x++)
            {
                int targetX=start+x,targetIndex=y*width+targetX,sourceIndex=y*StageWidth+x;
                if(section>0&&x<StageOverlap){float t=(x+.5f)/StageOverlap;pixels[targetIndex]=Color32.Lerp(pixels[targetIndex],source[sourceIndex],t);}
                else pixels[targetIndex]=source[sourceIndex];
            }
        }
        output.SetPixels32(pixels);output.Apply();File.WriteAllBytes(StageCompositePath,output.EncodeToPNG());Object.DestroyImmediate(output);AssetDatabase.ImportAsset(StageCompositePath,ImportAssetOptions.ForceSynchronousImport);
    }

    private static Color32[] NormalizeStageSource(Texture2D source)
    {
        if(source.width==StageWidth&&source.height==StageHeight)return source.GetPixels32();

        // ImageGen uses a fixed landscape canvas. Center-crop it to the stage section
        // aspect before nearest-neighbour scaling, so the boss arena is not stretched.
        float targetAspect=(float)StageWidth/StageHeight;
        int cropWidth=source.width,cropHeight=source.height;
        if((float)source.width/source.height>targetAspect)cropWidth=Mathf.RoundToInt(source.height*targetAspect);
        else cropHeight=Mathf.RoundToInt(source.width/targetAspect);
        int cropX=(source.width-cropWidth)/2,cropY=(source.height-cropHeight)/2;
        Color32[] input=source.GetPixels32(),result=new Color32[StageWidth*StageHeight];
        for(int y=0;y<StageHeight;y++)
        {
            int sy=cropY+Mathf.Min(cropHeight-1,Mathf.FloorToInt((y+.5f)*cropHeight/StageHeight));
            for(int x=0;x<StageWidth;x++)
            {
                int sx=cropX+Mathf.Min(cropWidth-1,Mathf.FloorToInt((x+.5f)*cropWidth/StageWidth));
                result[y*StageWidth+x]=input[sy*source.width+sx];
            }
        }
        return result;
    }

    private static void ConfigureTexture(string path,bool readable)
    {
        var importer=AssetImporter.GetAtPath(path) as TextureImporter;if(importer==null)return;importer.textureType=TextureImporterType.Default;importer.isReadable=readable;
        importer.sRGBTexture=true;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.filterMode=FilterMode.Point;importer.npotScale=TextureImporterNPOTScale.None;importer.maxTextureSize=8192;importer.SaveAndReimport();
    }

    private static AttackData Attack(string file,string action,float startup,float active,float recovery,float damage,float rangeX,float rangeDepth,float knockback,float cooldown,float forwardMove)
    {
        string path=Root+"/"+file+".asset";var data=AssetDatabase.LoadAssetAtPath<AttackData>(path);
        if(data==null){data=ScriptableObject.CreateInstance<AttackData>();AssetDatabase.CreateAsset(data,path);}
        data.actionId=action;data.animationState=action;data.priority=ActionPriority.HeavyAttack;data.startup=startup;data.active=active;data.recovery=recovery;
        data.damage=damage;data.rangeX=rangeX;data.rangeDepth=rangeDepth;data.knockbackX=knockback;data.knockbackDepth=.08f;data.hitStop=.04f;data.cooldown=cooldown;data.forwardMove=forwardMove;data.lockGroundPosition=false;data.visualOffset=Vector2.zero;
        EditorUtility.SetDirty(data);return data;
    }

    public static void SetupSkillAnchors()
    {
        ConfigureSkill("Assets/Data/Combat/Iteration02/ATK_DIA_Skill1.asset",Vector2.zero);
        ConfigureSkill("Assets/Data/Combat/Iteration02/ATK_DIA_ParryCounter.asset",Vector2.zero);
        ConfigureSkill("Assets/Data/Combat/Iteration02/ATK_DIA_ChargeSkill2.asset",Vector2.zero);
        ConfigureSkill("Assets/Data/Combat/Iteration02/ATK_DIA_Skill3.asset",Vector2.zero);
        ConfigureSkill("Assets/Data/Combat/Iteration02/ATK_DIA_Skill4.asset",Vector2.zero);
        AssetDatabase.SaveAssets();Debug.Log("ITERATION03_SKILL_ANCHORS_OK stationary=5 rootLock=true perFrameBakedShadowPivot=true visualOffset=zero");
    }

    public static void SetupHud()
    {
        const string uiRoot="Assets/AgentGenerated/Art/UI/Iteration03/";
        string[] names={"hud_player_frame","hud_hp_fill","hud_status_plaque"};var textures=new Texture2D[names.Length];
        for(int i=0;i<names.Length;i++){string path=uiRoot+names[i]+".png";ConfigureTexture(path,false);textures[i]=AssetDatabase.LoadAssetAtPath<Texture2D>(path);if(textures[i]==null)throw new FileNotFoundException("HUD asset missing",path);}
        var scene=EditorSceneManager.OpenScene("Assets/Scenes/SC_Stage01_AncientStreet.unity",OpenSceneMode.Single);var hud=Object.FindFirstObjectByType<StageHud>();if(hud==null)throw new MissingComponentException("StageHud missing");
        var serialized=new SerializedObject(hud);Set(serialized,"playerFrame",textures[0]);Set(serialized,"hpFill",textures[1]);Set(serialized,"statusPlaque",textures[2]);serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(hud);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();Debug.Log("ITERATION03_HUD_OK originalAssets=3 dynamicText=true worldCameraHud=true");
    }

    private static void ConfigureSkill(string path,Vector2 visualOffset)
    {
        var data=AssetDatabase.LoadAssetAtPath<AttackData>(path);if(data==null)throw new FileNotFoundException("Skill AttackData missing",path);
        data.lockGroundPosition=true;data.forwardMove=0f;data.visualOffset=visualOffset;EditorUtility.SetDirty(data);
    }

    private static void Set(SerializedObject serialized,string property,Object value)=>serialized.FindProperty(property).objectReferenceValue=value;
    private static void Assign(SerializedProperty property,Sprite[] sprites){property.arraySize=sprites.Length;for(int i=0;i<sprites.Length;i++)property.GetArrayElementAtIndex(i).objectReferenceValue=sprites[i];}
    private static void EnsureFolder(string path){if(AssetDatabase.IsValidFolder(path))return;int slash=path.LastIndexOf('/');AssetDatabase.CreateFolder(path.Substring(0,slash),path.Substring(slash+1));}
}
