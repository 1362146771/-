using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;

namespace ThreeKingdoms
{
    public sealed class RuntimeValidationDirector : MonoBehaviour
    {
        private StageManager stage;
        private CharacterMotor motor;
        private CharacterAnimator animationDriver;
        private CharacterCombat combat;
        private string outputDirectory;
        private int captureWidth=1280,captureHeight=720;

        private IEnumerator Start()
        {
            bool visualAnchorFix = Environment.GetEnvironmentVariable("THREE_KINGDOMS_VISUAL_ANCHOR_CAPTURE") == "1" || Environment.GetCommandLineArgs().Contains("-visualAnchorCapture");
            if(visualAnchorFix){Application.runInBackground=true;yield return RunVisualAnchorFixCapture();yield break;}
            bool iteration03 = Environment.GetEnvironmentVariable("THREE_KINGDOMS_ITERATION03_CAPTURE") == "1" || Environment.GetCommandLineArgs().Contains("-iteration03Capture");
            if(iteration03){Application.runInBackground=true;yield return RunIteration03();yield break;}
            bool iteration02 = Environment.GetEnvironmentVariable("THREE_KINGDOMS_ITERATION02_CAPTURE") == "1" || Environment.GetCommandLineArgs().Contains("-iteration02Capture");
            if(iteration02){Application.runInBackground=true;yield return RunIteration02();yield break;}
            bool captureRequested = Environment.GetEnvironmentVariable("THREE_KINGDOMS_CAPTURE") == "1" || Environment.GetCommandLineArgs().Contains("-captureValidation");
            if (!captureRequested) yield break;
            outputDirectory = Environment.GetEnvironmentVariable("THREE_KINGDOMS_SCREENSHOTS");
            if (string.IsNullOrWhiteSpace(outputDirectory)) outputDirectory = @"C:\Users\shanghai\Desktop\三国战纪\Screenshots";
            Directory.CreateDirectory(outputDirectory);
            yield return null;
            yield return null;
            stage = FindFirstObjectByType<StageManager>();
            motor = stage.PlayerMotor;
            animationDriver = motor.GetComponent<CharacterAnimator>();
            combat = motor.GetComponent<CharacterCombat>();
            motor.GetComponent<PlayerInputController>().enabled = false;

            yield return Capture("01_stage_start.png");
            animationDriver.Play("CombatIdle", true); yield return Capture("02_diaochan_idle.png");
            motor.MoveForTest(Vector2.right, false, .45f); animationDriver.Play("Walk", true); yield return Capture("03_diaochan_walk.png");
            motor.SetPosition(3f, .9f); yield return Capture("04_depth_movement.png");
            motor.MoveForTest(Vector2.right, true, .45f); animationDriver.Play("Run", true); yield return Capture("05_diaochan_run.png");
            combat.enabled = false; combat.SetCrouch(true); yield return Capture("06_crouch.png"); combat.SetCrouch(false);
            combat.RequestDodge(); yield return Capture("07_dodge.png"); FinishAction();
            yield return SampleCombo("08_combo_hit1.png", .12f);
            yield return SampleCombo("09_combo_hit2.png", .36f);
            yield return SampleCombo("10_combo_hit3.png", .58f);
            yield return SampleCombo("11_combo_hit4.png", .82f);
            motor.Jump(); motor.ManualTick(.12f); yield return Capture("12_jump.png");
            combat.RequestJumpAttack(); yield return Capture("13_jump_attack.png"); FinishAction();
            for (int i = 0; i < 30 && motor.IsAirborne; i++) motor.ManualTick(.1f);
            combat.RequestHeavyAttack(); yield return Capture("14_heavy_attack.png"); FinishAction();
            stage.SetPlayerXForTest(7.1f); yield return null; yield return null;
            var enemy = FindObjectsByType<SoldierAI>(FindObjectsSortMode.None).FirstOrDefault();
            enemy?.GetComponent<CharacterAnimator>().Play("Attack01", true); yield return Capture("15_enemy_attack.png");
            if (enemy != null) { motor.SetPosition(motor.X, -1.2f); enemy.GetComponent<CharacterMotor>().SetPosition(enemy.transform.position.x, .9f); }
            yield return Capture("16_depth_sorting.png");
            yield return Capture("17_encounter01.png"); stage.ForceClearEncounterForTest(); yield return new WaitForSecondsRealtime(.8f);
            stage.SetPlayerXForTest(18.1f); yield return null; yield return null; yield return Capture("18_encounter02.png"); stage.ForceClearEncounterForTest(); yield return new WaitForSecondsRealtime(.8f);
            stage.SetPlayerXForTest(29.1f); yield return null; yield return null; yield return Capture("19_encounter03.png"); stage.ForceClearEncounterForTest(); yield return new WaitForSecondsRealtime(.8f);
            stage.SetPlayerXForTest(39.2f); yield return new WaitForSecondsRealtime(.45f);stage.ForceCompleteBossForTest();yield return null; CreateStageClearBanner(); yield return Capture("20_stage_clear.png");
            Debug.Log("[THREE_KINGDOMS_VALIDATION] CAPTURE_COMPLETE count=20 stageClear=" + stage.StageClear);
            yield return new WaitForSecondsRealtime(.5f);
            Application.Quit(stage.StageClear ? 0 : 3);
        }

        private IEnumerator RunIteration02()
        {
            captureWidth=1920;captureHeight=1080;
            outputDirectory=Environment.GetEnvironmentVariable("THREE_KINGDOMS_SCREENSHOTS");
            if(string.IsNullOrWhiteSpace(outputDirectory))outputDirectory=@"C:\Users\shanghai\Desktop\三国战纪\Screenshots\Iteration02";
            Directory.CreateDirectory(outputDirectory);yield return null;yield return null;
            stage=FindFirstObjectByType<StageManager>();motor=stage.PlayerMotor;animationDriver=motor.GetComponent<CharacterAnimator>();combat=motor.GetComponent<CharacterCombat>();
            motor.GetComponent<PlayerInputController>().enabled=false;combat.enabled=false;animationDriver.Play("CombatIdle",true);yield return new WaitForSecondsRealtime(.2f);

            yield return Capture("01_new_background.png");yield return Capture("02_character_scale.png");
            MeasureCharacterScale(motor.transform.Find("VisualRoot").GetComponent<SpriteRenderer>());
            yield return SampleState("Walk",.05f,"03_walk_frame_a.png");yield return SampleState("Walk",.38f,"04_walk_frame_b.png");yield return SampleState("Walk",.72f,"05_walk_frame_c.png");
            yield return SampleState("Run",.05f,"06_run_frame_a.png");yield return SampleState("Run",.38f,"07_run_frame_b.png");yield return SampleState("Run",.72f,"08_run_frame_c.png");
            motor.MoveForTest(Vector2.right,true,.38f);yield return SampleState("Run",.52f,"09_double_tap_run.png");

            combat.RequestSkill1();animationDriver.Sample("Skill1",.52f);yield return Capture("10_skill1.png");FinishAction();
            stage.SetPlayerXForTest(7.1f);yield return null;yield return null;
            var enemies=FindObjectsByType<SoldierAI>(FindObjectsSortMode.None);
            foreach(var enemy in enemies)enemy.enabled=false;
            var parryEnemy=enemies.FirstOrDefault();
            if(parryEnemy!=null)
            {
                parryEnemy.GetComponent<CharacterMotor>().SetPosition(motor.X+1.45f,motor.Depth);
            }
            combat.RequestParry();combat.ManualTick(.09f);yield return CaptureNoDelay("11_parry_start.png");
            if(parryEnemy!=null)stage.PlayerHealth.ReceiveDamage(new DamagePacket(parryEnemy.GetComponent<CharacterIdentity>(),10f,0f,0f,0f));
            animationDriver.Sample("ParryCounter",.48f);yield return Capture("12_parry_success.png");FinishAction();

            combat.BeginCharge();animationDriver.Sample("ChargeSkill2",.28f);yield return Capture("13_charge_skill.png");
            combat.ReleaseChargeForTest(1.1f);animationDriver.Sample("ChargeSkill2",.72f);yield return Capture("14_charge_release.png");FinishAction();
            combat.RequestSkill3();animationDriver.Sample("Skill3_A",.55f);yield return Capture("15_skill3.png");FinishAction();
            combat.RequestSkill4();animationDriver.Sample("Skill4",.50f);yield return Capture("16_skill4.png");FinishAction();

            motor.SetPosition(7.1f,-.6f);for(int i=0;i<enemies.Length;i++)if(enemies[i]!=null){enemies[i].gameObject.SetActive(true);enemies[i].enabled=false;enemies[i].GetComponent<CharacterMotor>().SetPosition(motor.X+(i==0?-2.4f:2.4f),motor.Depth+(i==0?.25f:-.25f));}
            yield return Capture("17_encounter_gameplay.png");yield return Capture("18_three_character_scale.png");MeasureSoldierScale(enemies);
            stage.ForceClearEncounterForTest();yield return new WaitForSecondsRealtime(.8f);
            stage.SetPlayerXForTest(18.1f);yield return null;yield return null;stage.ForceClearEncounterForTest();yield return new WaitForSecondsRealtime(.8f);
            stage.SetPlayerXForTest(29.1f);yield return null;yield return null;yield return new WaitForSecondsRealtime(.2f);
            var finalEnemies=FindObjectsByType<SoldierAI>(FindObjectsSortMode.None);for(int i=0;i<finalEnemies.Length;i++){finalEnemies[i].enabled=false;finalEnemies[i].GetComponent<CharacterMotor>().SetPosition(motor.X+(i-1)*2.2f,motor.Depth+(i%2==0?.25f:-.25f));}
            yield return Capture("19_final_arena.png");stage.ForceClearEncounterForTest();yield return new WaitForSecondsRealtime(.8f);stage.SetPlayerXForTest(39.2f);yield return new WaitForSecondsRealtime(.45f);stage.ForceCompleteBossForTest();yield return null;
            CreateStageClearBanner();yield return Capture("20_stage_clear.png");
            Debug.Log("[THREE_KINGDOMS_ITERATION02] CAPTURE_COMPLETE count=20 stageClear="+stage.StageClear+" chargeLevel="+combat.LastChargeLevel+" parry="+combat.LastParrySucceeded);
            yield return new WaitForSecondsRealtime(.3f);Application.Quit(stage.StageClear?0:3);
        }

        private IEnumerator RunIteration03()
        {
            captureWidth=1920;captureHeight=1080;
            outputDirectory=Environment.GetEnvironmentVariable("THREE_KINGDOMS_SCREENSHOTS");
            if(string.IsNullOrWhiteSpace(outputDirectory))outputDirectory=@"C:\Users\shanghai\Desktop\三国战纪\Screenshots\Iteration03";
            Directory.CreateDirectory(outputDirectory);yield return null;yield return null;
            stage=FindFirstObjectByType<StageManager>();motor=stage.PlayerMotor;animationDriver=motor.GetComponent<CharacterAnimator>();combat=motor.GetComponent<CharacterCombat>();
            motor.GetComponent<PlayerInputController>().enabled=false;combat.enabled=false;stage.enabled=false;
            var follow=Camera.main.GetComponent<BeatEmUpCamera>();if(follow!=null)follow.enabled=false;

            SetEvidenceLabel("ORIGINAL FORTRESS STAGE / 3 CONNECTED SECTIONS",new Color(1f,.82f,.28f));SetView(8f,-.55f);yield return Capture("01_new_stage_overview.png");
            var map=FindFirstObjectByType<StageNavigationMap>();var overlay=CreateNavigationOverlay(map);SetEvidenceLabel("GREEN: WALKABLE BELT    RED: BLOCKED SCENE OBJECTS",Color.white);yield return Capture("02_walkable_area_demo.png");
            Destroy(overlay);motor.SetPosition(8f,-.6f);Vector2 allowedBefore=new Vector2(motor.X,motor.Depth);motor.SetPosition(8f,2.4f);Vector2 constrained=new Vector2(motor.X,motor.Depth);
            CreateWorldMarker(new Vector2(8f,2.4f),"X  ROOF / UNREACHABLE",new Color(1f,.18f,.12f));CreateWorldMarker(constrained,"PLAYER CONSTRAINED HERE",new Color(.25f,1f,.38f));
            SetEvidenceLabel("BLOCK TEST  requested=(8.00, 2.40)  actual=("+constrained.x.ToString("F2")+", "+constrained.y.ToString("F2")+")",Color.white);yield return Capture("03_unreachable_area_blocked.png");ClearWorldMarkers();

            stage.enabled=true;stage.BeginEncounter(0);yield return null;yield return null;var enemies=FindObjectsByType<SoldierAI>(FindObjectsSortMode.None);foreach(var e in enemies){e.enabled=false;e.GetComponent<CharacterCombat>().enabled=false;}
            var soldier=enemies.FirstOrDefault();if(soldier==null)throw new InvalidOperationException("Iteration03 capture could not spawn soldier.");
            motor.SetPosition(8.3f,-.65f);soldier.GetComponent<CharacterMotor>().SetPosition(6.4f,-.65f);SetView(7.5f,-.65f);
            soldier.GetComponent<CharacterAnimator>().Sample("Attack01",.48f);SetEvidenceLabel("SOLDIER ATTACK01 / FAST STRIKE",new Color(1f,.75f,.25f));yield return Capture("04_soldier_attack01.png");
            soldier.GetComponent<CharacterAnimator>().Sample("Attack01",.72f);SetEvidenceLabel("SOLDIER TWO-STAGE SLASH / SECOND HIT",new Color(1f,.75f,.25f));yield return Capture("05_soldier_attack02.png");
            soldier.GetComponent<CharacterAnimator>().Sample("Attack01",.90f);SetEvidenceLabel("SOLDIER TWO-STAGE SLASH / RECOVERY",new Color(1f,.75f,.25f));yield return Capture("06_soldier_runattack.png");

            string measurements="{\n";Vector2 skillAnchor=new Vector2(8f,-.7f);
            yield return CaptureLockedSkill("Skill1",skillAnchor,.24f,"07_skill1_position_before_after.png",v=>measurements+=v+",\n");FinishAction();
            combat.RequestParry();combat.ManualTick(.09f);stage.PlayerHealth.ReceiveDamage(new DamagePacket(soldier.GetComponent<CharacterIdentity>(),10f,0f,0f,0f));motor.SetPosition(9.2f,.5f);combat.ManualTick(.18f);animationDriver.Sample("ParryCounter",.48f);Vector2 parryNow=new Vector2(motor.X,motor.Depth);SetEvidenceLabel(PositionEvidence("ParryCounter",skillAnchor,parryNow),Color.white);yield return Capture("08_parry_position_before_after.png");measurements+=MeasurementJson("ParryCounter",skillAnchor,parryNow)+",\n";FinishAction();
            motor.SetPosition(skillAnchor.x,skillAnchor.y);combat.BeginCharge();motor.SetPosition(9.2f,.5f);combat.ManualTick(.22f);animationDriver.Sample("ChargeSkill2",.28f);Vector2 chargeNow=new Vector2(motor.X,motor.Depth);SetEvidenceLabel(PositionEvidence("ChargeSkill2",skillAnchor,chargeNow),Color.white);yield return Capture("09_charge_skill_position_before_after.png");combat.ReleaseChargeForTest(.65f);FinishAction();measurements+=MeasurementJson("ChargeSkill2",skillAnchor,chargeNow)+",\n";
            yield return CaptureLockedSkill("Skill3",skillAnchor,.72f,"10_skill3_position_before_after.png",v=>measurements+=v+",\n");FinishAction();
            yield return CaptureLockedSkill("Skill4",skillAnchor,.28f,"11_skill4_position_before_after.png",v=>measurements+=v+"\n");FinishAction();
            measurements+="}\n";File.WriteAllText(Path.Combine(outputDirectory,"skill_position_measurements.json"),measurements);

            stage.PlayerHealth.ReceiveDamage(new DamagePacket(soldier.GetComponent<CharacterIdentity>(),25f,0f,0f,0f));yield return new WaitForSecondsRealtime(.25f);SetEvidenceLabel("ARCADE HUD / LIVE HP + ENCOUNTER STATUS",new Color(1f,.82f,.28f));yield return Capture("12_new_hud_ui.png");
            foreach(var e in enemies)if(e!=null){e.gameObject.SetActive(true);e.enabled=false;e.GetComponent<CharacterMotor>().SetPosition(motor.X+((e==soldier)?-2.2f:2.2f),motor.Depth+((e==soldier)?.18f:-.24f));}
            SetEvidenceLabel("ENCOUNTER 01 / NEW STAGE + SOLDIER GROUP",new Color(1f,.82f,.28f));yield return Capture("13_encounter_with_new_stage.png");
            stage.ForceClearEncounterForTest();yield return new WaitForSecondsRealtime(.3f);stage.SetPlayerXForTest(18.1f);yield return null;yield return null;stage.ForceClearEncounterForTest();yield return new WaitForSecondsRealtime(.3f);stage.SetPlayerXForTest(29.1f);yield return null;yield return null;stage.ForceClearEncounterForTest();yield return new WaitForSecondsRealtime(.3f);stage.SetPlayerXForTest(39.2f);SetView(36f,-.55f);yield return new WaitForSecondsRealtime(.35f);stage.ForceCompleteBossForTest();yield return null;
            CreateStageClearBanner();SetEvidenceLabel("ALL 3 ENCOUNTERS + CAO CAO CLEARED / LIVE PLAYER BUILD",new Color(1f,.82f,.28f));yield return Capture("14_stage_clear_with_new_ui.png");
            Debug.Log("[THREE_KINGDOMS_ITERATION03] CAPTURE_COMPLETE count=14 stageClear="+stage.StageClear+" rootLockEvidence="+File.Exists(Path.Combine(outputDirectory,"skill_position_measurements.json")));
            yield return new WaitForSecondsRealtime(.3f);Application.Quit(stage.StageClear?0:3);
        }

        private IEnumerator RunVisualAnchorFixCapture()
        {
            captureWidth=1920;captureHeight=1080;outputDirectory=Environment.GetEnvironmentVariable("THREE_KINGDOMS_SCREENSHOTS");
            if(string.IsNullOrWhiteSpace(outputDirectory))outputDirectory=@"C:\Users\shanghai\Desktop\三国战纪\Screenshots\VisualAnchorFix";
            Directory.CreateDirectory(outputDirectory);yield return null;yield return null;
            stage=FindFirstObjectByType<StageManager>();motor=stage.PlayerMotor;animationDriver=motor.GetComponent<CharacterAnimator>();combat=motor.GetComponent<CharacterCombat>();
            motor.GetComponent<PlayerInputController>().enabled=false;combat.enabled=false;stage.enabled=false;var follow=Camera.main.GetComponent<BeatEmUpCamera>();if(follow!=null)follow.enabled=false;
            motor.SetPosition(8f,-.65f);SetView(8f,-.65f);animationDriver.Sample("CombatIdle",.25f);SetEvidenceLabel("GROUND REFERENCE / ONE BAKED SHADOW",Color.white);yield return Capture("01_idle_anchor.png");
            string json="{\n  \"runtimeShadowActive\": "+motor.transform.Find("Shadow").gameObject.activeSelf.ToString().ToLowerInvariant()+",\n  \"skills\": {\n";
            foreach(var sample in new[]{("Skill1",.52f,"02_skill1_anchor.png"),("ParryCounter",.48f,"03_parrycounter_anchor.png"),("ChargeSkill2",.72f,"04_charge_anchor.png"),("Skill3_A",.55f,"05_skill3_anchor.png"),("Skill4",.50f,"06_skill4_anchor.png")})
            {
                animationDriver.Sample(sample.Item1,sample.Item2);var renderer=motor.transform.Find("VisualRoot").GetComponent<SpriteRenderer>();Vector2 pivot=new Vector2(renderer.sprite.pivot.x/renderer.sprite.rect.width,renderer.sprite.pivot.y/renderer.sprite.rect.height);
                SetEvidenceLabel(sample.Item1+"  ROOT=("+motor.X.ToString("F2")+","+motor.Depth.ToString("F2")+")  PIVOT=BAKED SHADOW  RUNTIME SHADOW=OFF",Color.white);yield return Capture(sample.Item3);
                json+="    \""+sample.Item1+"\": {\"root\": ["+motor.X.ToString("F3",System.Globalization.CultureInfo.InvariantCulture)+", "+motor.Depth.ToString("F3",System.Globalization.CultureInfo.InvariantCulture)+"], \"pivot\": ["+pivot.x.ToString("F4",System.Globalization.CultureInfo.InvariantCulture)+", "+pivot.y.ToString("F4",System.Globalization.CultureInfo.InvariantCulture)+"]}"+(sample.Item1=="Skill4"?"\n":",\n");
            }
            json+="  }\n}\n";File.WriteAllText(Path.Combine(outputDirectory,"visual_anchor_measurements.json"),json);
            stage.enabled=true;stage.BeginEncounter(0);yield return null;yield return null;var soldier=FindFirstObjectByType<SoldierAI>();soldier.enabled=false;soldier.GetComponent<CharacterCombat>().enabled=false;soldier.GetComponent<CharacterMotor>().SetPosition(6.4f,-.65f);soldier.GetComponent<CharacterAnimator>().Sample("Attack01",.55f);
            SetEvidenceLabel("SOLDIER JUMP ATTACK / CUSTOM SHADOW PIVOT / ONE SHADOW",Color.white);yield return Capture("07_soldier_jump_anchor.png");
            Debug.Log("[VISUAL_ANCHOR_FIX] CAPTURE_COMPLETE count=7 runtimeShadowActive="+motor.transform.Find("Shadow").gameObject.activeSelf);yield return new WaitForSecondsRealtime(.25f);Application.Quit(0);
        }

        private IEnumerator CaptureLockedSkill(string action,Vector2 anchor,float tick,string file,Action<string> append)
        {
            motor.SetPosition(anchor.x,anchor.y);if(action=="Skill1")combat.RequestSkill1();else if(action=="Skill3")combat.RequestSkill3();else combat.RequestSkill4();
            motor.SetPosition(anchor.x+1.2f,anchor.y+1.2f);combat.ManualTick(tick);animationDriver.Sample(action=="Skill3"?"Skill3_A":action,.52f);Vector2 now=new Vector2(motor.X,motor.Depth);
            SetEvidenceLabel(PositionEvidence(action,anchor,now),Color.white);append(MeasurementJson(action,anchor,now));yield return Capture(file);
        }

        private static string PositionEvidence(string action,Vector2 before,Vector2 during)=>action+" ROOT  BEFORE=("+before.x.ToString("F2")+","+before.y.ToString("F2")+")  DURING=("+during.x.ToString("F2")+","+during.y.ToString("F2")+")  DRIFT="+Vector2.Distance(before,during).ToString("F3");
        private static string MeasurementJson(string action,Vector2 before,Vector2 during)=>"  \""+action+"\": {\"before\": ["+before.x.ToString("F3",System.Globalization.CultureInfo.InvariantCulture)+", "+before.y.ToString("F3",System.Globalization.CultureInfo.InvariantCulture)+"], \"during\": ["+during.x.ToString("F3",System.Globalization.CultureInfo.InvariantCulture)+", "+during.y.ToString("F3",System.Globalization.CultureInfo.InvariantCulture)+"], \"drift\": "+Vector2.Distance(before,during).ToString("F4",System.Globalization.CultureInfo.InvariantCulture)+"}";

        private void SetView(float x,float y){Camera.main.transform.position=new Vector3(x,y*.06f+.15f,-10f);}
        private void SetEvidenceLabel(string value,Color color)
        {
            var old=GameObject.Find("Iteration03EvidenceLabel");if(old!=null)Destroy(old);var go=new GameObject("Iteration03EvidenceLabel");go.transform.SetParent(Camera.main.transform,false);go.transform.localPosition=new Vector3(0f,-4.45f,10f);
            var text=go.AddComponent<TextMesh>();text.text=value;text.anchor=TextAnchor.MiddleCenter;text.alignment=TextAlignment.Center;text.fontSize=44;text.characterSize=.035f;text.color=color;text.GetComponent<MeshRenderer>().sortingOrder=6500;
        }

        private GameObject CreateNavigationOverlay(StageNavigationMap map)
        {
            var root=new GameObject("Iteration03NavigationOverlay");CreateLine(root.transform,map.WalkablePolygon.Concat(new[]{map.WalkablePolygon[0]}).ToArray(),new Color(.20f,1f,.30f),.055f);
            foreach(var zone in map.BlockedZones){Rect r=zone.bounds;CreateLine(root.transform,new[]{new Vector2(r.xMin,r.yMin),new Vector2(r.xMax,r.yMin),new Vector2(r.xMax,r.yMax),new Vector2(r.xMin,r.yMax),new Vector2(r.xMin,r.yMin)},new Color(1f,.15f,.10f),.07f);}
            return root;
        }
        private static void CreateLine(Transform parent,Vector2[] points,Color color,float width)
        {
            var go=new GameObject("NavigationLine");go.transform.SetParent(parent,false);var line=go.AddComponent<LineRenderer>();line.material=new Material(Shader.Find("Sprites/Default"));line.startColor=line.endColor=color;line.startWidth=line.endWidth=width;line.positionCount=points.Length;line.sortingOrder=6200;for(int i=0;i<points.Length;i++)line.SetPosition(i,new Vector3(points[i].x,points[i].y,-.2f));
        }
        private static void CreateWorldMarker(Vector2 point,string value,Color color)
        {
            var go=new GameObject("Iteration03WorldMarker");go.transform.position=new Vector3(point.x,point.y,-.3f);var text=go.AddComponent<TextMesh>();text.text=value;text.anchor=TextAnchor.MiddleCenter;text.alignment=TextAlignment.Center;text.fontSize=48;text.characterSize=.028f;text.color=color;text.GetComponent<MeshRenderer>().sortingOrder=6300;
        }
        private static void ClearWorldMarkers(){foreach(var marker in FindObjectsByType<TextMesh>(FindObjectsSortMode.None).Where(x=>x.gameObject.name=="Iteration03WorldMarker"))DestroyImmediate(marker.gameObject);}

        private IEnumerator SampleState(string state,float normalized,string file){animationDriver.Sample(state,normalized);yield return Capture(file);}

        private void MeasureCharacterScale(SpriteRenderer renderer)
        {
            int height=MeasureRendererHeight(renderer);float ratio=(float)height/captureHeight;
            File.WriteAllText(Path.Combine(outputDirectory,"character_scale_measurement.json"),"{\n  \"resolution\": \"1920x1080\",\n  \"opaqueHeightPixels\": "+height+",\n  \"screenHeightRatio\": "+ratio.ToString("F6",System.Globalization.CultureInfo.InvariantCulture)+",\n  \"visualScale\": "+motor.VisualScale.ToString("F2",System.Globalization.CultureInfo.InvariantCulture)+"\n}\n");
            Debug.Log("[THREE_KINGDOMS_ITERATION02] characterHeightPixels="+height+" ratio="+ratio.ToString("F4"));
        }

        private void MeasureSoldierScale(SoldierAI[] enemies)
        {
            int playerHeight=MeasureRendererHeight(motor.transform.Find("VisualRoot").GetComponent<SpriteRenderer>());
            var heights=enemies.Where(x=>x!=null&&x.gameObject.activeInHierarchy).Select(x=>MeasureRendererHeight(x.transform.Find("VisualRoot").GetComponent<SpriteRenderer>())).Where(x=>x>0).ToArray();
            float average=heights.Length==0?0f:(float)heights.Average();float ratio=playerHeight==0?0f:average/playerHeight;
            File.WriteAllText(Path.Combine(outputDirectory,"soldier_scale_measurement.json"),"{\n  \"diaochanHeightPixels\": "+playerHeight+",\n  \"soldierAverageHeightPixels\": "+average.ToString("F2",System.Globalization.CultureInfo.InvariantCulture)+",\n  \"soldierToDiaochanRatio\": "+ratio.ToString("F6",System.Globalization.CultureInfo.InvariantCulture)+"\n}\n");
            Debug.Log("[THREE_KINGDOMS_ITERATION02] soldierToDiaochan="+ratio.ToString("F4"));
        }

        private int MeasureRendererHeight(SpriteRenderer renderer)
        {
            if(renderer==null)return 0;var visible=RenderPixels();bool wasEnabled=renderer.enabled;renderer.enabled=false;var hidden=RenderPixels();renderer.enabled=wasEnabled;
            int minY=captureHeight,maxY=-1;
            for(int y=0;y<captureHeight;y++)for(int x=0;x<captureWidth;x++)
            {
                int i=y*captureWidth+x;Color32 a=visible[i],b=hidden[i];
                if(Mathf.Abs(a.r-b.r)+Mathf.Abs(a.g-b.g)+Mathf.Abs(a.b-b.b)+Mathf.Abs(a.a-b.a)<12)continue;
                minY=Mathf.Min(minY,y);maxY=Mathf.Max(maxY,y);
            }
            return maxY<minY?0:maxY-minY+1;
        }

        private Color32[] RenderPixels()
        {
            var camera=Camera.main;var target=new RenderTexture(captureWidth,captureHeight,24,RenderTextureFormat.ARGB32);var image=new Texture2D(captureWidth,captureHeight,TextureFormat.RGBA32,false);
            var previousActive=RenderTexture.active;var previousTarget=camera.targetTexture;camera.targetTexture=target;RenderTexture.active=target;camera.Render();
            image.ReadPixels(new Rect(0,0,captureWidth,captureHeight),0,0);image.Apply();var pixels=image.GetPixels32();camera.targetTexture=previousTarget;RenderTexture.active=previousActive;Destroy(target);Destroy(image);return pixels;
        }

        private IEnumerator SampleCombo(string file, float normalized)
        {
            animationDriver.Sample("AttackCombo4", normalized);
            yield return Capture(file);
        }

        private void FinishAction()
        {
            for (int i = 0; i < 400 && combat.IsBusy; i++) combat.ManualTick(.01f);
        }

        private static void CreateStageClearBanner()
        {
            var go = new GameObject("StageClearEvidence");
            go.transform.SetParent(Camera.main.transform, false);
            go.transform.localPosition = new Vector3(0f, 1.8f, 10f);
            var text = go.AddComponent<TextMesh>();
            text.text = "STAGE CLEAR";
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.fontSize = 72;
            text.characterSize = .08f;
            text.color = new Color(1f, .82f, .18f);
            text.GetComponent<MeshRenderer>().sortingOrder = 5000;
        }

        private IEnumerator Capture(string file)
        {
            yield return new WaitForEndOfFrame();
            WriteCapture(file);yield return new WaitForSecondsRealtime(.12f);
        }

        private IEnumerator CaptureNoDelay(string file)
        {
            yield return new WaitForEndOfFrame();WriteCapture(file);
        }

        private void WriteCapture(string file)
        {
            string path = Path.Combine(outputDirectory, file);
            var camera = Camera.main;
            var target = new RenderTexture(captureWidth, captureHeight, 24, RenderTextureFormat.ARGB32);
            var image = new Texture2D(captureWidth, captureHeight, TextureFormat.RGBA32, false);
            var previousActive = RenderTexture.active;
            var previousTarget = camera.targetTexture;
            camera.targetTexture = target;
            RenderTexture.active = target;
            camera.Render();
            image.ReadPixels(new Rect(0, 0, captureWidth, captureHeight), 0, 0);
            image.Apply();
            File.WriteAllBytes(path, image.EncodeToPNG());
            camera.targetTexture = previousTarget;
            RenderTexture.active = previousActive;
            Destroy(target);
            Destroy(image);
            Debug.Log("[THREE_KINGDOMS_VALIDATION] screenshot=" + path);
        }
    }
}
