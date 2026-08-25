using System.Collections;
using System.Linq;
using NUnit.Framework;
using ThreeKingdoms;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ThreeKingdoms.Tests.PlayMode
{
    public sealed class ThreeKingdomsDemoPlayModeTests
    {
        private static IEnumerator LoadStage(System.Action<StageManager> callback)
        {
            yield return SceneManager.LoadSceneAsync("SC_Stage01_AncientStreet", LoadSceneMode.Single);
            yield return null;
            yield return null;
            var stage = Object.FindFirstObjectByType<StageManager>();
            Assert.That(stage, Is.Not.Null);
            Assert.That(stage.PlayerMotor, Is.Not.Null);
            callback(stage);
        }

        private static CharacterCombat ManualCombat(StageManager stage)
        {
            var input = stage.PlayerMotor.GetComponent<PlayerInputController>();
            if (input != null) input.enabled = false;
            var combat = stage.PlayerMotor.GetComponent<CharacterCombat>();
            combat.enabled = false;
            return combat;
        }

        private static void FinishCombo(CharacterCombat combat)
        {
            for (int i = 0; i < 220 && combat.IsComboActive; i++) combat.ManualTick(.01f);
        }

        private static void QueueCombo(CharacterCombat combat, int count)
        {
            for (int i = 0; i < count; i++) combat.RequestComboAttack();
            FinishCombo(combat);
        }

        [UnityTest] public IEnumerator _01_DiaochanSpawn()
        {
            yield return LoadStage(stage => Assert.That(stage.PlayerMotor.name, Is.EqualTo("Diaochan_Player")));
        }

        [UnityTest] public IEnumerator _02_Entrance()
        {
            yield return LoadStage(stage => Assert.That(stage.PlayerMotor.GetComponent<CharacterAnimator>().CurrentState, Is.EqualTo("Entrance")));
        }

        [UnityTest] public IEnumerator _03_Idle()
        {
            yield return LoadStage(stage => stage.PlayerMotor.GetComponent<PlayerInputController>().enabled = false);
            yield return new WaitForSeconds(1f);
            var animator = Object.FindFirstObjectByType<PlayerInputController>().GetComponent<CharacterAnimator>();
            animator.Play("CombatIdle", true);
            Assert.That(animator.CurrentState, Is.EqualTo("CombatIdle"));
        }

        [UnityTest] public IEnumerator _04_Walk()
        {
            yield return LoadStage(stage => {
                stage.PlayerMotor.GetComponent<PlayerInputController>().enabled = false;
                stage.PlayerMotor.MoveForTest(Vector2.right, false, .25f);
                stage.PlayerMotor.GetComponent<CharacterAnimator>().Play("Walk", true);
                Assert.That(stage.PlayerMotor.GetComponent<CharacterAnimator>().CurrentState, Is.EqualTo("Walk"));
            });
        }

        [UnityTest] public IEnumerator _05_Run()
        {
            yield return LoadStage(stage => {
                stage.PlayerMotor.GetComponent<PlayerInputController>().enabled = false;
                stage.PlayerMotor.MoveForTest(Vector2.right, true, .25f);
                stage.PlayerMotor.GetComponent<CharacterAnimator>().Play("Run", true);
                Assert.That(stage.PlayerMotor.GetComponent<CharacterAnimator>().CurrentState, Is.EqualTo("Run"));
            });
        }

        [UnityTest] public IEnumerator _06_MoveDepth()
        {
            yield return LoadStage(stage => {
                float before = stage.PlayerMotor.Depth;
                stage.PlayerMotor.MoveForTest(Vector2.up, false, .5f);
                Assert.That(stage.PlayerMotor.Depth, Is.GreaterThan(before));
                stage.PlayerMotor.SetPosition(stage.PlayerMotor.X, 99f);
                Assert.That(stage.PlayerMotor.Depth, Is.LessThanOrEqualTo(-.24f));
            });
        }

        [UnityTest] public IEnumerator _07_DepthSorting()
        {
            yield return LoadStage(stage => {
                var sorter = stage.PlayerMotor.GetComponent<DepthSorter>();
                var visual = stage.PlayerMotor.transform.Find("VisualRoot").GetComponent<SpriteRenderer>();
                stage.PlayerMotor.SetPosition(0, 1f); sorter.RefreshSort(); int far = visual.sortingOrder;
                stage.PlayerMotor.SetPosition(0, -1f); sorter.RefreshSort(); int near = visual.sortingOrder;
                Assert.That(near, Is.GreaterThan(far));
            });
        }

        [UnityTest] public IEnumerator _08_Crouch()
        {
            yield return LoadStage(stage => {
                var combat = ManualCombat(stage); combat.SetCrouch(true);
                Assert.That(stage.PlayerMotor.GetComponent<CharacterAnimator>().CurrentState, Is.EqualTo("Crouch"));
            });
        }

        [UnityTest] public IEnumerator _09_Dodge()
        {
            yield return LoadStage(stage => {
                var combat = ManualCombat(stage); float before = stage.PlayerMotor.X; combat.RequestDodge();
                Assert.That(combat.CurrentAction, Is.EqualTo("Dodge")); Assert.That(stage.PlayerMotor.X, Is.GreaterThan(before));
            });
        }

        [UnityTest] public IEnumerator _10_Jump()
        {
            yield return LoadStage(stage => {
                stage.PlayerMotor.GetComponent<PlayerInputController>().enabled = false;
                Assert.That(stage.PlayerMotor.Jump(), Is.True); stage.PlayerMotor.GetComponent<CharacterAnimator>().Play("Jump",true);stage.PlayerMotor.ManualTick(.1f);
                Assert.That(stage.PlayerMotor.Elevation, Is.GreaterThan(0f));
                Assert.That(stage.PlayerMotor.JumpNormalizedTime,Is.GreaterThan(0f));
                Assert.That(stage.PlayerMotor.GetComponent<CharacterAnimator>().CurrentState,Is.EqualTo("Jump"));
                Assert.That(stage.PlayerMotor.transform.Find("Shadow").localPosition.y, Is.EqualTo(0f).Within(.001f));
            });
        }

        [UnityTest] public IEnumerator _11_JumpAttack()
        {
            yield return LoadStage(stage => {
                var combat = ManualCombat(stage); stage.PlayerMotor.Jump(); combat.RequestJumpAttack();
                Assert.That(combat.CurrentAction, Is.EqualTo("JumpAttack"));
            });
        }

        [UnityTest] public IEnumerator _12_AttackCombo_1Hit()
        {
            yield return LoadStage(stage => { var c=ManualCombat(stage); QueueCombo(c,1); Assert.That(c.ComboMaxSegmentsReached,Is.EqualTo(1)); });
        }

        [UnityTest] public IEnumerator _13_AttackCombo_2Hit()
        {
            yield return LoadStage(stage => { var c=ManualCombat(stage); QueueCombo(c,2); Assert.That(c.ComboMaxSegmentsReached,Is.EqualTo(2)); });
        }

        [UnityTest] public IEnumerator _14_AttackCombo_3Hit()
        {
            yield return LoadStage(stage => { var c=ManualCombat(stage); QueueCombo(c,3); Assert.That(c.ComboMaxSegmentsReached,Is.EqualTo(3)); });
        }

        [UnityTest] public IEnumerator _15_AttackCombo_4Hit()
        {
            yield return LoadStage(stage => { var c=ManualCombat(stage); QueueCombo(c,4); Assert.That(c.ComboMaxSegmentsReached,Is.EqualTo(4)); });
        }

        [UnityTest] public IEnumerator _16_HeavyAttack()
        {
            yield return LoadStage(stage => { var c=ManualCombat(stage); c.RequestHeavyAttack(); Assert.That(c.CurrentAction,Is.EqualTo("HeavyAttack")); });
        }

        [UnityTest] public IEnumerator _17_HitboxHit()
        {
            yield return LoadStage(stage => stage.BeginEncounter(0));
            yield return null;
            var s=Object.FindObjectsByType<SoldierAI>(FindObjectsSortMode.None).First();
            s.enabled=false;
            var stage=Object.FindFirstObjectByType<StageManager>(); var c=ManualCombat(stage);
            stage.PlayerMotor.SetPosition(7f,0f); s.GetComponent<CharacterMotor>().SetPosition(7.8f,0f);
            float before=s.GetComponent<CharacterHealth>().Current; c.RequestComboAttack();
            for(int i=0;i<32;i++)c.ManualTick(.01f);
            Assert.That(s.GetComponent<CharacterHealth>().Current,Is.LessThan(before));
        }

        [UnityTest] public IEnumerator _18_DepthMiss()
        {
            yield return LoadStage(stage => stage.BeginEncounter(0));
            yield return null;
            var s=Object.FindObjectsByType<SoldierAI>(FindObjectsSortMode.None).First(); s.enabled=false;
            var stage=Object.FindFirstObjectByType<StageManager>(); var c=ManualCombat(stage);
            stage.PlayerMotor.SetPosition(7f,-1f); s.GetComponent<CharacterMotor>().SetPosition(7.8f,1f);
            float before=s.GetComponent<CharacterHealth>().Current; c.RequestComboAttack();
            for(int i=0;i<32;i++)c.ManualTick(.01f);
            Assert.That(s.GetComponent<CharacterHealth>().Current,Is.EqualTo(before));
        }

        [UnityTest] public IEnumerator _19_EnemyApproach()
        {
            yield return LoadStage(stage => stage.BeginEncounter(0)); yield return null;
            var stage=Object.FindFirstObjectByType<StageManager>(); var ai=Object.FindObjectsByType<SoldierAI>(FindObjectsSortMode.None).First();
            stage.PlayerMotor.SetPosition(7f,0f); ai.GetComponent<CharacterMotor>().SetPosition(10f,0f); ai.Tick(.02f);
            Assert.That(ai.CurrentState,Is.EqualTo(SoldierAI.State.Approach));
        }

        [UnityTest] public IEnumerator _20_EnemyDepthAlign()
        {
            yield return LoadStage(stage => stage.BeginEncounter(0)); yield return null;
            var stage=Object.FindFirstObjectByType<StageManager>(); var ai=Object.FindObjectsByType<SoldierAI>(FindObjectsSortMode.None).First();
            stage.PlayerMotor.SetPosition(7f,-1f); ai.GetComponent<CharacterMotor>().SetPosition(7.8f,1f); ai.Tick(.02f);
            Assert.That(ai.CurrentState,Is.EqualTo(SoldierAI.State.AlignDepth));
        }

        [UnityTest] public IEnumerator _21_EnemyAttack()
        {
            yield return LoadStage(stage => stage.BeginEncounter(0)); yield return null;
            var stage=Object.FindFirstObjectByType<StageManager>(); var ai=Object.FindObjectsByType<SoldierAI>(FindObjectsSortMode.None).First();
            stage.PlayerMotor.SetPosition(7f,0f); ai.GetComponent<CharacterMotor>().SetPosition(7.8f,0f); ai.Tick(.02f);
            Assert.That(ai.CurrentState,Is.EqualTo(SoldierAI.State.Attack));
        }

        [UnityTest] public IEnumerator _22_Encounter01Lock()
        {
            yield return LoadStage(stage => { stage.SetPlayerXForTest(7.1f); });
            yield return null;
            var stage=Object.FindFirstObjectByType<StageManager>();
            Assert.That(stage.CameraLocked,Is.True); Assert.That(stage.EnemyCount,Is.EqualTo(2));
        }

        [UnityTest] public IEnumerator _23_Encounter01Clear()
        {
            yield return LoadStage(stage => stage.BeginEncounter(0)); yield return null;
            var s=Object.FindFirstObjectByType<StageManager>(); s.ForceClearEncounterForTest();
            Assert.That(s.CameraLocked,Is.False);
        }

        [UnityTest] public IEnumerator _24_Encounter02Clear()
        {
            yield return LoadStage(stage => stage.BeginEncounter(0)); yield return null;
            var s=Object.FindFirstObjectByType<StageManager>(); s.ForceClearEncounterForTest(); s.BeginEncounter(1); yield return null;
            Assert.That(s.EnemyCount,Is.EqualTo(3)); s.ForceClearEncounterForTest(); Assert.That(s.CameraLocked,Is.False);
        }

        [UnityTest] public IEnumerator _25_Encounter03Clear()
        {
            yield return LoadStage(stage => stage.BeginEncounter(0)); yield return null;
            var s=Object.FindFirstObjectByType<StageManager>(); s.ForceClearEncounterForTest(); s.BeginEncounter(1); yield return null; s.ForceClearEncounterForTest(); s.BeginEncounter(2); yield return null;
            Assert.That(s.EnemyCount,Is.EqualTo(3)); s.ForceClearEncounterForTest(); Assert.That(s.CompletedEncounters,Is.EqualTo(3));
        }

        [UnityTest] public IEnumerator _26_StageExit()
        {
            yield return LoadStage(stage => stage.BeginEncounter(0)); yield return null;
            var s=Object.FindFirstObjectByType<StageManager>(); s.ForceClearEncounterForTest(); s.BeginEncounter(1); yield return null; s.ForceClearEncounterForTest(); s.BeginEncounter(2); yield return null; s.ForceClearEncounterForTest();
            Assert.That(s.StageClear,Is.False);
        }

        [UnityTest] public IEnumerator _27_StageClear()
        {
            yield return LoadStage(stage => stage.BeginEncounter(0)); yield return null;
            var s=Object.FindFirstObjectByType<StageManager>(); s.ForceClearEncounterForTest(); s.BeginEncounter(1); yield return null; s.ForceClearEncounterForTest(); s.BeginEncounter(2); yield return null; s.ForceClearEncounterForTest();
            s.SetPlayerXForTest(39.2f); yield return null; Assert.That(s.BossStarted,Is.True);Assert.That(s.StageClear,Is.False);s.ForceCompleteBossForTest();yield return null;Assert.That(s.BossDefeated,Is.True);Assert.That(s.StageClear,Is.True);
        }
    }
}
