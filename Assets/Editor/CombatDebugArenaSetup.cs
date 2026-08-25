using System.Linq;
using ThreeKingdoms;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class CombatDebugArenaSetup
{
    public const string ScenePath="Assets/Scenes/SC_CombatRangeDamageTest.unity";

    [MenuItem("Three Kingdoms/Testing/Create Combat Range + Damage Scene")]
    public static void CreateScene()
    {
        Scene original=SceneManager.GetActiveScene();
        Scene scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
        try
        {
            var camera=Create("CombatDebugCamera",scene,typeof(Camera)).GetComponent<Camera>();camera.tag="MainCamera";camera.transform.position=new Vector3(0f,1.35f,-10f);camera.orthographic=true;camera.orthographicSize=4.8f;camera.backgroundColor=new Color(.035f,.055f,.085f);camera.clearFlags=CameraClearFlags.SolidColor;
            var floor=Create("DebugFloor",scene,typeof(SpriteRenderer));floor.transform.position=new Vector3(0f,-1.25f,.5f);floor.transform.localScale=new Vector3(12f,.18f,1f);floor.GetComponent<SpriteRenderer>().color=new Color(.20f,.23f,.27f);
            var backLine=Create("DepthReferenceLine",scene,typeof(SpriteRenderer));backLine.transform.position=new Vector3(0f,1.45f,.6f);backLine.transform.localScale=new Vector3(12f,.025f,1f);backLine.GetComponent<SpriteRenderer>().color=new Color(.18f,.28f,.36f);

            GameObject diaPrefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Characters/PF_Diaochan.prefab");
            GameObject soldierPrefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Characters/PF_CommonSoldier.prefab");
            if(diaPrefab==null||soldierPrefab==null)throw new MissingReferenceException("Combat test prefabs are missing.");
            var player=(GameObject)PrefabUtility.InstantiatePrefab(diaPrefab,scene);player.name="Diaochan_CombatTester";player.GetComponent<CharacterMotor>().SetPosition(-1.65f,0f);
            var dummy=(GameObject)PrefabUtility.InstantiatePrefab(soldierPrefab,scene);dummy.name="Infinite_Dummy_Soldier";dummy.GetComponent<CharacterMotor>().SetPosition(1.55f,0f);
            var ai=dummy.GetComponent<SoldierAI>();if(ai!=null)ai.enabled=false;
            var enemyCombat=dummy.GetComponent<CharacterCombat>();if(enemyCombat!=null)enemyCombat.enabled=false;
            var debug=Create("CombatDebugArena",scene,typeof(CombatDebugArena));

            EditorSceneManager.SaveScene(scene,ScenePath);
            var scenes=EditorBuildSettings.scenes.ToList();if(scenes.All(s=>s.path!=ScenePath)){scenes.Add(new EditorBuildSettingsScene(ScenePath,true));EditorBuildSettings.scenes=scenes.ToArray();}
            Selection.activeObject=AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);AssetDatabase.SaveAssets();
            Debug.Log("COMBAT_DEBUG_ARENA_CREATED path="+ScenePath+" player="+player.name+" dummy="+dummy.name+" controller="+debug.name);
        }
        finally
        {
            EditorSceneManager.CloseScene(scene,true);if(original.IsValid()&&original.isLoaded)SceneManager.SetActiveScene(original);
        }
    }

    [MenuItem("Three Kingdoms/Testing/Open Combat Range + Damage Scene")]
    public static void OpenScene()
    {
        if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
        EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
    }

    private static GameObject Create(string name,Scene scene,params System.Type[] components)
    {
        var go=new GameObject(name,components);SceneManager.MoveGameObjectToScene(go,scene);return go;
    }
}
