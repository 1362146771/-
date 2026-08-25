using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ThreeKingdoms.EditorTools
{
    public static class BattleEditorMigration
    {
        private const string Root = "Assets/Data/Combat/Actions";
        [MenuItem("Tools/Three Kingdoms/Combat/Import Missing Actions (Safe)")]
        public static void MigrateExistingCombatData()
        {
            EnsureFolder("Assets/Data/Combat"); EnsureFolder(Root); EnsureFolder(Root+"/Diaochan"); EnsureFolder(Root+"/Soldier"); EnsureFolder(Root+"/CaoCao");
            var dia = new List<ActionDefinition>(); var sol = new List<ActionDefinition>(); var cao = new List<ActionDefinition>();int created=0,preserved=0;
            MigrateOwner("Diaochan","Assets/Animations/Diaochan","AN_DIA_",Root+"/Diaochan",dia,ref created,ref preserved);
            MigrateOwner("Soldier","Assets/Animations/Soldier","AN_SOL_",Root+"/Soldier",sol,ref created,ref preserved);
            MigrateOwner("CaoCao","Assets/Animations/CaoCao","AN_CAO_",Root+"/CaoCao",cao,ref created,ref preserved);
            SaveLibrary(Root+"/LIB_Diaochan.asset","Diaochan",dia); SaveLibrary(Root+"/LIB_Soldier.asset","Soldier",sol); SaveLibrary(Root+"/LIB_CaoCao.asset","CaoCao",cao);
            WirePrefab("Assets/Prefabs/Characters/PF_Diaochan.prefab",Root+"/LIB_Diaochan.asset"); WirePrefab("Assets/Prefabs/Characters/PF_CommonSoldier.prefab",Root+"/LIB_Soldier.asset"); WirePrefab("Assets/Prefabs/Characters/PF_CaoCaoBoss.prefab",Root+"/LIB_CaoCao.asset");
            AssetDatabase.SaveAssets(); AssetDatabase.Refresh(); Debug.Log($"[BATTLE_EDITOR] Safe import complete: created={created}, preserved={preserved}. Existing Action frame ranges, shapes, movement and combat values were not modified.");
        }
        public static void RunBatch() { MigrateExistingCombatData(); CreateTestScene(); }
        private static void MigrateOwner(string owner,string animationFolder,string prefix,string target,List<ActionDefinition> output,ref int created,ref int preserved)
        {
            foreach(string guid in AssetDatabase.FindAssets("t:AnimationClip",new[]{animationFolder}))
            {
                string path=AssetDatabase.GUIDToAssetPath(guid);AnimationClip clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);if(clip==null)continue;
                string id=clip.name.StartsWith(prefix)?clip.name.Substring(prefix.Length):clip.name;string shortOwner=owner=="Diaochan"?"DIA":owner=="Soldier"?"SOL":"CAO";string assetPath=$"{target}/{shortOwner}_{id}.asset";
                ActionDefinition action=AssetDatabase.LoadAssetAtPath<ActionDefinition>(assetPath);
                if(action==null)
                {
                    action=ScriptableObject.CreateInstance<ActionDefinition>();AssetDatabase.CreateAsset(action,assetPath);created++;
                    action.id=id;action.ownerId=owner;action.displayName=id;action.animation=clip;action.animatorState=id;
                    int actual=ActionEditorUtility.ReadAnimationFrames(clip).Count;action.frameCount=Mathf.Max(1,actual);action.framesPerSecond=actual>0&&clip.length>0f?actual/clip.length:clip.frameRate;action.recoveryEndFrame=action.frameCount-1;
                    action.category=Category(id);action.loop=id.Contains("Idle")||id=="Walk"||id=="Run";action.startupEndFrame=Mathf.Clamp(Mathf.RoundToInt(action.frameCount*.3f),0,action.frameCount-1);action.activeEndFrame=Mathf.Clamp(Mathf.RoundToInt(action.frameCount*.65f),action.startupEndFrame,action.frameCount-1);
                    AttackData legacy=FindAttack(owner,id);if(legacy!=null)CopyLegacy(action,legacy);
                    // Only real attacks receive a generated hit shape. Reactions, guard, entrance and death
                    // remain editable actions but must never deal damage just because they are non-locomotion.
                    if(legacy!=null)CreateDogfoodShape(action,id);
                    if(id=="AttackCombo4")CreateCombo(action);
                    EditorUtility.SetDirty(action);
                }
                else
                {
                    // Existing assets are authored data. Never recalculate frame phases, copy legacy values,
                    // delete frame shapes, rebuild movement curves, or rewrite combo segments here.
                    preserved++;
                }
                output.Add(action);
            }
        }
        private static ActionCategory Category(string id)
        {
            if(id=="Idle"||id=="IdleAction"||id=="CombatIdle"||id=="Walk"||id=="Run"||id=="Crouch"||id=="IdleLong")return ActionCategory.Locomotion;
            if(id=="Guard")return ActionCategory.Defense;if(id.Contains("Skill")||id.Contains("Ultimate")||id=="ParryCounter")return ActionCategory.Skill;if(id=="HeavyAttack")return ActionCategory.HeavyAttack;if(id=="Dodge")return ActionCategory.Dodge;if(id.Contains("Jump"))return ActionCategory.Jump;if(id=="Death")return ActionCategory.Death;
            if(id=="Thrust"||id=="DownSlash"||id=="DownSlashWave"||id=="Charge"||id=="ChargedCharge"||id=="CrouchAttack"||id.Contains("Attack"))return ActionCategory.NormalAttack;return ActionCategory.Reaction;
        }
        private static AttackData FindAttack(string owner,string id)
        {
            string folder=owner=="Diaochan"?"Assets/Data/Combat/Iteration02":owner=="Soldier"?"Assets/Data/Combat/Iteration03":"Assets/Data/Combat/CaoCao";
            string[] guids=AssetDatabase.FindAssets("t:AttackData",new[]{folder});foreach(string guid in guids){var a=AssetDatabase.LoadAssetAtPath<AttackData>(AssetDatabase.GUIDToAssetPath(guid));if(a!=null&&(a.actionId==id||(id=="Skill3_A"&&a.actionId=="Skill3_A")))return a;}return null;
        }
        private static void CopyLegacy(ActionDefinition a,AttackData old)
        {
            a.legacyAttackData=old;a.animatorState=old.animationState;a.combat.damage=old.damage;a.combat.hitStop=old.hitStop;a.combat.knockbackX=old.knockbackX;a.combat.knockbackDepth=old.knockbackDepth;a.combat.cooldown=old.cooldown;a.combat.priority=old.priority;a.combat.broadPhaseDepth=old.rangeDepth;
            a.lockGroundPosition=old.lockGroundPosition;a.startupEndFrame=a.ClampFrame(Mathf.FloorToInt(old.startup*a.framesPerSecond));a.activeEndFrame=a.ClampFrame(Mathf.CeilToInt((old.startup+old.active)*a.framesPerSecond)-1);a.recoveryEndFrame=a.frameCount-1;
            a.movement.moveX=AnimationCurve.Linear(0,0,1,old.forwardMove);a.ai.maxDistance=old.rangeX;a.ai.depthTolerance=old.rangeDepth;a.ai.cooldown=old.cooldown;
            if(a.ownerId=="Soldier"){a.ai.enabledForAI=true;a.ai.usageWeight=a.id=="Attack01"?1f:a.id=="Attack02"?.65f:a.id=="RunAttack"?.55f:.3f;a.ai.minDistance=a.id.Contains("Run")||a.id.Contains("Jump")?1.4f:0f;}
            if(a.ownerId=="CaoCao"){a.ai.enabledForAI=true;a.ai.usageWeight=a.id.Contains("Ultimate")?.28f:a.id.Contains("Charge")?.55f:1f;a.ai.minDistance=a.id.Contains("Charge")?1.5f:0f;a.combat.superArmor=a.id=="Skill"||a.id.Contains("Ultimate");}
        }
        private static void CreateDogfoodShape(ActionDefinition a,string id)
        {
            float reach=a.legacyAttackData==null?1.8f:a.legacyAttackData.rangeX;float height=id.Contains("Skill3")?2.8f:1.7f;
            for(int f=a.startupEndFrame;f<=a.activeEndFrame;f++)
            {
                float pulse=1f-Mathf.Abs((f-(a.startupEndFrame+a.activeEndFrame)*.5f)/Mathf.Max(1,(a.activeEndFrame-a.startupEndFrame+1)*.5f))*.15f;
                var points=new[]{new Vector2(.1f,.15f),new Vector2(reach*pulse,.25f),new Vector2(reach*pulse,height*pulse),new Vector2(.35f,height)};
                ActionEditorUtility.CreatePolygon(a,f,a.category==ActionCategory.Skill?ActionShapeRole.EffectHitbox:ActionShapeRole.DamageHitbox,points);
            }
            if(id=="Skill3_A")a.movement.elevation=new AnimationCurve(new Keyframe(0,0),new Keyframe(.3f,1.1f),new Keyframe(.7f,1.1f),new Keyframe(1,0));
        }
        private static void CreateCombo(ActionDefinition a){foreach(ComboSegment s in CharacterCombat.DiaochanCombo)a.combo.Add(new ActionComboSegment{startFrame=Mathf.RoundToInt(s.startTime*a.framesPerSecond),hitStartFrame=Mathf.RoundToInt(s.hitStart*a.framesPerSecond),hitEndFrame=Mathf.RoundToInt(s.hitEnd*a.framesPerSecond),comboWindowStart=Mathf.RoundToInt(s.comboWindowStart*a.framesPerSecond),comboWindowEnd=Mathf.RoundToInt(s.comboWindowEnd*a.framesPerSecond),endFrame=Mathf.Min(a.frameCount-1,Mathf.RoundToInt(s.segmentEnd*a.framesPerSecond)),damage=s.damage,forwardMove=s.forwardMove,knockback=s.knockback});}
        private static void SaveLibrary(string path,string owner,List<ActionDefinition> list){var lib=AssetDatabase.LoadAssetAtPath<CharacterActionLibrary>(path);if(lib==null){lib=ScriptableObject.CreateInstance<CharacterActionLibrary>();AssetDatabase.CreateAsset(lib,path);}lib.ownerId=owner;lib.actions=new List<ActionDefinition>(list);EditorUtility.SetDirty(lib);}
        private static void WirePrefab(string prefabPath,string libraryPath)
        {
            if(AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath)==null)return;
            GameObject root=PrefabUtility.LoadPrefabContents(prefabPath);try{var combat=root.GetComponent<CharacterCombat>();var runner=root.GetComponent<ActionRunner>()??root.AddComponent<ActionRunner>();var lib=AssetDatabase.LoadAssetAtPath<CharacterActionLibrary>(libraryPath);if(combat!=null)combat.ConfigureActionLibrary(lib);runner.Configure(lib);PrefabUtility.SaveAsPrefabAsset(root,prefabPath);}finally{PrefabUtility.UnloadPrefabContents(root);}
        }
        private static void EnsureFolder(string path){if(AssetDatabase.IsValidFolder(path))return;int slash=path.LastIndexOf('/');string parent=path.Substring(0,slash),name=path.Substring(slash+1);EnsureFolder(parent);AssetDatabase.CreateFolder(parent,name);}

        public static void OpenTestScene(ActionDefinition action){if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;SessionState.SetString("TK.BattleEditor.TestAction",action==null?"":AssetDatabase.GetAssetPath(action));CreateTestScene();EditorSceneManager.OpenScene("Assets/Scenes/SC_BattleEditorTest.unity");EditorApplication.delayCall+=()=>EditorApplication.isPlaying=true;}
        public static void CreateTestScene()
        {
            const string path="Assets/Scenes/SC_BattleEditorTest.unity";Scene scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var camera=new GameObject("BattleEditorCamera",typeof(Camera));camera.transform.position=new Vector3(0,1,-10);var c=camera.GetComponent<Camera>();c.orthographic=true;c.orthographicSize=4.5f;c.backgroundColor=new Color(.04f,.07f,.11f);
            var ground=new GameObject("TestGround",typeof(SpriteRenderer));ground.transform.position=new Vector3(0,-.8f,0);ground.transform.localScale=new Vector3(10,.15f,1);ground.GetComponent<SpriteRenderer>().color=new Color(.22f,.25f,.28f);
            GameObject diaPrefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Characters/PF_Diaochan.prefab"),solPrefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Characters/PF_CommonSoldier.prefab");
            if(diaPrefab!=null){var dia=(GameObject)PrefabUtility.InstantiatePrefab(diaPrefab,scene);dia.name="Diaochan_ActionTester";dia.transform.position=new Vector3(-1.2f,-.2f,0);dia.GetComponent<PlayerInputController>().enabled=false;}
            if(solPrefab!=null){var sol=(GameObject)PrefabUtility.InstantiatePrefab(solPrefab,scene);sol.name="Dummy_Soldier";sol.transform.position=new Vector3(1.2f,-.05f,0);sol.GetComponent<SoldierAI>().enabled=false;}
            new GameObject("BattleEditorTestDirector",typeof(BattleEditorTestDirector));EditorSceneManager.SaveScene(scene,path);AssetDatabase.SaveAssets();
        }
    }
}
