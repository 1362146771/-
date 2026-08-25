using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ThreeKingdoms.Tests
{
    public sealed class DiaochanHitReactionPlayModeTests
    {
        [UnityTest] public IEnumerator NonLethalDamage_PlaysHitReactLocksInputThenReturnsIdle()
        {
            yield return SceneManager.LoadSceneAsync("SC_Stage01_AncientStreet",LoadSceneMode.Single);yield return null;yield return null;
            StageManager stage=Object.FindFirstObjectByType<StageManager>();stage.enabled=false;CharacterHealth health=stage.PlayerHealth;var animationDriver=health.GetComponent<CharacterAnimator>();var input=health.GetComponent<PlayerInputController>();var reaction=health.GetComponent<DiaochanHitReaction>();
            Assert.That(reaction,Is.Not.Null);Assert.That(health.ReceiveDamage(new DamagePacket(null,10f,0f,0f,0f)),Is.True);yield return null;
            Assert.That(animationDriver.CurrentState,Is.EqualTo("HitReact"));Assert.That(input.enabled,Is.False);Assert.That(reaction.IsReacting,Is.True);
            yield return new WaitForSeconds(.56f);Assert.That(animationDriver.CurrentState,Is.EqualTo("CombatIdle"));Assert.That(input.enabled,Is.True);Assert.That(reaction.IsReacting,Is.False);
        }
    }
}
