using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ThreeKingdoms.EditorTools
{
    public static class SoldierAttackFrameSync
    {
        public static void Sync()
        {
            const string path="Assets/Data/Combat/Actions/Soldier/SOL_Attack01.asset";
            ActionDefinition action=AssetDatabase.LoadAssetAtPath<ActionDefinition>(path);
            if(action==null)throw new System.InvalidOperationException("Missing "+path);
            List<Sprite> frames=ActionEditorUtility.ReadAnimationFrames(action.animation);
            if(frames.Count==0)throw new System.InvalidOperationException("Attack01 has no Sprite frames");
            int oldLast=Mathf.Max(0,action.frameCount-1);Vector2 oldLastOffset=action.VisualOffsetAt(oldLast);
            action.frameCount=frames.Count;action.recoveryEndFrame=frames.Count-1;action.visualFrames=new List<Sprite>(frames);action.dataVersion=ActionDefinition.CurrentVersion;
            for(int i=oldLast+1;i<frames.Count;i++)action.SetVisualOffset(i,oldLastOffset);
            EditorUtility.SetDirty(action);AssetDatabase.SaveAssets();Debug.Log($"[BATTLE_EDITOR] Synced Soldier Attack01 to {frames.Count} exact runtime Sprite frames.");
        }

    }
}
