using ThreeKingdoms;
using UnityEditor;
using UnityEngine;

public static class Iteration02CombatDataSetup
{
    private const string Root="Assets/Data/Combat/Iteration02";

    [MenuItem("Three Kingdoms/Iteration 02/Setup Combat Data")]
    public static void Setup()
    {
        EnsureFolder("Assets/Data");EnsureFolder("Assets/Data/Combat");EnsureFolder(Root);
        var skill1=Attack("ATK_DIA_Skill1","Skill1","Skill1",.42f,.34f,.49f,24f,2.7f,.82f,.75f,1.8f);
        var counter=Attack("ATK_DIA_ParryCounter","ParryCounter","ParryCounter",.18f,.24f,.44f,32f,2.55f,.78f,1.05f,0f,ActionPriority.ParrySuccess);
        var charge=Attack("ATK_DIA_ChargeSkill2","ChargeSkill2","ChargeSkill2",.30f,.35f,.55f,22f,2.65f,.82f,.85f,2.5f);
        var skill3=Attack("ATK_DIA_Skill3","Skill3_A","Skill3_A",.56f,1.50f,.50f,38f,3.55f,1.05f,1.25f,3.5f);
        var skill4=Attack("ATK_DIA_Skill4","Skill4","Skill4",.28f,.34f,.33f,32f,3.0f,.92f,1.0f,4.5f);
        var parry=LoadOrCreate<ParryData>(Root+"/ATK_DIA_Parry.asset");
        parry.parryStartup=.08f;parry.parryWindowStart=.08f;parry.parryWindowEnd=.30f;parry.recovery=.28f;parry.cooldown=1.5f;parry.counterAttack=counter;EditorUtility.SetDirty(parry);

        const string prefabPath="Assets/Prefabs/Characters/PF_Diaochan.prefab";
        var root=PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            var combat=root.GetComponent<CharacterCombat>();if(combat==null)throw new MissingComponentException("CharacterCombat missing from Diaochan prefab");
            var serialized=new SerializedObject(combat);
            Set(serialized,"skill1Data",skill1);Set(serialized,"chargeSkill2Data",charge);Set(serialized,"skill3Data",skill3);Set(serialized,"skill4Data",skill4);
            Set(serialized,"parryCounterData",counter);Set(serialized,"parryData",parry);serialized.ApplyModifiedPropertiesWithoutUndo();
            var input=root.GetComponent<PlayerInputController>();if(input==null)throw new MissingComponentException("PlayerInputController missing from Diaochan prefab");
            var serializedInput=new SerializedObject(input);serializedInput.FindProperty("phaseBEnabled").boolValue=true;serializedInput.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root,prefabPath);
        }
        finally{PrefabUtility.UnloadPrefabContents(root);}
        AssetDatabase.SaveAssets();Debug.Log("ITERATION02_COMBAT_DATA_OK skills=5 parry=real chargeLevels=3 dc18=unbound");
    }

    private static AttackData Attack(string file,string id,string animation,float startup,float active,float recovery,float damage,float rangeX,float rangeDepth,float knockback,float cooldown,ActionPriority priority=ActionPriority.Skill)
    {
        var data=LoadOrCreate<AttackData>(Root+"/"+file+".asset");data.actionId=id;data.animationState=animation;data.priority=priority;
        data.startup=startup;data.active=active;data.recovery=recovery;data.damage=damage;data.rangeX=rangeX;data.rangeDepth=rangeDepth;data.knockbackX=knockback;data.knockbackDepth=.08f;data.hitStop=.04f;data.cooldown=cooldown;EditorUtility.SetDirty(data);return data;
    }
    private static T LoadOrCreate<T>(string path) where T:ScriptableObject
    {
        var asset=AssetDatabase.LoadAssetAtPath<T>(path);if(asset!=null)return asset;
        if(AssetDatabase.LoadMainAssetAtPath(path)!=null)AssetDatabase.DeleteAsset(path);
        asset=ScriptableObject.CreateInstance<T>();AssetDatabase.CreateAsset(asset,path);return asset;
    }
    private static void EnsureFolder(string path)
    {
        if(AssetDatabase.IsValidFolder(path))return;int slash=path.LastIndexOf('/');AssetDatabase.CreateFolder(path.Substring(0,slash),path.Substring(slash+1));
    }
    private static void Set(SerializedObject serialized,string property,Object value)=>serialized.FindProperty(property).objectReferenceValue=value;
}
