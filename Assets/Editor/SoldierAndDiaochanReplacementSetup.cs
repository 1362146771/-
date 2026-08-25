using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ThreeKingdoms;
using ThreeKingdoms.EditorTools;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

[InitializeOnLoad]
public static class SoldierAndDiaochanReplacementSetup
{
    private const string SoldierFrameRoot="Assets/Art/Characters/SoldierReplacement";
    private const string DiaDeathRoot="Assets/Art/Characters/Diaochan/DeathReplacementFrames";
    private const string SoldierAnimationRoot="Assets/Animations/Soldier/";
    private const string SoldierControllerPath=SoldierAnimationRoot+"AC_SOL.controller";
    private const string DiaControllerPath="Assets/Animations/Diaochan/AC_DIA.controller";
    private const string SoldierPrefabPath="Assets/Prefabs/Characters/PF_CommonSoldier.prefab";
    private static readonly float[] AttackDurations={.04f,.12f,.04f,.08f,.18f,.08f,.10f,.12f,.10f,.02f,.06f,.06f,.06f,.06f,.12f,.12f,.12f,.04f,.08f};
    private static readonly int[] HitBloodFrameSelection={0,1,5,10,11,14,18,22,25};
    private static readonly float[] HitBloodDurations={.02f,.04f,.04f,.04f,.04f,.04f,.04f,.04f,.04f};
    private static readonly Vector2 AttackCanvasPivot=new Vector2(147f/252f,38f/151f);

    static SoldierAndDiaochanReplacementSetup(){EditorApplication.update+=SetupWhenReady;}

    [MenuItem("Three Kingdoms/Characters/Import Replacement Soldier And Diaochan Death")]
    public static void Sync()
    {
        ConfigureAttackFrames();ConfigureFrames("WalkFrames",true);ConfigureHurtDeathFrames();ConfigureFrames("HitReactBloodFrames",true);ConfigureDiaDeathFrames();
        Sprite[] attack=LoadSprites(SoldierFrameRoot+"/AttackFrames","NSOL_Attack_",48);
        Sprite[] walk=LoadSprites(SoldierFrameRoot+"/WalkFrames","NSOL_Walk_",83);
        Sprite[] hurtDeath=LoadSprites(SoldierFrameRoot+"/HurtDeathFrames","NSOL_HurtDeath_",82);
        Sprite[] hitBlood=LoadSprites(SoldierFrameRoot+"/HitReactBloodFrames","NSOL_HitBlood_",28);
        Sprite[] diaDeath=LoadSprites(DiaDeathRoot,"DIA_DeathNew_",50);

        AnimationClip idle=WriteClip(SoldierAnimationRoot+"AN_SOL_Idle.anim",new[]{walk[11]},new[]{.5f},true);
        AnimationClip idleLong=WriteClip(SoldierAnimationRoot+"AN_SOL_IdleLong.anim",new[]{walk[11]},new[]{.5f},true);
        AnimationClip walkClip=WriteClip(SoldierAnimationRoot+"AN_SOL_Walk.anim",walk,Enumerable.Range(0,83).Select(i=>i>=81?.06f:.02f).ToArray(),true);
        AnimationClip attack01=WriteClip(SoldierAnimationRoot+"AN_SOL_Attack01.anim",Slice(attack,0,19),AttackDurations,false);
        AnimationClip hitReact=WriteHitReactClip(SelectFrames(hitBlood,HitBloodFrameSelection));
        AnimationClip death=WriteClip(SoldierAnimationRoot+"AN_SOL_Death.anim",Slice(hurtDeath,34,45),Enumerable.Repeat(.02f,45).ToArray(),false);
        AnimationClip newDiaDeath=WriteClip("Assets/Animations/Diaochan/AN_DIA_Death.anim",diaDeath,Enumerable.Range(0,50).Select(i=>i==49?.48f:.02f).ToArray(),false);

        RemoveObsoleteSoldierAttacks();
        SetStates(SoldierControllerPath,new Dictionary<string,AnimationClip>{{"Idle",idle},{"IdleLong",idleLong},{"Walk",walkClip},{"Attack01",attack01},{"HitReact",hitReact},{"Death",death}});
        SetStates(DiaControllerPath,new Dictionary<string,AnimationClip>{{"Death",newDiaDeath}});
        BattleEditorMigration.MigrateExistingCombatData();
        ConfigureSoldierActions(attack01,hitReact,death,idle,idleLong,walkClip);
        ConfigureDiaDeathAction(newDiaDeath);
        WireSingleAttackPrefab();CompactSoldierLibrary();WireSoldierPrefab();
        AssetDatabase.SaveAssets();AssetDatabase.Refresh();
        Debug.Log("NPC_REPLACEMENT_OK soldierWalk=83 onlyAttack=Attack01 twoStageSlashFrames=19 trimmedWalkFrames=19-47 hurtWithBlood=9 duration=.34 deathNoBlood=45 diaDeath=50 sourceGifsUntouched=true");
    }

    [MenuItem("Three Kingdoms/Characters/Apply Soldier Single Two Stage Slash")]
    public static void ApplySingleTwoStageSlash()
    {
        ConfigureAttackFrames();
        Sprite[] attack=LoadSprites(SoldierFrameRoot+"/AttackFrames","NSOL_Attack_",48);
        AnimationClip clip=WriteClip(SoldierAnimationRoot+"AN_SOL_Attack01.anim",Slice(attack,0,19),AttackDurations,false);
        RemoveObsoleteSoldierAttacks();SetStates(SoldierControllerPath,new Dictionary<string,AnimationClip>{{"Attack01",clip}});
        ConfigureSingleAttackAction(clip);WireSingleAttackPrefab();CompactSoldierLibrary();
        AssetDatabase.SaveAssets();AssetDatabase.Refresh();
        Debug.Log("NPC_SINGLE_ATTACK_OK action=Attack01 sourceFrames=0-18 removedWalkFrames=19-47 removed=Attack02,RunAttack,JumpAttack twoDamagePhases=true");
    }

    [MenuItem("Three Kingdoms/Characters/Apply Soldier Blood HitReact")]
    public static void ApplyBloodHitReactReplacement()
    {
        ConfigureFrames("HitReactBloodFrames",true);
        Sprite[] sprites=LoadSprites(SoldierFrameRoot+"/HitReactBloodFrames","NSOL_HitBlood_",28);
        AnimationClip clip=WriteHitReactClip(SelectFrames(sprites,HitBloodFrameSelection));
        SetStates(SoldierControllerPath,new Dictionary<string,AnimationClip>{{"HitReact",clip}});
        ConfigureAction("Assets/Data/Combat/Actions/Soldier/SOL_HitReact.asset",clip,ActionCategory.Reaction,false,0,0,HitBloodFrameSelection.Length-1,null);
        SetSoldierHurtDuration(HitBloodDurations.Sum());
        AssetDatabase.SaveAssets();AssetDatabase.Refresh();
        Debug.Log("NPC_BLOOD_HIT_REACT_OK frames=9 duration=.34 sourceFrames=0,1,5,10,11,14,18,22,25 rootKnockback=false deathAnimationUnchanged=true");
    }

    [MenuItem("Three Kingdoms/Characters/Fix Replacement Soldier Facing And Hurt Motion")]
    public static void ApplyFacingAndHurtFix()
    {
        GameObject root=PrefabUtility.LoadPrefabContents(SoldierPrefabPath);try
        {
            CharacterAnimator animation=root.GetComponent<CharacterAnimator>();if(animation==null)throw new MissingComponentException("CharacterAnimator missing from soldier prefab");
            if(root.GetComponent<SoldierReactionController>()==null)root.AddComponent<SoldierReactionController>();
            EditorUtility.SetDirty(animation);PrefabUtility.SaveAsPrefabAsset(root,SoldierPrefabPath);
        }
        finally{PrefabUtility.UnloadPrefabContents(root);}
        AssetDatabase.SaveAssets();Debug.Log("NPC_FACING_HURT_FIX_OK sourcePrefix=NSOL_leftFacing suppressRootKnockback=true");
    }

    [MenuItem("Three Kingdoms/Characters/Fix Replacement Soldier HitReact Anchors")]
    public static void ApplyHitReactAnchorFix()
    {
        ConfigureHurtDeathFrames();AssetDatabase.SaveAssets();AssetDatabase.Refresh();
        Debug.Log("NPC_HIT_REACT_ANCHOR_FIX_OK anchoredFrames=0-14 deathCanvasFrames=34-78");
    }

    private static void SetupWhenReady()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)return;
        if(Count("AttackFrames")<48||Count("WalkFrames")<83||Count("HurtDeathFrames")<82||Count("HitReactBloodFrames")<28||AssetDatabase.FindAssets("DIA_DeathNew_ t:Texture2D",new[]{DiaDeathRoot}).Length<50)return;
        EditorApplication.update-=SetupWhenReady;
        // AN_SOL_HitReact is the one-time import marker. Reopening Unity must never overwrite later Battle Editor authoring.
        AnimationClip hitReact=AssetDatabase.LoadAssetAtPath<AnimationClip>(SoldierAnimationRoot+"AN_SOL_HitReact.anim");
        if(hitReact==null)Sync();
        else
        {
            ObjectReferenceKeyframe[] keys=AnimationUtility.GetObjectReferenceCurve(hitReact,EditorCurveBinding.PPtrCurve("VisualRoot",typeof(SpriteRenderer),"m_Sprite"));
            if(keys.Length==0||!AssetDatabase.GetAssetPath(keys[0].value).Contains("HitReactBloodFrames"))ApplyBloodHitReactReplacement();
        }
    }

    private static int Count(string folder)=>AssetDatabase.FindAssets("t:Texture2D",new[]{SoldierFrameRoot+"/"+folder}).Length;

    private static void ConfigureFrames(string folder,bool anchorFeet)
    {
        foreach(string guid in AssetDatabase.FindAssets("t:Texture2D",new[]{SoldierFrameRoot+"/"+folder}))ConfigureFrame(AssetDatabase.GUIDToAssetPath(guid),anchorFeet);
    }

    private static void ConfigureAttackFrames()
    {
        foreach(string guid in AssetDatabase.FindAssets("NSOL_Attack_ t:Texture2D",new[]{SoldierFrameRoot+"/AttackFrames"}))
        {
            // All frames share the source GIF's 253x152 canvas. A single pivot preserves
            // its authored continuity; per-frame foot detection can jump between feet,
            // sword outlines and slash effects and visually teleport the whole Sprite.
            ConfigureFrame(AssetDatabase.GUIDToAssetPath(guid),AttackCanvasPivot);
        }
    }

    private static void ConfigureHurtDeathFrames()
    {
        foreach(string guid in AssetDatabase.FindAssets("NSOL_HurtDeath_ t:Texture2D",new[]{SoldierFrameRoot+"/HurtDeathFrames"}))
        {
            string path=AssetDatabase.GUIDToAssetPath(guid),name=Path.GetFileNameWithoutExtension(path);int split=name.LastIndexOf('_');
            bool isHitReact=split>=0&&int.TryParse(name.Substring(split+1),out int frame)&&frame<=14;
            ConfigureFrame(path,isHitReact);
        }
    }

    private static void ConfigureDiaDeathFrames()
    {
        foreach(string guid in AssetDatabase.FindAssets("DIA_DeathNew_ t:Texture2D",new[]{DiaDeathRoot}))ConfigureFrame(AssetDatabase.GUIDToAssetPath(guid),false);
    }

    private static void ConfigureFrame(string path,bool anchorFeet)
        =>ConfigureFrame(path,anchorFeet?DetectFootPivot(path):new Vector2(.5f,0f));

    private static void ConfigureFrame(string path,Vector2 pivot)
    {
        if(!(AssetImporter.GetAtPath(path) is TextureImporter importer))return;
        var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);settings.spriteAlignment=(int)SpriteAlignment.Custom;settings.spritePivot=pivot;importer.SetTextureSettings(settings);
        importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;importer.spritePixelsPerUnit=64f;importer.filterMode=FilterMode.Point;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.mipmapEnabled=false;importer.alphaIsTransparency=true;importer.SaveAndReimport();
    }

    private static Vector2 DetectFootPivot(string assetPath)
    {
        byte[] bytes=File.ReadAllBytes(Path.GetFullPath(assetPath));var texture=new Texture2D(2,2,TextureFormat.RGBA32,false);
        if(!ImageConversion.LoadImage(texture,bytes,false)){UnityEngine.Object.DestroyImmediate(texture);return new Vector2(.5f,0f);}
        Color32[] pixels=texture.GetPixels32();int width=texture.width,height=texture.height,minY=height;
        for(int y=0;y<height;y++)for(int x=0;x<width;x++)if(pixels[y*width+x].a>32){minY=Mathf.Min(minY,y);}
        if(minY==height){UnityEngine.Object.DestroyImmediate(texture);return new Vector2(.5f,0f);}
        var xs=new List<int>();int maxBand=Mathf.Min(height-1,minY+5);
        for(int y=minY;y<=maxBand;y++)for(int x=0;x<width;x++)if(pixels[y*width+x].a>96)xs.Add(x);
        xs.Sort();float footX=xs.Count==0?width*.5f:xs[xs.Count/2];UnityEngine.Object.DestroyImmediate(texture);
        return new Vector2(footX/Mathf.Max(1,width-1),(float)minY/Mathf.Max(1,height-1));
    }

    private static Sprite[] LoadSprites(string folder,string prefix,int expected)
    {
        Sprite[] result=AssetDatabase.FindAssets(prefix+" t:Sprite",new[]{folder}).Select(AssetDatabase.GUIDToAssetPath).Distinct().OrderBy(p=>p,StringComparer.Ordinal).Select(AssetDatabase.LoadAssetAtPath<Sprite>).Where(s=>s!=null).ToArray();
        if(result.Length!=expected)throw new InvalidOperationException($"Expected {expected} sprites in {folder}, found {result.Length}");return result;
    }

    private static Sprite[] Slice(Sprite[] source,int start,int count){var result=new Sprite[count];Array.Copy(source,start,result,0,count);return result;}

    private static Sprite[] SelectFrames(Sprite[] source,int[] indices)=>indices.Select(index=>source[index]).ToArray();

    private static AnimationClip WriteClip(string path,Sprite[] sprites,float[] durations,bool loop)
    {
        if(sprites.Length!=durations.Length)throw new ArgumentException("Sprite/duration mismatch: "+path);
        AnimationClip clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);if(clip==null){clip=new AnimationClip{name=Path.GetFileNameWithoutExtension(path)};AssetDatabase.CreateAsset(clip,path);}
        float total=durations.Sum();clip.frameRate=sprites.Length/Mathf.Max(.01f,total);var keys=new ObjectReferenceKeyframe[sprites.Length+1];float time=0f;
        for(int i=0;i<sprites.Length;i++){keys[i]=new ObjectReferenceKeyframe{time=time,value=sprites[i]};time+=durations[i];}keys[sprites.Length]=new ObjectReferenceKeyframe{time=time,value=sprites[sprites.Length-1]};
        AnimationUtility.SetObjectReferenceCurve(clip,EditorCurveBinding.PPtrCurve("VisualRoot",typeof(SpriteRenderer),"m_Sprite"),keys);
        var serialized=new SerializedObject(clip);SerializedProperty settings=serialized.FindProperty("m_AnimationClipSettings");if(settings!=null)settings.FindPropertyRelative("m_LoopTime").boolValue=loop;serialized.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(clip);return clip;
    }

    private static AnimationClip WriteHitReactClip(Sprite[] sprites)
    {
        AnimationClip clip=WriteClip(SoldierAnimationRoot+"AN_SOL_HitReact.anim",sprites,HitBloodDurations,false);
        // A 50 Hz sample grid preserves the shortened reaction's 20/40 ms timing. Unity
        // retains one sample after the terminal key, so place the duplicate one tick early.
        clip.frameRate=50f;
        EditorCurveBinding binding=EditorCurveBinding.PPtrCurve("VisualRoot",typeof(SpriteRenderer),"m_Sprite");
        ObjectReferenceKeyframe[] keys=AnimationUtility.GetObjectReferenceCurve(clip,binding);
        keys[keys.Length-1].time=HitBloodDurations.Sum()-(1f/clip.frameRate);
        AnimationUtility.SetObjectReferenceCurve(clip,binding,keys);
        EditorUtility.SetDirty(clip);return clip;
    }

    private static void SetStates(string controllerPath,Dictionary<string,AnimationClip> clips)
    {
        AnimatorController controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);if(controller==null)throw new FileNotFoundException("Animator missing",controllerPath);var machine=controller.layers[0].stateMachine;
        foreach(var pair in clips){AnimatorState state=machine.states.Select(s=>s.state).FirstOrDefault(s=>s.name==pair.Key);if(state==null)state=machine.AddState(pair.Key);state.motion=pair.Value;}EditorUtility.SetDirty(controller);
    }

    private static void ConfigureSoldierActions(AnimationClip attack01,AnimationClip hitReact,AnimationClip death,AnimationClip idle,AnimationClip idleLong,AnimationClip walk)
    {
        ConfigureAction("Assets/Data/Combat/Actions/Soldier/SOL_Idle.asset",idle,ActionCategory.Locomotion,true,0,0,0,null);
        ConfigureAction("Assets/Data/Combat/Actions/Soldier/SOL_IdleLong.asset",idleLong,ActionCategory.Locomotion,true,0,0,0,null);
        ConfigureAction("Assets/Data/Combat/Actions/Soldier/SOL_Walk.asset",walk,ActionCategory.Locomotion,true,0,0,82,null);
        ConfigureSingleAttackAction(attack01);
        ConfigureAction("Assets/Data/Combat/Actions/Soldier/SOL_HitReact.asset",hitReact,ActionCategory.Reaction,false,0,0,HitBloodFrameSelection.Length-1,null);
        ConfigureAction("Assets/Data/Combat/Actions/Soldier/SOL_Death.asset",death,ActionCategory.Death,false,0,0,44,null);
    }

    private static void ConfigureSingleAttackAction(AnimationClip clip)
    {
        int[] hitFrames=Enumerable.Range(3,5).Concat(Enumerable.Range(9,8)).ToArray();
        const string actionPath="Assets/Data/Combat/Actions/Soldier/SOL_Attack01.asset";
        ConfigureAction(actionPath,clip,ActionCategory.NormalAttack,false,2,16,18,hitFrames);
        ActionDefinition action=AssetDatabase.LoadAssetAtPath<ActionDefinition>(actionPath);action.displayName="Two Stage Slash";action.combat.hitPolicy=ActionHitPolicy.OncePerPhase;action.ai.enabledForAI=true;
        foreach(ActionFrameShape shape in action.frameShapes)if(shape.frame>=9){shape.overrideDamage=true;shape.damage=14f;}
        EditorUtility.SetDirty(action);
        float fps=19f/Mathf.Max(.01f,clip.length);SetAttackData("Assets/Data/Combat/Iteration03/ATK_SOL_Attack01.asset",clip.length,3f/fps,14f/fps);
    }

    private static void ConfigureAction(string path,AnimationClip clip,ActionCategory category,bool loop,int startup,int active,int recovery,int[] hitFrames)
    {
        ActionDefinition action=AssetDatabase.LoadAssetAtPath<ActionDefinition>(path);if(action==null)throw new FileNotFoundException("ActionDefinition missing",path);
        int count=Mathf.Max(1,AnimationUtility.GetObjectReferenceCurve(clip,EditorCurveBinding.PPtrCurve("VisualRoot",typeof(SpriteRenderer),"m_Sprite")).Length-1);
        action.animation=clip;action.animatorState=action.id;action.category=category;action.loop=loop;action.framesPerSecond=count/Mathf.Max(.01f,clip.length);action.frameCount=count;action.startupEndFrame=Mathf.Clamp(startup,0,count-1);action.activeEndFrame=Mathf.Clamp(active,action.startupEndFrame,count-1);action.recoveryEndFrame=Mathf.Clamp(recovery,action.activeEndFrame,count-1);
        action.frameShapes.RemoveAll(s=>s!=null&&(s.role==ActionShapeRole.DamageHitbox||s.role==ActionShapeRole.EffectHitbox||s.role==ActionShapeRole.WeaponHitbox));
        if(hitFrames!=null)foreach(int frame in hitFrames)action.frameShapes.Add(new ActionFrameShape{frame=frame,role=ActionShapeRole.DamageHitbox,shapeType=ActionShapeType.Box,center=new Vector2(1.25f,1.05f),size=new Vector2(2.5f,1.8f)});
        EditorUtility.SetDirty(action);
    }

    private static void SetAttackData(string path,float duration,float startup,float active)
    {
        AttackData data=AssetDatabase.LoadAssetAtPath<AttackData>(path);if(data==null)return;data.startup=startup;data.active=active;data.recovery=Mathf.Max(.02f,duration-startup-active);data.rangeX=2.8f;data.rangeDepth=.78f;EditorUtility.SetDirty(data);
    }

    private static void ConfigureDiaDeathAction(AnimationClip clip)
    {
        ActionDefinition action=AssetDatabase.LoadAssetAtPath<ActionDefinition>("Assets/Data/Combat/Actions/Diaochan/DIA_Death.asset");if(action==null)return;action.animation=clip;action.animatorState="Death";action.category=ActionCategory.Death;action.loop=false;action.frameCount=50;action.framesPerSecond=50f/clip.length;action.startupEndFrame=0;action.activeEndFrame=0;action.recoveryEndFrame=49;action.frameShapes.Clear();EditorUtility.SetDirty(action);
    }

    private static void WireSoldierPrefab()
    {
        GameObject root=PrefabUtility.LoadPrefabContents(SoldierPrefabPath);try
        {
            if(root.GetComponent<SoldierReactionController>()==null)root.AddComponent<SoldierReactionController>();
            Transform shadow=root.transform.Find("Shadow");if(shadow!=null)shadow.gameObject.SetActive(true);
            PrefabUtility.SaveAsPrefabAsset(root,SoldierPrefabPath);
        }
        finally{PrefabUtility.UnloadPrefabContents(root);}
    }

    private static void SetSoldierHurtDuration(float duration)
    {
        GameObject root=PrefabUtility.LoadPrefabContents(SoldierPrefabPath);try
        {
            SoldierReactionController controller=root.GetComponent<SoldierReactionController>();
            if(controller==null)controller=root.AddComponent<SoldierReactionController>();
            SerializedObject serialized=new SerializedObject(controller);serialized.FindProperty("hurtDuration").floatValue=duration;serialized.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root,SoldierPrefabPath);
        }
        finally{PrefabUtility.UnloadPrefabContents(root);}
    }

    private static void RemoveObsoleteSoldierAttacks()
    {
        string[] obsoleteIds={"Attack02","RunAttack","JumpAttack"};
        foreach(string id in obsoleteIds)
        {
            AssetDatabase.DeleteAsset(SoldierAnimationRoot+"AN_SOL_"+id+".anim");
            AssetDatabase.DeleteAsset("Assets/Data/Combat/Actions/Soldier/SOL_"+id+".asset");
            AssetDatabase.DeleteAsset("Assets/Data/Combat/Iteration03/ATK_SOL_"+id+".asset");
        }
        AnimatorController controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(SoldierControllerPath);
        if(controller!=null)
        {
            AnimatorStateMachine machine=controller.layers[0].stateMachine;
            foreach(ChildAnimatorState child in machine.states.ToArray())if(obsoleteIds.Contains(child.state.name))machine.RemoveState(child.state);
            EditorUtility.SetDirty(controller);
        }
    }

    private static void CompactSoldierLibrary()
    {
        CharacterActionLibrary library=AssetDatabase.LoadAssetAtPath<CharacterActionLibrary>("Assets/Data/Combat/Actions/LIB_Soldier.asset");if(library==null)return;
        library.actions.RemoveAll(action=>action==null||action.id=="Attack02"||action.id=="RunAttack"||action.id=="JumpAttack");EditorUtility.SetDirty(library);
    }

    private static void WireSingleAttackPrefab()
    {
        GameObject root=PrefabUtility.LoadPrefabContents(SoldierPrefabPath);try
        {
            SoldierAI ai=root.GetComponent<SoldierAI>();if(ai==null)throw new MissingComponentException("SoldierAI missing from soldier prefab");
            SerializedObject serialized=new SerializedObject(ai);serialized.FindProperty("attack01Data").objectReferenceValue=AssetDatabase.LoadAssetAtPath<AttackData>("Assets/Data/Combat/Iteration03/ATK_SOL_Attack01.asset");serialized.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root,SoldierPrefabPath);
        }
        finally{PrefabUtility.UnloadPrefabContents(root);}
    }
}
