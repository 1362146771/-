using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ThreeKingdoms.EditorTools
{
    [InitializeOnLoad]
    public static class AllCharacterActionFrameSync
    {
        static AllCharacterActionFrameSync()=>EditorApplication.delayCall+=SyncAllNow;

        [MenuItem("Three Kingdoms/Battle Editor/Sync Exact Frames For All Characters")]
        public static void SyncAllNow()
        {
            string[] guids=AssetDatabase.FindAssets("t:ActionDefinition",new[]{"Assets/Data/Combat/Actions"});
            int changed=0;
            foreach(string guid in guids)
            {
                string path=AssetDatabase.GUIDToAssetPath(guid);ActionDefinition action=AssetDatabase.LoadAssetAtPath<ActionDefinition>(path);
                if(action==null||action.animation==null)continue;List<Sprite> frames=ActionEditorUtility.ReadAnimationFrames(action.animation);if(frames.Count==0)continue;
                bool upgradingReaction=action.dataVersion<ActionDefinition.CurrentVersion&&action.id=="HitReact";
                bool dirty=action.dataVersion!=ActionDefinition.CurrentVersion||action.frameCount!=frames.Count||!SameFrames(action.visualFrames,frames);
                if(!dirty)continue;
                int previousLast=Mathf.Max(0,action.frameCount-1);Vector2 previousLastOffset=action.VisualOffsetAt(previousLast);
                action.dataVersion=ActionDefinition.CurrentVersion;action.frameCount=frames.Count;action.visualFrames=new List<Sprite>(frames);
                if(upgradingReaction)InitializeReactionDefaults(action);
                action.startupEndFrame=Mathf.Clamp(action.startupEndFrame,0,frames.Count-1);
                action.activeEndFrame=Mathf.Clamp(action.activeEndFrame,action.startupEndFrame,frames.Count-1);
                action.recoveryEndFrame=Mathf.Clamp(action.recoveryEndFrame,action.activeEndFrame,frames.Count-1);
                action.frameVisualOffsets?.RemoveAll(item=>item==null||item.frame<0||item.frame>=frames.Count);
                if(frames.Count-1>previousLast&&previousLastOffset.sqrMagnitude>.00000001f)for(int i=previousLast+1;i<frames.Count;i++)action.SetVisualOffset(i,previousLastOffset);
                EditorUtility.SetDirty(action);changed++;
            }
            if(changed>0)AssetDatabase.SaveAssets();
            Debug.Log($"[BATTLE_EDITOR] Exact ActionDefinition frame sync complete for Diaochan, Soldier and CaoCao. Changed {changed} assets; PNG pivots untouched.");
        }

        private static void InitializeReactionDefaults(ActionDefinition action)
        {
            action.reaction=new ActionReactionData();
            action.reaction.light.animationActionId="HitReact";action.reaction.heavy.animationActionId="HitReact";
            if(action.ownerId=="Soldier")
            {
                action.reaction.light.stunDuration=.34f;action.reaction.light.retreatDistance=0f;
                action.reaction.heavy.stunDuration=.46f;action.reaction.heavy.retreatDistance=.45f;
            }
            else if(action.ownerId=="Diaochan")
            {
                action.reaction.light.stunDuration=.54f;action.reaction.light.retreatDistance=.45f;
                action.reaction.heavy.stunDuration=.64f;action.reaction.heavy.retreatDistance=.7f;
            }
        }

        private static bool SameFrames(List<Sprite> current,List<Sprite> expected)
        {
            if(current==null||current.Count!=expected.Count)return false;
            for(int i=0;i<expected.Count;i++)if(current[i]!=expected[i])return false;
            return true;
        }
    }
}
