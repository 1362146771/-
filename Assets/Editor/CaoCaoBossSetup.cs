using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ThreeKingdoms;
using ThreeKingdoms.EditorTools;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class CaoCaoBossSetup
{
    private const string FrameRoot="Assets/Art/Characters/CaoCao/Frames";
    private const string AnimationRoot="Assets/Animations/CaoCao";
    private const string DataRoot="Assets/Data/Combat/CaoCao";
    private const string PrefabPath="Assets/Prefabs/Characters/PF_CaoCaoBoss.prefab";
    private const string ScenePath="Assets/Scenes/SC_Stage01_AncientStreet.unity";
    private static readonly (string name,float fps,bool loop)[] Clips={
        ("Idle",10f,true),("Walk",10f,true),("IdleAction",12.5f,false),("Guard",12.5f,false),("Death",10f,false),
        ("Thrust",100f/11f,false),("DownSlash",10f,false),("DownSlashWave",100f/11f,false),("Charge",100f/6f,false),
        ("ChargedCharge",100f/6f,false),("Skill",10f,false),("Phase1Ultimate",100f/7f,false),("Phase2Ultimate",12.5f,false),
        ("Phase3Ultimate",100f/9f,false),("Phase1Stagger",10f,false),("Phase2Stagger",10f,false),("HitReactA",10f,false),
        ("HitReactB",10f,false),("Entrance",10f,false)};

    static CaoCaoBossSetup()
    {
        EditorApplication.update+=SetupWhenReady;
    }

    [MenuItem("Three Kingdoms/Boss/Rebuild Cao Cao Boss")]
    public static void Rebuild()
    {
        Setup(true);
    }

    [MenuItem("Three Kingdoms/Boss/Sync Cao Cao Battle Editor + Scale")]
    public static void SyncBattleEditorAndScale()
    {
        ApplyBossScale();
        BattleEditorMigration.MigrateExistingCombatData();
        AssetDatabase.SaveAssets();AssetDatabase.Refresh();
        Debug.Log("CAO_CAO_EDITOR_SYNC_OK actions=19 visualScale=1.70 library=Assets/Data/Combat/Actions/LIB_CaoCao.asset");
    }

    public static void RunBattleEditorScaleBatch(){SyncBattleEditorAndScale();}

    private static void SetupWhenReady()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)return;
        var existingBoss=AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if(existingBoss!=null)
        {
            EditorApplication.update-=SetupWhenReady;
            bool missingLibrary=AssetDatabase.LoadAssetAtPath<CharacterActionLibrary>("Assets/Data/Combat/Actions/LIB_CaoCao.asset")==null;
            var existingMotor=existingBoss.GetComponent<CharacterMotor>();
            if(missingLibrary||existingMotor==null||Mathf.Abs(existingMotor.VisualScale-1.70f)>.001f)SyncBattleEditorAndScale();
            return;
        }
        if(AssetDatabase.FindAssets("t:Texture2D",new[]{FrameRoot}).Length<379)return;
        EditorApplication.update-=SetupWhenReady;
        Setup(false);
    }

    private static void Setup(bool rebuild)
    {
        EnsureFolder("Assets/Animations");EnsureFolder(AnimationRoot);EnsureFolder("Assets/Data");EnsureFolder("Assets/Data/Combat");EnsureFolder(DataRoot);
        ConfigureFrames();
        var clipMap=new Dictionary<string,AnimationClip>();
        foreach(var spec in Clips)clipMap[spec.name]=CreateClip(spec.name,spec.fps,spec.loop,rebuild);
        AnimatorController controller=CreateController(clipMap,rebuild);
        var attacks=CreateAttacks();
        CreateBossPrefab(controller,clipMap["Idle"],attacks,rebuild);
        WireStage();AssetDatabase.SaveAssets();AssetDatabase.Refresh();
        Debug.Log("CAO_CAO_BOSS_SETUP_OK gifs=19_bound_00_to_18 frames=379 clips=19 attacks=9 phases=3 prefab="+PrefabPath);
    }

    private static void ConfigureFrames()
    {
        foreach(string guid in AssetDatabase.FindAssets("t:Texture2D",new[]{FrameRoot}))
        {
            string path=AssetDatabase.GUIDToAssetPath(guid);var importer=AssetImporter.GetAtPath(path) as TextureImporter;if(importer==null)continue;
            var textureSettings=new TextureImporterSettings();importer.ReadTextureSettings(textureSettings);
            bool changed=importer.textureType!=TextureImporterType.Sprite||importer.spriteImportMode!=SpriteImportMode.Single||importer.filterMode!=FilterMode.Point||importer.textureCompression!=TextureImporterCompression.Uncompressed||importer.mipmapEnabled||Mathf.Abs(importer.spritePixelsPerUnit-64f)>.01f||textureSettings.spriteAlignment!=(int)SpriteAlignment.Custom||textureSettings.spritePivot!=new Vector2(.5f,0f);
            if(!changed)continue;textureSettings.spriteAlignment=(int)SpriteAlignment.Custom;textureSettings.spritePivot=new Vector2(.5f,0f);importer.SetTextureSettings(textureSettings);importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;importer.filterMode=FilterMode.Point;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.mipmapEnabled=false;importer.alphaIsTransparency=true;importer.spritePixelsPerUnit=64f;importer.SaveAndReimport();
        }
    }

    private static AnimationClip CreateClip(string action,float fps,bool loop,bool rebuild)
    {
        string path=AnimationRoot+"/AN_CAO_"+action+".anim";if(rebuild&&AssetDatabase.LoadAssetAtPath<AnimationClip>(path)!=null)AssetDatabase.DeleteAsset(path);
        var existing=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);if(existing!=null)return existing;
        string prefix="CAO_"+action+"_";Sprite[] frames=AssetDatabase.FindAssets("t:Texture2D",new[]{FrameRoot}).Select(AssetDatabase.GUIDToAssetPath).Where(p=>Path.GetFileNameWithoutExtension(p).StartsWith(prefix,StringComparison.Ordinal)).Distinct().OrderBy(p=>p,StringComparer.Ordinal).Select(AssetDatabase.LoadAssetAtPath<Sprite>).Where(s=>s!=null).ToArray();
        if(frames.Length==0)throw new InvalidOperationException("No Cao Cao frames found for "+action);
        var clip=new AnimationClip{name="AN_CAO_"+action,frameRate=fps};int count=frames.Length+(loop?1:0);var keys=new ObjectReferenceKeyframe[count];
        for(int i=0;i<frames.Length;i++)keys[i]=new ObjectReferenceKeyframe{time=i/fps,value=frames[i]};if(loop)keys[count-1]=new ObjectReferenceKeyframe{time=frames.Length/fps,value=frames[0]};
        AnimationUtility.SetObjectReferenceCurve(clip,EditorCurveBinding.PPtrCurve("VisualRoot",typeof(SpriteRenderer),"m_Sprite"),keys);var so=new SerializedObject(clip);so.FindProperty("m_AnimationClipSettings.m_LoopTime").boolValue=loop;so.ApplyModifiedPropertiesWithoutUndo();AssetDatabase.CreateAsset(clip,path);return clip;
    }

    private static AnimatorController CreateController(Dictionary<string,AnimationClip> clips,bool rebuild)
    {
        string path=AnimationRoot+"/AC_CAO.controller";if(rebuild&&AssetDatabase.LoadAssetAtPath<AnimatorController>(path)!=null)AssetDatabase.DeleteAsset(path);
        var existing=AssetDatabase.LoadAssetAtPath<AnimatorController>(path);if(existing!=null)return existing;
        var controller=AnimatorController.CreateAnimatorControllerAtPath(path);var machine=controller.layers[0].stateMachine;AnimatorState idle=null;
        foreach(var pair in clips){var state=machine.AddState(pair.Key);state.motion=pair.Value;if(pair.Key=="Idle")idle=state;}machine.defaultState=idle;EditorUtility.SetDirty(controller);return controller;
    }

    private static Dictionary<string,AttackData> CreateAttacks()
    {
        var result=new Dictionary<string,AttackData>();
        result["thrust"]=Attack("Thrust",.22f,.18f,.15f,14f,2.7f,.70f,.65f,.45f,.45f,false);
        result["downSlash"]=Attack("DownSlash",.25f,.15f,.20f,18f,2.9f,.76f,.82f,.65f,.35f,false);
        result["downSlashWave"]=Attack("DownSlashWave",.22f,.22f,.22f,22f,4.4f,.88f,.90f,1.2f,0f,true);
        result["charge"]=Attack("Charge",.18f,.42f,.24f,20f,3.7f,.72f,1.05f,1.1f,2.2f,false);
        result["chargedCharge"]=Attack("ChargedCharge",.45f,.55f,.32f,28f,4.6f,.82f,1.30f,1.8f,3.0f,false);
        result["skill"]=Attack("Skill",.28f,.42f,.30f,26f,4.2f,.95f,1.0f,1.8f,0f,true);
        result["phase1Ultimate"]=Attack("Phase1Ultimate",.80f,1.60f,1.10f,38f,5.2f,1.15f,1.2f,6f,0f,true);
        result["phase2Ultimate"]=Attack("Phase2Ultimate",.70f,1.60f,1.14f,50f,5.8f,1.25f,1.5f,7f,0f,true);
        result["phase3Ultimate"]=Attack("Phase3Ultimate",1.0f,3.20f,1.38f,70f,7.0f,1.45f,1.9f,10f,0f,true);
        return result;
    }

    private static AttackData Attack(string action,float startup,float active,float recovery,float damage,float rangeX,float rangeDepth,float knockback,float cooldown,float move,bool lockGround)
    {
        string path=DataRoot+"/ATK_CAO_"+action+".asset";var data=AssetDatabase.LoadAssetAtPath<AttackData>(path);if(data==null){data=ScriptableObject.CreateInstance<AttackData>();AssetDatabase.CreateAsset(data,path);}
        data.actionId=action;data.animationState=action;data.priority=ActionPriority.Skill;data.startup=startup;data.active=active;data.recovery=recovery;data.damage=damage;data.rangeX=rangeX;data.rangeDepth=rangeDepth;data.knockbackX=knockback;data.knockbackDepth=.10f;data.hitStop=.045f;data.cooldown=cooldown;data.forwardMove=move;data.lockGroundPosition=lockGround;data.visualOffset=Vector2.zero;EditorUtility.SetDirty(data);return data;
    }

    private static void CreateBossPrefab(AnimatorController controller,AnimationClip idle,Dictionary<string,AttackData> attacks,bool rebuild)
    {
        if(rebuild&&AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath)!=null)AssetDatabase.DeleteAsset(PrefabPath);if(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath)!=null)return;
        const string soldierPath="Assets/Prefabs/Characters/PF_CommonSoldier.prefab";var root=PrefabUtility.LoadPrefabContents(soldierPath);
        try
        {
            root.name="PF_CaoCaoBoss";var soldier=root.GetComponent<SoldierAI>();if(soldier!=null)Object.DestroyImmediate(soldier);
            var animator=root.GetComponent<Animator>();animator.runtimeAnimatorController=controller;var renderer=root.transform.Find("VisualRoot").GetComponent<SpriteRenderer>();renderer.sprite=AnimationUtility.GetObjectReferenceCurve(idle,EditorCurveBinding.PPtrCurve("VisualRoot",typeof(SpriteRenderer),"m_Sprite"))[0].value as Sprite;
            var health=new SerializedObject(root.GetComponent<CharacterHealth>());health.FindProperty("maxHealth").floatValue=900f;health.ApplyModifiedPropertiesWithoutUndo();
            var motor=new SerializedObject(root.GetComponent<CharacterMotor>());motor.FindProperty("visualScale.character").floatValue=1.70f;motor.FindProperty("visualScale.shadow").vector2Value=new Vector2(1.85f,1.12f);motor.FindProperty("depthMax").floatValue=-.24f;motor.ApplyModifiedPropertiesWithoutUndo();
            var driver=new SerializedObject(root.GetComponent<CharacterAnimator>());driver.FindProperty("idleState").stringValue="Idle";driver.ApplyModifiedPropertiesWithoutUndo();
            var ai=root.AddComponent<CaoCaoBossAI>();var so=new SerializedObject(ai);foreach(var pair in attacks)so.FindProperty(pair.Key).objectReferenceValue=pair.Value;so.ApplyModifiedPropertiesWithoutUndo();PrefabUtility.SaveAsPrefabAsset(root,PrefabPath);
        }
        finally{PrefabUtility.UnloadPrefabContents(root);}
    }

    private static void ApplyBossScale()
    {
        if(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath)==null)throw new InvalidOperationException("Cao Cao prefab is missing: "+PrefabPath);
        var root=PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            var motor=new SerializedObject(root.GetComponent<CharacterMotor>());motor.FindProperty("visualScale.character").floatValue=1.70f;motor.FindProperty("visualScale.shadow").vector2Value=new Vector2(1.85f,1.12f);motor.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root,PrefabPath);
        }
        finally{PrefabUtility.UnloadPrefabContents(root);}
    }

    private static void WireStage()
    {
        var scene=EditorSceneManager.GetActiveScene();if(scene.path!=ScenePath)scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);var stage=Object.FindFirstObjectByType<StageManager>();if(stage==null)throw new MissingComponentException("StageManager missing from Stage01");
        var so=new SerializedObject(stage);so.FindProperty("bossPrefab").objectReferenceValue=AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);so.FindProperty("bossTriggerX").floatValue=36.5f;so.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(stage);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
    }

    private static void EnsureFolder(string path){if(AssetDatabase.IsValidFolder(path))return;int slash=path.LastIndexOf('/');EnsureFolder(path.Substring(0,slash));AssetDatabase.CreateFolder(path.Substring(0,slash),path.Substring(slash+1));}
}
