using System;
using System.Linq;
using ThreeKingdoms;
using ThreeKingdoms.EditorTools;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

[InitializeOnLoad]
public static class DiaochanJumpSetup
{
    private const string FrameRoot="Assets/Art/Characters/Diaochan/Frames";
    private const string ClipPath="Assets/Animations/Diaochan/AN_DIA_Jump.anim";
    private const string ControllerPath="Assets/Animations/Diaochan/AC_DIA.controller";
    private static readonly float[] SourceDurations={.02f,.02f,.02f,.02f,.02f,.02f,.02f,.02f,.02f,.02f,.02f,.02f,.02f,.02f,.02f,.02f,.02f,.02f,.02f,.02f,.12f,.02f,.02f,.02f,.02f,.02f,.02f,.02f,.02f,.02f,.02f,.02f,.02f,.02f,.02f,.02f,.02f,.04f,.12f,.02f};
    private const float RuntimeJumpDuration=.80f;

    static DiaochanJumpSetup(){EditorApplication.update+=SetupWhenReady;}

    [MenuItem("Three Kingdoms/Player/Sync Diaochan Jump")]
    public static void Sync()
    {
        ConfigureFrames();AnimationClip clip=CreateClip();AddAnimatorState(clip);BattleEditorMigration.MigrateExistingCombatData();AssetDatabase.SaveAssets();AssetDatabase.Refresh();
        Debug.Log("DIAOCHAN_JUMP_OK sourceFrames=40 sourceDuration=1.02 runtimeDuration=0.80 state=Jump jumpAttackPreserved=true");
    }

    private static void SetupWhenReady()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)return;
        if(AssetDatabase.FindAssets("DIA_Jump_ t:Texture2D",new[]{FrameRoot}).Length<40)return;
        EditorApplication.update-=SetupWhenReady;if(AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath)==null)Sync();
    }

    private static void ConfigureFrames()
    {
        foreach(string guid in AssetDatabase.FindAssets("DIA_Jump_ t:Texture2D",new[]{FrameRoot}))
        {
            string path=AssetDatabase.GUIDToAssetPath(guid);var importer=AssetImporter.GetAtPath(path) as TextureImporter;if(importer==null)continue;var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);settings.spriteAlignment=(int)SpriteAlignment.Custom;settings.spritePivot=new Vector2(.5f,0f);importer.SetTextureSettings(settings);
            importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;importer.spritePixelsPerUnit=64f;importer.filterMode=FilterMode.Point;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.mipmapEnabled=false;importer.alphaIsTransparency=true;importer.SaveAndReimport();
        }
    }

    private static AnimationClip CreateClip()
    {
        AnimationClip existing=AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath);if(existing!=null)return existing;
        Sprite[] sprites=AssetDatabase.FindAssets("DIA_Jump_ t:Texture2D",new[]{FrameRoot}).Select(AssetDatabase.GUIDToAssetPath).OrderBy(p=>p,StringComparer.Ordinal).Select(AssetDatabase.LoadAssetAtPath<Sprite>).Where(s=>s!=null).ToArray();if(sprites.Length!=40)throw new InvalidOperationException("Expected 40 Diaochan Jump sprites, found "+sprites.Length);
        var clip=new AnimationClip{name="AN_DIA_Jump",frameRate=50f};var keys=new ObjectReferenceKeyframe[41];float sourceTotal=SourceDurations.Sum(),scale=RuntimeJumpDuration/sourceTotal,time=0f;
        for(int i=0;i<sprites.Length;i++){keys[i]=new ObjectReferenceKeyframe{time=time,value=sprites[i]};time+=SourceDurations[i]*scale;}keys[40]=new ObjectReferenceKeyframe{time=RuntimeJumpDuration,value=sprites[39]};
        AnimationUtility.SetObjectReferenceCurve(clip,EditorCurveBinding.PPtrCurve("VisualRoot",typeof(SpriteRenderer),"m_Sprite"),keys);AssetDatabase.CreateAsset(clip,ClipPath);return clip;
    }

    private static void AddAnimatorState(AnimationClip clip)
    {
        AnimatorController controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);if(controller==null)throw new InvalidOperationException("Diaochan controller missing: "+ControllerPath);var machine=controller.layers[0].stateMachine;var state=machine.states.Select(s=>s.state).FirstOrDefault(s=>s.name=="Jump");if(state==null)state=machine.AddState("Jump");state.motion=clip;EditorUtility.SetDirty(controller);
    }
}
