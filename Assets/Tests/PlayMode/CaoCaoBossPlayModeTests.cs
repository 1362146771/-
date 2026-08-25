using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ThreeKingdoms.Tests
{
    public sealed class CaoCaoBossPlayModeTests
    {
        private static IEnumerator Load(System.Action<StageManager> ready)
        {
            yield return SceneManager.LoadSceneAsync("SC_Stage01_AncientStreet",LoadSceneMode.Single);yield return null;yield return null;var stage=Object.FindFirstObjectByType<StageManager>();Assert.That(stage,Is.Not.Null);ready(stage);
        }

        [UnityTest] public IEnumerator CaoCao_01_BossPrefabHasNineteenAnimationStatesAndNineAttacks()
        {
            StageManager stage=null;yield return Load(s=>stage=s);stage.BeginBossForTest();var boss=stage.ActiveBoss;Assert.That(boss,Is.Not.Null);Assert.That(boss.Maximum,Is.EqualTo(900f));var ai=boss.GetComponent<CaoCaoBossAI>();Assert.That(ai,Is.Not.Null);
            foreach(string action in new[]{"Thrust","DownSlash","DownSlashWave","Charge","ChargedCharge","Skill","Phase1Ultimate","Phase2Ultimate","Phase3Ultimate"})Assert.That(ai.AttackDataForTest(action),Is.Not.Null,action);
            var controller=boss.GetComponent<Animator>().runtimeAnimatorController;Assert.That(controller,Is.Not.Null);Assert.That(controller.animationClips.Length,Is.EqualTo(19));
            Assert.That(boss.transform.position.y,Is.EqualTo(-1.95f).Within(.001f),"Cao Cao should spawn in the front-middle combat belt, not deep in the background");
            Assert.That(boss.GetComponent<CharacterMotor>().VisualScale,Is.EqualTo(1.70f).Within(.001f));var runner=boss.GetComponent<ActionRunner>();Assert.That(runner,Is.Not.Null);Assert.That(runner.Library,Is.Not.Null);Assert.That(runner.Library.ownerId,Is.EqualTo("CaoCao"));Assert.That(runner.Library.actions.Count,Is.EqualTo(19));
        }

        [UnityTest] public IEnumerator CaoCao_02_SkillSuperArmorTakesDamageWithoutStaggerOrKnockback()
        {
            StageManager stage=null;yield return Load(s=>stage=s);stage.BeginBossForTest();var boss=stage.ActiveBoss;var ai=boss.GetComponent<CaoCaoBossAI>();ai.SetTarget(stage.PlayerMotor.transform);boss.GetComponent<CharacterMotor>().SetPosition(38f,-1.05f);Vector2 before=new Vector2(boss.transform.position.x,boss.transform.position.y);float hp=boss.Current;
            Assert.That(ai.BeginAttackForTest("Skill"),Is.True);Assert.That(ai.IsSkillProtected,Is.True);var source=stage.PlayerMotor.GetComponent<CharacterIdentity>();Assert.That(boss.ReceiveDamage(new DamagePacket(source,25f,-2f,.5f,0f)),Is.True);
            Assert.That(boss.Current,Is.EqualTo(hp-25f));Assert.That(new Vector2(boss.transform.position.x,boss.transform.position.y),Is.EqualTo(before));Assert.That(ai.State,Is.EqualTo(CaoCaoBossAI.BossState.Attack));Assert.That(boss.GetComponent<CharacterAnimator>().CurrentState,Is.EqualTo("Skill"));
        }

        [UnityTest] public IEnumerator CaoCao_03_StageClearRequiresBossDefeat()
        {
            StageManager stage=null;yield return Load(s=>stage=s);stage.BeginEncounter(0);yield return null;stage.ForceClearEncounterForTest();stage.BeginEncounter(1);yield return null;stage.ForceClearEncounterForTest();stage.BeginEncounter(2);yield return null;stage.ForceClearEncounterForTest();stage.SetPlayerXForTest(39.2f);yield return null;
            Assert.That(stage.BossStarted,Is.True);Assert.That(stage.CameraLocked,Is.True);Assert.That(stage.StageClear,Is.False);stage.ForceCompleteBossForTest();yield return null;Assert.That(stage.BossDefeated,Is.True);Assert.That(stage.StageClear,Is.True);
        }
    }
}
