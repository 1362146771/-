using System.Collections;
using System.Linq;
using NUnit.Framework;
using ThreeKingdoms;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ThreeKingdoms.Tests.PlayMode
{
    public sealed class NpcReplacementPlayModeTests
    {
        private static readonly float[] AttackFootX={168f,168f,168f,168.5f,170f,170f,170f,168f,166f,161.5f,160.5f,162.5f,162.5f,163.5f,163.5f,163.5f,163.5f,163.5f,173f,173f};
        private static readonly float[] AttackFootYFromBottom={39f,40f,41f,41f,42f,42f,42f,41f,40f,40f,39f,39f,38f,38f,37f,36f,35f,34f,34f,34f};
        private static IEnumerator Spawn(System.Action<StageManager,SoldierAI> ready)
        {
            yield return SceneManager.LoadSceneAsync("SC_Stage01_AncientStreet",LoadSceneMode.Single);yield return null;yield return null;
            StageManager stage=Object.FindFirstObjectByType<StageManager>();stage.BeginEncounter(0);yield return null;yield return null;
            SoldierAI soldier=Object.FindObjectsByType<SoldierAI>(FindObjectsSortMode.None).First();soldier.enabled=false;ready(stage,soldier);
        }

        [UnityTest] public IEnumerator NpcReplacement_01_SpawnedSoldierUsesNewIdleAndSingleShadow()
        {
            yield return Spawn((stage,soldier)=>
            {
                var animation=soldier.GetComponent<CharacterAnimator>();var renderer=soldier.transform.Find("VisualRoot").GetComponent<SpriteRenderer>();animation.Sample("Idle",0f);Sprite sprite=renderer.sprite;
                Assert.That(sprite.name,Does.StartWith("NSOL_Walk_"));Assert.That(soldier.transform.Find("Shadow").gameObject.activeSelf,Is.True);
                animation.Face(1);Assert.That(renderer.flipX,Is.True,"left-facing source must flip when moving right");animation.Face(-1);Assert.That(renderer.flipX,Is.False);
            });
        }

        [UnityTest] public IEnumerator NpcReplacement_02_SoldierPlaysHurtThenBloodlessDeath()
        {
            StageManager stage=null;SoldierAI soldier=null;yield return Spawn((s,e)=>{stage=s;soldier=e;});var health=soldier.GetComponent<CharacterHealth>();var animation=soldier.GetComponent<CharacterAnimator>();var motor=soldier.GetComponent<CharacterMotor>();var renderer=soldier.transform.Find("VisualRoot").GetComponent<SpriteRenderer>();Vector2 before=new Vector2(motor.X,motor.Depth);
            animation.Face(1);health.ReceiveDamage(new DamagePacket(stage.PlayerMotor.GetComponent<CharacterIdentity>(),1f,1.2f,.3f,0f));Assert.AreEqual("HitReact",animation.CurrentState);Assert.That(renderer.enabled,Is.True,"an authored reaction must not be hidden by the placeholder flash");Assert.That(Vector2.Distance(before,new Vector2(motor.X,motor.Depth)),Is.LessThan(.01f),"hurt must not add visible root displacement (sub-pixel navigation projection is allowed)");yield return null;
            Assert.That(renderer.sprite.name,Does.StartWith("NSOL_HitBlood_"));Assert.That(renderer.flipX,Is.False,"right-facing HitReact source must stay unflipped when soldier faces right");animation.Face(-1);yield return null;Assert.That(renderer.flipX,Is.True,"right-facing HitReact source must flip when soldier faces left");yield return new WaitForSeconds(.34f);
            health.KillForTest();Assert.AreEqual("Death",animation.CurrentState);yield return null;Assert.That(soldier.GetComponent<SoldierReactionController>(),Is.Not.Null);
        }

        [UnityTest] public IEnumerator NpcReplacement_03_OnlyCombinedTwoStageSlashIsAvailable()
        {
            StageManager stage=null;SoldierAI soldier=null;yield return Spawn((s,e)=>{stage=s;soldier=e;});var combat=soldier.GetComponent<CharacterCombat>();combat.enabled=false;soldier.SetTarget(stage.PlayerMotor.transform);
            soldier.GetComponent<CharacterMotor>().SetPosition(6f,-.7f);stage.PlayerMotor.SetPosition(7.2f,-.7f);
            Assert.That(soldier.BeginAttackForTest("Attack01"),Is.True);AttackData attack=soldier.AttackDataForTest("Attack01");Assert.AreEqual("Attack01",soldier.SelectedAttackId);
            foreach(string removed in new[]{"Attack02","RunAttack","JumpAttack"}){Assert.That(soldier.AttackDataForTest(removed),Is.Null);Assert.That(soldier.BeginAttackForTest(removed),Is.False);}
            soldier.Tick(attack.Duration+.01f);Assert.AreEqual(SoldierAI.State.Cooldown,soldier.CurrentState);
        }

        [UnityTest] public IEnumerator NpcReplacement_04_WaveEntersFromWideBackWallPositions()
        {
            yield return SceneManager.LoadSceneAsync("SC_Stage01_AncientStreet",LoadSceneMode.Single);yield return null;yield return null;
            StageManager stage=Object.FindFirstObjectByType<StageManager>();stage.BeginEncounter(0);yield return null;
            SoldierAI[] soldiers=Object.FindObjectsByType<SoldierAI>(FindObjectsSortMode.None).OrderBy(item=>item.transform.position.x).ToArray();
            Assert.That(soldiers.Length,Is.EqualTo(2));
            Assert.That(soldiers[1].transform.position.x-soldiers[0].transform.position.x,Is.GreaterThanOrEqualTo(8.4f),"wave should be spread across both wall entrances");
            foreach(SoldierAI soldier in soldiers)Assert.That(soldier.transform.position.y,Is.InRange(-.55f,-.20f),"navigation may project the requested -0.30 wall depth onto the nearest valid back-wall edge");
        }

        [UnityTest] public IEnumerator NpcReplacement_05_TwoStageSlashStaysOnShadowForEveryFrame()
        {
            StageManager stage=null;SoldierAI soldier=null;yield return Spawn((s,e)=>{stage=s;soldier=e;});soldier.enabled=false;CharacterCombat combat=soldier.GetComponent<CharacterCombat>();combat.enabled=false;CharacterMotor motor=soldier.GetComponent<CharacterMotor>();ActionRunner runner=soldier.GetComponent<ActionRunner>();Transform visual=soldier.transform.Find("VisualRoot"),shadow=soldier.transform.Find("Shadow");
            motor.SetPosition(6f,-.6f);stage.PlayerMotor.SetPosition(7.2f,-.6f);Vector2 root=new Vector2(motor.X,motor.Depth);Assert.That(soldier.BeginAttackForTest("Attack01"),Is.True);Assert.That(runner.IsPlaying,Is.True,"Soldier must use the authored ActionRunner path");ActionDefinition action=runner.Current;Assert.That(action.lockGroundPosition,Is.True);Assert.That(action.frameVisualOffsets.Count,Is.EqualTo(action.frameCount),"Battle Editor Frame Pose data must remain in the action instead of being baked into PNG pivots");
            for(int i=0;i<action.frameCount;i++)
            {
                combat.ManualTick(i==0?0f:1f/action.framesPerSecond+.0001f);Assert.That(new Vector2(motor.X,motor.Depth),Is.EqualTo(root),$"gameplay root moved on frame {i}");Vector2 authored=action.VisualOffsetAt(runner.CurrentFrame);Vector2 expected=new Vector2(authored.x*animationMirror(soldier),authored.y);Assert.That(Vector2.Distance(new Vector2(visual.localPosition.x,visual.localPosition.y),expected),Is.LessThan(.001f),$"Game did not apply Battle Editor Frame Pose on frame {i}");Assert.That(shadow.localPosition,Is.EqualTo(Vector3.zero),$"shadow moved locally on frame {i}");Sprite runtime=visual.GetComponent<SpriteRenderer>().sprite;Assert.That(runtime,Is.SameAs(action.VisualFrameAt(runner.CurrentFrame)),$"runtime Sprite does not match Battle Editor frame {i}");Vector2 pivot=new Vector2(runtime.pivot.x/runtime.rect.width,runtime.pivot.y/runtime.rect.height);Assert.That(Vector2.Distance(pivot,new Vector2(.5833333f,.25165564f)),Is.LessThan(.001f),$"shared attack PNG pivot was destructively rewritten on frame {i}");AssertFootPixelsOnShadow(soldier,runtime,runner.CurrentFrame,visual.localPosition);
            }
        }

        [UnityTest] public IEnumerator NpcReplacement_06_AutomaticAiAttackMatchesBattleEditorAfterAnimatorEvaluation()
        {
            yield return SceneManager.LoadSceneAsync("SC_Stage01_AncientStreet",LoadSceneMode.Single);yield return null;yield return null;
            StageManager stage=Object.FindFirstObjectByType<StageManager>();stage.BeginEncounter(0);yield return null;yield return null;
            SoldierAI[] soldiers=Object.FindObjectsByType<SoldierAI>(FindObjectsSortMode.None);Assert.That(soldiers.Length,Is.GreaterThan(0));
            SoldierAI soldier=soldiers[0];for(int i=1;i<soldiers.Length;i++)soldiers[i].gameObject.SetActive(false);
            CharacterMotor motor=soldier.GetComponent<CharacterMotor>();ActionRunner runner=soldier.GetComponent<ActionRunner>();SpriteRenderer renderer=soldier.transform.Find("VisualRoot").GetComponent<SpriteRenderer>();
            motor.SetPosition(6f,-.8f);stage.PlayerMotor.SetPosition(7.2f,-.8f);soldier.SetTarget(stage.PlayerMotor.transform);
            yield return new WaitForSeconds(.75f);
            float timeout=2f;while(timeout>0f&&!runner.IsPlaying){timeout-=Time.deltaTime;yield return null;}
            Assert.That(runner.IsPlaying,Is.True,"real SoldierAI did not enter the authored attack path");Vector2 root=new Vector2(motor.X,motor.Depth);ActionDefinition action=runner.Current;
            int observed=-1;timeout=action.Duration+.5f;
            while(timeout>0f&&runner.IsPlaying)
            {
                // Resuming on the following frame observes the previous frame after LateUpdate;
                // WaitForEndOfFrame is not dispatched by Unity's batchmode test runner.
                timeout-=Time.deltaTime;yield return null;
                if(!runner.IsPlaying)break;observed=Mathf.Max(observed,runner.CurrentFrame);
                Assert.That(Vector2.Distance(new Vector2(motor.X,motor.Depth),root),Is.LessThan(.001f),$"automatic AI root moved on frame {runner.CurrentFrame}");
                Vector2 authored=action.VisualOffsetAt(runner.CurrentFrame),expected=new Vector2(authored.x*animationMirror(soldier),authored.y);Vector3 actual=soldier.transform.Find("VisualRoot").localPosition;Assert.That(Vector2.Distance(new Vector2(actual.x,actual.y),expected),Is.LessThan(.001f),$"automatic AI did not apply Battle Editor Frame Pose on frame {runner.CurrentFrame}");
                Assert.That(renderer.sprite,Is.SameAs(action.VisualFrameAt(runner.CurrentFrame)),$"Animator replaced Battle Editor sprite after Update on frame {runner.CurrentFrame}");
            }
            Assert.That(observed,Is.GreaterThanOrEqualTo(action.frameCount-2),"automatic attack did not traverse the full authored action");
        }

        private static int animationMirror(SoldierAI soldier)=>soldier.GetComponent<CharacterAnimator>().VisualMirrorSign;
        private static void AssertFootPixelsOnShadow(SoldierAI soldier,Sprite sprite,int frame,Vector3 visualLocal)
        {
            float scale=soldier.GetComponent<CharacterMotor>().VisualScale,sign=animationMirror(soldier);int index=Mathf.Clamp(frame,0,AttackFootX.Length-1);
            float footX=visualLocal.x+sign*(AttackFootX[index]-sprite.pivot.x)/sprite.pixelsPerUnit*scale;
            float footY=visualLocal.y+(AttackFootYFromBottom[index]-sprite.pivot.y)/sprite.pixelsPerUnit*scale;
            Assert.That(Mathf.Abs(footX),Is.LessThan(.035f),$"rendered foot pixels left the shadow horizontally on frame {frame}: {footX:F3}");
            Assert.That(Mathf.Abs(footY),Is.LessThan(.035f),$"rendered foot pixels left the ground on frame {frame}: {footY:F3}");
        }
    }
}
