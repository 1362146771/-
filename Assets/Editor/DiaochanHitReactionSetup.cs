using System;
using System.Linq;
using ThreeKingdoms;
using ThreeKingdoms.EditorTools;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

[InitializeOnLoad]
public static class DiaochanHitReactionSetup
{
    private const string FrameRoot="Assets/Art/Characters/Diaochan/Frames";
    private const string ClipPath="Assets/Animations/Diaochan/AN_DIA_HitReact.anim";
    private const string ControllerPath="Assets/Animations/Diaochan/AC_DIA.controller";
    private const string PrefabPath="Assets/Prefabs/Characters/PF_Diaochan.prefab";
    private static readonly float[] Durations={.04f,.08f,.02f,.02f,.08f,.04f,.06f,.02f,.04f,.04f,.04f,.04f,.02f};

    static DiaochanHitReactionSetup(){EditorApplication.update+=SetupWhenReady;}

    [MenuItem("Three Kingdoms/Player/Sync Diaochan Hit Reaction")]
    public static void Sync()
    {
        ConfigureFrames();AnimationClip clip=CreateClip();AddAnimatorState(clip);WirePrefab();BattleEditorMigration.MigrateExistingCombatData();AssetDatabase.SaveAssets();AssetDatabase.Refresh();
        Debug.Log("DIAOCHAN_HIT_REACTION_OK sourceFrames=13 duration=0.54 state=HitReact battleEditor=true");
    }

    private static void ConfigureFrames()
    {
        foreach(string guid in AssetDatabase.FindAssets("DIA_HitReact_ t:Texture2D",new[]{FrameRoot}))
        {
            string path=AssetDatabase.GUIDToAssetPath(guid);var importer=AssetImporter.GetAtPath(path) as TextureImporter;if(importer==null)continue;
            var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);settings.spriteAlignment=(int)SpriteAlignment.Custom;settings.spritePivot=new Vector2(.5f,0f);importer.SetTextureSettings(settings);
            importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;importer.spritePixelsPerUnit=64f;importer.filterMode=FilterMode.Point;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.mipmapEnabled=false;importer.alphaIsTransparency=true;importer.SaveAndReimport();
        }
    }

    private static void SetupWhenReady()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)return;
        if(AssetDatabase.FindAssets("DIA_HitReact_ t:Texture2D",new[]{FrameRoot}).Length<13)return;
        EditorApplication.update-=SetupWhenReady;
        GameObject prefab=AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if(AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath)==null||prefab==null||prefab.GetComponent<DiaochanHitReaction>()==null)Sync();
    }

    private static AnimationClip CreateClip()
    {
        AnimationClip existing=AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath);if(existing!=null)return existing;
        Sprite[] sprites=AssetDatabase.FindAssets("DIA_HitReact_ t:Texture2D",new[]{FrameRoot}).Select(AssetDatabase.GUIDToAssetPath).OrderBy(p=>p,StringComparer.Ordinal).Select(AssetDatabase.LoadAssetAtPath<Sprite>).Where(s=>s!=null).ToArray();
        if(sprites.Length!=13)throw new InvalidOperationException("Expected 13 Diaochan HitReact sprites, found "+sprites.Length);
        var clip=new AnimationClip{name="AN_DIA_HitReact",frameRate=50f};var keys=new ObjectReferenceKeyframe[14];float time=0f;
        for(int i=0;i<sprites.Length;i++){keys[i]=new ObjectReferenceKeyframe{time=time,value=sprites[i]};time+=Durations[i];}
        // Duplicate the final sprite at the exact GIF end time so Unity preserves its last 20 ms hold.
        keys[13]=new ObjectReferenceKeyframe{time=time,value=sprites[12]};AnimationUtility.SetObjectReferenceCurve(clip,EditorCurveBinding.PPtrCurve("VisualRoot",typeof(SpriteRenderer),"m_Sprite"),keys);AssetDatabase.CreateAsset(clip,ClipPath);return clip;
    }

    private static void AddAnimatorState(AnimationClip clip)
    {
        AnimatorController controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);if(controller==null)throw new InvalidOperationException("Diaochan controller missing: "+ControllerPath);
        var machine=controller.layers[0].stateMachine;var state=machine.states.Select(s=>s.state).FirstOrDefault(s=>s.name=="HitReact");if(state==null)state=machine.AddState("HitReact");state.motion=clip;EditorUtility.SetDirty(controller);
    }

    private static void WirePrefab()
    {
        GameObject root=PrefabUtility.LoadPrefabContents(PrefabPath);try{if(root.GetComponent<DiaochanHitReaction>()==null)root.AddComponent<DiaochanHitReaction>();PrefabUtility.SaveAsPrefabAsset(root,PrefabPath);}finally{PrefabUtility.UnloadPrefabContents(root);}
    }
}
