using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditorInternal;
using UnityEngine;

namespace ThreeKingdoms.EditorTools
{
    public static class BattleEditorEvidenceCapture
    {
        private sealed class Shot
        {
            public string file, asset; public int frame, tool; public bool left, validate;
            public ActionShapeRole role;
            public Shot(string file,string asset,int frame=0,int tool=0,ActionShapeRole role=ActionShapeRole.EffectHitbox,bool left=false,bool validate=false){this.file=file;this.asset=asset;this.frame=frame;this.tool=tool;this.role=role;this.left=left;this.validate=validate;}
        }
        private static readonly string Output = @"C:\Users\shanghai\Desktop\三国战纪\Screenshots\BattleEditorV01";
        private static ThreeKingdomsBattleEditorWindow window;
        private static Queue<Shot> shots;
        private static Shot current;
        private static int settleFrames;

        public static void CaptureAll()
        {
            Directory.CreateDirectory(Output);
            string dia="Assets/Data/Combat/Actions/Diaochan/",sol="Assets/Data/Combat/Actions/Soldier/";
            shots=new Queue<Shot>(new[]{
                new Shot("01_editor_overview.png",dia+"DIA_Skill1.asset",5),
                new Shot("02_character_action_browser.png",dia+"DIA_CombatIdle.asset",0),
                new Shot("03_animation_frame_preview.png",dia+"DIA_Skill1.asset",6),
                new Shot("04_box_hitbox_edit.png",sol+"SOL_Attack01.asset",3,1,ActionShapeRole.DamageHitbox),
                new Shot("05_polygon_hitbox_edit.png",dia+"DIA_Skill1.asset",6,2),
                new Shot("06_effect_hitbox_skill1.png",dia+"DIA_Skill1.asset",7,0),
                new Shot("07_facing_right.png",dia+"DIA_Skill1.asset",7),
                new Shot("08_facing_left.png",dia+"DIA_Skill1.asset",7,0,ActionShapeRole.EffectHitbox,true),
                new Shot("09_timeline.png",dia+"DIA_Skill3_A.asset",17),
                new Shot("10_combo_editor.png",dia+"DIA_AttackCombo4.asset",12),
                new Shot("11_movement_elevation.png",dia+"DIA_Skill3_A.asset",14),
                new Shot("12_combat_settings.png",sol+"SOL_Attack01.asset",4),
                new Shot("13_validation_pass.png",dia+"DIA_Skill1.asset",5,0,ActionShapeRole.EffectHitbox,false,true),
                new Shot("15_skill1_dogfood.png",dia+"DIA_Skill1.asset",6,2),
                new Shot("16_skill3_dogfood.png",dia+"DIA_Skill3_A.asset",17,2),
                new Shot("17_soldier_attack_dogfood.png",sol+"SOL_Attack01.asset",4,1,ActionShapeRole.DamageHitbox)
            });
            window=ThreeKingdomsBattleEditorWindow.Open();window.position=new Rect(60,60,1500,900);window.maximized=false;
            EditorApplication.update+=Tick;Next();
        }
        public static void BuildTestArena()
        {
            const string output=@"C:\Users\shanghai\Desktop\三国战纪\Build\BattleEditorTest\BattleEditorTest.exe";Directory.CreateDirectory(Path.GetDirectoryName(output));
            BuildReport report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{"Assets/Scenes/SC_BattleEditorTest.unity"},locationPathName=output,target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development});
            Debug.Log("[BATTLE_EDITOR_TEST_BUILD] result="+report.summary.result+" errors="+report.summary.totalErrors);if(report.summary.result!=BuildResult.Succeeded)throw new InvalidOperationException("Battle Editor test arena build failed.");
        }
        private static void Next()
        {
            if(shots.Count==0){EditorApplication.update-=Tick;window.Close();EditorApplication.Exit(0);return;}
            current=shots.Dequeue();var action=AssetDatabase.LoadAssetAtPath<ActionDefinition>(current.asset);window.ConfigureEvidence(action,current.frame,current.left,current.tool,current.role,current.validate);settleFrames=20;
        }
        private static void Tick()
        {
            if(--settleFrames>0){window.Repaint();return;}
            Rect r=window.position;int width=Mathf.Max(1,Mathf.RoundToInt(r.width)),height=Mathf.Max(1,Mathf.RoundToInt(r.height));
            Color[] pixels=InternalEditorUtility.ReadScreenPixel(new Vector2(r.x,r.y),width,height);var texture=new Texture2D(width,height,TextureFormat.RGB24,false);texture.SetPixels(pixels);texture.Apply();
            File.WriteAllBytes(Path.Combine(Output,current.file),texture.EncodeToPNG());UnityEngine.Object.DestroyImmediate(texture);Debug.Log("[BATTLE_EDITOR_CAPTURE] "+current.file);Next();
        }
    }
}
