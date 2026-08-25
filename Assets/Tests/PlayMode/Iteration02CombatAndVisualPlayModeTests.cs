using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ThreeKingdoms;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ThreeKingdoms.Tests.PlayMode
{
    public sealed class Iteration02CombatAndVisualPlayModeTests
    {
        private static IEnumerator Load(System.Action<StageManager,CharacterCombat> ready)
        {
            yield return SceneManager.LoadSceneAsync("SC_Stage01_AncientStreet",LoadSceneMode.Single);yield return null;yield return null;
            var stage=Object.FindFirstObjectByType<StageManager>();Assert.That(stage,Is.Not.Null);
            stage.PlayerMotor.GetComponent<PlayerInputController>().enabled=false;
            var combat=stage.PlayerMotor.GetComponent<CharacterCombat>();combat.enabled=false;ready(stage,combat);
        }

        private static IEnumerator SpawnTarget(StageManager stage,System.Action<CharacterHealth> ready,float xOffset=1.5f,float depthOffset=0f)
        {
            stage.PlayerMotor.SetPosition(7f,-.6f);yield return null;yield return null;
            var ai=Object.FindObjectsByType<SoldierAI>(FindObjectsSortMode.None).First();ai.enabled=false;
            var motor=ai.GetComponent<CharacterMotor>();motor.SetPosition(stage.PlayerMotor.X+xOffset,stage.PlayerMotor.Depth+depthOffset);
            ready(ai.GetComponent<CharacterHealth>());
        }

        [UnityTest] public IEnumerator Iteration02_01_BackgroundHasThreeSectionsAndFourParallaxLayers()
        {
            yield return Load((stage,combat)=>
            {
                var builder=Object.FindFirstObjectByType<StageVisualBuilder>();Assert.That(builder.SectionCount,Is.EqualTo(3));
                var layers=builder.GetComponentsInChildren<ParallaxLayer>();Assert.That(layers.Length,Is.EqualTo(4));
                CollectionAssert.AreEquivalent(new[]{-.04f,0f,.09f,.18f},layers.Select(x=>x.CameraFollow).ToArray());
                Assert.That(layers.All(x=>x.GetComponentsInChildren<SpriteRenderer>().Length==3),Is.True);
            });
        }

        [UnityTest] public IEnumerator Iteration02_02_CharactersUseUnifiedVisualScale()
        {
            yield return Load((stage,combat)=>Assert.That(stage.PlayerMotor.VisualScale,Is.EqualTo(2.38f).Within(.001f)));
            var stage=Object.FindFirstObjectByType<StageManager>();CharacterMotor soldier=null;
            yield return SpawnTarget(stage,h=>soldier=h.GetComponent<CharacterMotor>());
            Assert.That(soldier.VisualScale,Is.EqualTo(2.38f).Within(.001f));
        }

        [UnityTest] public IEnumerator Iteration02_03_Skill1DamagesInRangeAndStartsCooldown()
        {
            StageManager stage=null;CharacterCombat combat=null;yield return Load((s,c)=>{stage=s;combat=c;});CharacterHealth enemy=null;
            yield return SpawnTarget(stage,h=>enemy=h);float before=enemy.Current;combat.RequestSkill1();combat.ManualTick(.43f);
            Assert.That(enemy.Current,Is.LessThan(before));Assert.That(combat.IsOnCooldown("Skill1"),Is.True);
        }

        [UnityTest] public IEnumerator Iteration02_04_Skill1MissesOutsideDepth()
        {
            StageManager stage=null;CharacterCombat combat=null;yield return Load((s,c)=>{stage=s;combat=c;});CharacterHealth enemy=null;
            yield return SpawnTarget(stage,h=>enemy=h,1.5f,1.2f);float before=enemy.Current;combat.RequestSkill1();combat.ManualTick(.43f);
            Assert.That(enemy.Current,Is.EqualTo(before));
        }

        [UnityTest] public IEnumerator Iteration02_05_ParryWithoutHitNeverCounters()
        {
            CharacterCombat combat=null;yield return Load((s,c)=>combat=c);combat.RequestParry();combat.ManualTick(.7f);
            Assert.That(combat.LastParrySucceeded,Is.False);Assert.That(combat.CurrentAction,Is.Empty);
        }

        [UnityTest] public IEnumerator Iteration02_06_ParryHitCancelsDamageAndCounterDamagesEnemy()
        {
            StageManager stage=null;CharacterCombat combat=null;yield return Load((s,c)=>{stage=s;combat=c;});CharacterHealth enemy=null;
            yield return SpawnTarget(stage,h=>enemy=h,1.4f,0f);float playerBefore=stage.PlayerHealth.Current,enemyBefore=enemy.Current;
            combat.RequestParry();combat.ManualTick(.09f);bool applied=stage.PlayerHealth.ReceiveDamage(new DamagePacket(enemy.GetComponent<CharacterIdentity>(),20f,0f,0f,0f));
            Assert.That(applied,Is.False);Assert.That(stage.PlayerHealth.Current,Is.EqualTo(playerBefore));Assert.That(combat.CurrentAction,Is.EqualTo("ParryCounter"));
            combat.ManualTick(.19f);Assert.That(enemy.Current,Is.LessThan(enemyBefore));Assert.That(combat.LastParrySucceeded,Is.True);
        }

        [UnityTest] public IEnumerator Iteration02_07_ChargeHasThreeDistinctLevels()
        {
            float[] damage=new float[3],knockback=new float[3];float[] holds={.1f,.55f,1.1f};
            for(int i=0;i<3;i++)
            {
                CharacterCombat combat=null;yield return Load((s,c)=>combat=c);combat.BeginCharge();combat.ReleaseChargeForTest(holds[i]);
                Assert.That(combat.LastChargeLevel,Is.EqualTo(i));damage[i]=combat.LastAttackDamage;knockback[i]=combat.LastAttackKnockback;
            }
            Assert.That(damage[0],Is.LessThan(damage[1]));Assert.That(damage[1],Is.LessThan(damage[2]));
            Assert.That(knockback[0],Is.LessThan(knockback[1]));Assert.That(knockback[1],Is.LessThan(knockback[2]));
        }

        [UnityTest] public IEnumerator Iteration02_08_Skill3UsesDc16AndCooldown()
        {
            CharacterCombat combat=null;yield return Load((s,c)=>combat=c);combat.RequestSkill3();
            Assert.That(combat.CurrentAction,Is.EqualTo("Skill3_A"));Assert.That(combat.IsOnCooldown("Skill3_A"),Is.True);
        }

        [UnityTest] public IEnumerator Iteration02_09_Skill4IsGameplayAttackWithCooldown()
        {
            StageManager stage=null;CharacterCombat combat=null;yield return Load((s,c)=>{stage=s;combat=c;});CharacterHealth enemy=null;
            yield return SpawnTarget(stage,h=>enemy=h,1.5f,0f);float before=enemy.Current;combat.RequestSkill4();combat.ManualTick(.29f);
            Assert.That(enemy.Current,Is.LessThan(before));Assert.That(combat.IsOnCooldown("Skill4"),Is.True);
        }

        [UnityTest] public IEnumerator Iteration02_10_SkillCannotCancelComboUntilRecovery()
        {
            CharacterCombat combat=null;yield return Load((s,c)=>combat=c);combat.RequestComboAttack();combat.RequestSkill1();
            Assert.That(combat.CurrentAction,Is.EqualTo("AttackCombo4"));combat.ManualTick(.30f);combat.RequestSkill1();
            Assert.That(combat.CurrentAction,Is.EqualTo("Skill1"));Assert.That(combat.CurrentPriority,Is.EqualTo(ActionPriority.Skill));
        }

        [UnityTest] public IEnumerator Iteration02_11_PlayerPrefabEnablesSkillInput()
        {
            yield return SceneManager.LoadSceneAsync("SC_Stage01_AncientStreet",LoadSceneMode.Single);yield return null;yield return null;
            var stage=Object.FindFirstObjectByType<StageManager>();Assert.That(stage,Is.Not.Null);
            var input=stage.PlayerMotor.GetComponent<PlayerInputController>();Assert.That(input,Is.Not.Null);Assert.That(input.enabled,Is.True);
            var field=typeof(PlayerInputController).GetField("phaseBEnabled",BindingFlags.Instance|BindingFlags.NonPublic);
            Assert.That(field,Is.Not.Null);Assert.That((bool)field.GetValue(input),Is.True,"The built player prefab must accept H/F/I/U/O/Q input.");
        }
    }
}
