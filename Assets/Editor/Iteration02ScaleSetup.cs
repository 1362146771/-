using ThreeKingdoms;
using UnityEditor;
using UnityEngine;

public static class Iteration02ScaleSetup
{
    public static void Apply()
    {
        ApplyTo("Assets/Prefabs/Characters/PF_Diaochan.prefab");ApplyTo("Assets/Prefabs/Characters/PF_CommonSoldier.prefab");
        AssetDatabase.SaveAssets();Debug.Log("ITERATION02_SCALE_OK character=2.38 shadow=1.95x1.22 camera=5.1");
    }
    private static void ApplyTo(string path)
    {
        var root=PrefabUtility.LoadPrefabContents(path);
        try
        {
            var motor=root.GetComponent<CharacterMotor>();var serialized=new SerializedObject(motor);
            serialized.FindProperty("visualScale.character").floatValue=2.38f;
            serialized.FindProperty("visualScale.shadow").vector2Value=new Vector2(1.95f,1.22f);
            serialized.ApplyModifiedPropertiesWithoutUndo();PrefabUtility.SaveAsPrefabAsset(root,path);
        }
        finally{PrefabUtility.UnloadPrefabContents(root);}
    }
}
