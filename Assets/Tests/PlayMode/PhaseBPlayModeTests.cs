using System.Collections;
using NUnit.Framework;
using ThreeKingdoms;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ThreeKingdoms.Tests.PlayMode
{
    public sealed class PhaseBPlayModeTests
    {
        private static IEnumerator Load(System.Action<CharacterCombat, CharacterHealth> check)
        {
            yield return SceneManager.LoadSceneAsync("SC_Stage01_AncientStreet", LoadSceneMode.Single);
            yield return null;
            var stage = Object.FindFirstObjectByType<StageManager>();
            var combat = stage.PlayerMotor.GetComponent<CharacterCombat>();
            stage.PlayerMotor.GetComponent<PlayerInputController>().enabled = false;
            combat.enabled = false;
            check(combat, stage.PlayerHealth);
        }

        [UnityTest] public IEnumerator PhaseB_01_CrouchAttack() { yield return Load((c,h)=>{c.SetCrouch(true);c.RequestCrouchAttack();Assert.That(c.CurrentAction,Is.EqualTo("CrouchAttack"));}); }
        [UnityTest] public IEnumerator PhaseB_02_Skill1() { yield return Load((c,h)=>{c.RequestSkill1();Assert.That(c.CurrentAction,Is.EqualTo("Skill1"));}); }
        [UnityTest] public IEnumerator PhaseB_03_ParryRequiresHit()
        {
            CharacterCombat combat=null;CharacterHealth health=null;
            yield return Load((c,h)=>{combat=c;health=h;c.RequestParry();Assert.That(c.CurrentAction,Is.EqualTo("Parry"));c.ManualTick(.09f);});
            bool damaged=health.ReceiveDamage(new DamagePacket(null,20,0,0,0));
            Assert.That(damaged,Is.False);Assert.That(combat.CurrentAction,Is.EqualTo("ParryCounter"));
        }
        [UnityTest] public IEnumerator PhaseB_04_ChargeSkill2() { yield return Load((c,h)=>{c.BeginCharge();c.ReleaseCharge();Assert.That(c.CurrentAction,Is.EqualTo("ChargeSkill2"));}); }
        [UnityTest] public IEnumerator PhaseB_05_Skill3UsesA() { yield return Load((c,h)=>{c.RequestSkill3();Assert.That(c.CurrentAction,Is.EqualTo("Skill3_A"));}); }
        [UnityTest] public IEnumerator PhaseB_06_Skill4() { yield return Load((c,h)=>{c.RequestSkill4();Assert.That(c.CurrentAction,Is.EqualTo("Skill4"));}); }
        [UnityTest] public IEnumerator PhaseB_07_DropItemHook() { yield return Load((c,h)=>{bool called=false;c.DropItemRequested+=()=>called=true;c.RequestDropItem();Assert.That(called,Is.True);}); }
    }
}
