using System.Collections;
using System.Linq;
using NUnit.Framework;
using ThreeKingdoms;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ThreeKingdoms.Tests.PlayMode
{
    public sealed class Iteration03PlayModeTests
    {
        private static IEnumerator Load(System.Action<StageManager> ready)
        {
            yield return SceneManager.LoadSceneAsync("SC_Stage01_AncientStreet",LoadSceneMode.Single);yield return null;yield return null;
            var stage=Object.FindFirstObjectByType<StageManager>();Assert.That(stage,Is.Not.Null);ready(stage);
        }

        private static IEnumerator SpawnSoldier(System.Action<StageManager,SoldierAI,CharacterCombat> ready)
        {
            StageManager stage=null;yield return Load(s=>stage=s);stage.SetPlayerXForTest(7.1f);yield return null;yield return null;
            var enemies=Object.FindObjectsByType<SoldierAI>(FindObjectsSortMode.None);
            foreach(var enemy in enemies){enemy.enabled=false;enemy.GetComponent<CharacterCombat>().enabled=false;}
            var ai=enemies.First();ai.SetTarget(stage.PlayerMotor.transform);var combat=ai.GetComponent<CharacterCombat>();ready(stage,ai,combat);
        }

        [UnityTest] public IEnumerator Iteration03_01_SoldierHasOnlyCombinedTwoStageSlash()
        {
            yield return SpawnSoldier((stage,ai,combat)=>
            {
                var data=ai.AttackDataForTest("Attack01");Assert.That(data,Is.Not.Null);Assert.That(data.startup,Is.GreaterThan(0f));Assert.That(data.active,Is.GreaterThan(0f));
                Assert.That(data.recovery,Is.GreaterThan(0f));Assert.That(data.cooldown,Is.GreaterThan(0f));Assert.That(data.rangeX,Is.GreaterThan(0f));Assert.That(data.rangeDepth,Is.GreaterThan(0f));
                foreach(string removed in new[]{"Attack02","JumpAttack","RunAttack"})Assert.That(ai.AttackDataForTest(removed),Is.Null,removed);
            });
        }

        [UnityTest] public IEnumerator Iteration03_02_SoldierAttackLifecycleStartupActiveRecoveryCooldown()
        {
            yield return SpawnSoldier((stage,ai,combat)=>
            {
                var data=ai.AttackDataForTest("Attack01");Assert.That(ai.BeginAttackForTest("Attack01"),Is.True);Assert.That(ai.CurrentState,Is.EqualTo(SoldierAI.State.Startup));
                ai.Tick(data.startup+.001f);Assert.That(ai.CurrentState,Is.EqualTo(SoldierAI.State.Active));ai.Tick(data.active+.001f);Assert.That(ai.CurrentState,Is.EqualTo(SoldierAI.State.Recovery));
                ai.Tick(data.recovery+.001f);Assert.That(ai.CurrentState,Is.EqualTo(SoldierAI.State.Cooldown));Assert.That(ai.CooldownRemaining,Is.GreaterThan(data.cooldown));
            });
        }

        [UnityTest] public IEnumerator Iteration03_03_TwoStageSlashDamagesInRangeAndMissesDepth()
        {
            foreach(string action in new[]{"Attack01"})
            {
                StageManager stage=null;SoldierAI ai=null;CharacterCombat combat=null;yield return SpawnSoldier((s,a,c)=>{stage=s;ai=a;combat=c;});
                var enemyMotor=ai.GetComponent<CharacterMotor>();enemyMotor.SetPosition(6f,-.6f);stage.PlayerMotor.SetPosition(7.2f,-.6f);float before=stage.PlayerHealth.Current;
                Assert.That(ai.BeginAttackForTest(action),Is.True);var data=ai.AttackDataForTest(action);combat.ManualTick(data.startup+data.active*.5f);Assert.That(stage.PlayerHealth.Current,Is.EqualTo(before-24f).Within(.01f),action+" must resolve exactly two phases (10 + 14), not damage every frame");

                yield return SceneManager.LoadSceneAsync("SC_Stage01_AncientStreet",LoadSceneMode.Single);yield return null;yield return null;stage=Object.FindFirstObjectByType<StageManager>();stage.SetPlayerXForTest(7.1f);yield return null;yield return null;
                var enemies=Object.FindObjectsByType<SoldierAI>(FindObjectsSortMode.None);foreach(var enemy in enemies){enemy.enabled=false;enemy.GetComponent<CharacterCombat>().enabled=false;}ai=enemies.First();ai.SetTarget(stage.PlayerMotor.transform);combat=ai.GetComponent<CharacterCombat>();enemyMotor=ai.GetComponent<CharacterMotor>();enemyMotor.SetPosition(6f,-.6f);stage.PlayerMotor.SetPosition(7.2f,-.24f);stage.PlayerMotor.transform.position=new Vector3(7.2f,.8f,0f);before=stage.PlayerHealth.Current;
                Assert.That(ai.BeginAttackForTest(action),Is.True);data=ai.AttackDataForTest(action);combat.ManualTick(data.startup+data.active*.5f);Assert.That(stage.PlayerHealth.Current,Is.EqualTo(before),action+" must miss outside Depth range");
            }
        }

        [UnityTest] public IEnumerator Iteration03_04_MultipleSoldiersHaveDesynchronizedCooldowns()
        {
            StageManager stage=null;yield return Load(s=>stage=s);stage.SetPlayerXForTest(18.1f);yield return null;yield return null;
            var enemies=Object.FindObjectsByType<SoldierAI>(FindObjectsSortMode.None);Assert.That(enemies.Length,Is.GreaterThanOrEqualTo(2));
            foreach(var ai in enemies.Take(2)){ai.enabled=false;ai.GetComponent<CharacterCombat>().enabled=false;Assert.That(ai.BeginAttackForTest("Attack01"),Is.True);var data=ai.AttackDataForTest("Attack01");ai.Tick(data.Duration+.01f);}
            Assert.That(enemies.Take(2).Select(x=>x.CooldownRemaining).Distinct().Count(),Is.GreaterThan(1));
        }

        [UnityTest] public IEnumerator Iteration03_05_NavigationUsesPolygonAndBlockedZones()
        {
            yield return Load(stage=>
            {
                var map=Object.FindFirstObjectByType<StageNavigationMap>();Assert.That(map,Is.Not.Null);Assert.That(map.WalkablePolygon.Length,Is.EqualTo(15));Assert.That(map.BlockedZones.Length,Is.EqualTo(5));
                Assert.That(map.ApproximateBeltDepth,Is.InRange(2f,2.4f));Assert.That(map.IsWalkable(new Vector2(8f,-.5f)),Is.True);Assert.That(map.IsWalkable(new Vector2(8f,.2f)),Is.False);
                Assert.That(map.IsWalkable(new Vector2(3f,-.48f)),Is.False);
            });
        }

        [UnityTest] public IEnumerator Iteration03_06_MotorCannotEnterRoofOrBlockedDecoration()
        {
            yield return Load(stage=>
            {
                var map=Object.FindFirstObjectByType<StageNavigationMap>();stage.PlayerMotor.SetPosition(8f,.8f);Assert.That(stage.PlayerMotor.Depth,Is.LessThan(-.2f));
                stage.PlayerMotor.SetPosition(3f,-.48f);Assert.That(map.IsWalkable(new Vector2(stage.PlayerMotor.X,stage.PlayerMotor.Depth)),Is.True);
            });
        }

        private static void AssertLocked(CharacterMotor motor,CharacterCombat combat,System.Action request,float duringTick,float finishTick)
        {
            motor.SetPosition(8f,-.7f);Vector2 before=new Vector2(motor.X,motor.Depth);request();motor.SetPosition(9.2f,.6f);combat.ManualTick(duringTick);
            AssertVector(before,new Vector2(motor.X,motor.Depth));combat.ManualTick(finishTick);
            AssertVector(before,new Vector2(motor.X,motor.Depth));AssertVector(Vector2.zero,motor.ActionVisualOffset);
        }

        private static void AssertVector(Vector2 expected,Vector2 actual)=>Assert.That(Vector2.Distance(expected,actual),Is.LessThan(.001f));

        [UnityTest] public IEnumerator Iteration03_07_Skill1RootLockedBeforeDuringAfter(){yield return Load(stage=>{var c=stage.PlayerMotor.GetComponent<CharacterCombat>();c.enabled=false;AssertLocked(stage.PlayerMotor,c,c.RequestSkill1,.2f,2f);});}
        [UnityTest] public IEnumerator Iteration03_08_ChargeRootLockedBeforeDuringAfter()
        {
            yield return Load(stage=>{var c=stage.PlayerMotor.GetComponent<CharacterCombat>();c.enabled=false;var m=stage.PlayerMotor;m.SetPosition(8f,-.7f);Vector2 before=new Vector2(m.X,m.Depth);c.BeginCharge();m.SetPosition(9f,.5f);c.ManualTick(.2f);AssertVector(before,new Vector2(m.X,m.Depth));c.ReleaseChargeForTest(.6f);m.SetPosition(9f,.5f);c.ManualTick(2f);AssertVector(before,new Vector2(m.X,m.Depth));});
        }
        [UnityTest] public IEnumerator Iteration03_09_Skill3RootLockedBeforeDuringAfter(){yield return Load(stage=>{var c=stage.PlayerMotor.GetComponent<CharacterCombat>();c.enabled=false;AssertLocked(stage.PlayerMotor,c,c.RequestSkill3,.7f,3f);});}
        [UnityTest] public IEnumerator Iteration03_10_Skill4RootLockedBeforeDuringAfter(){yield return Load(stage=>{var c=stage.PlayerMotor.GetComponent<CharacterCombat>();c.enabled=false;AssertLocked(stage.PlayerMotor,c,c.RequestSkill4,.3f,2f);});}

        [UnityTest] public IEnumerator Iteration03_11_ParryCounterRootLockedBeforeDuringAfter()
        {
            StageManager stage=null;yield return Load(s=>stage=s);var c=stage.PlayerMotor.GetComponent<CharacterCombat>();c.enabled=false;var m=stage.PlayerMotor;m.SetPosition(8f,-.7f);Vector2 before=new Vector2(m.X,m.Depth);
            c.RequestParry();c.ManualTick(.09f);stage.PlayerHealth.ReceiveDamage(new DamagePacket(null,10f,0f,0f,0f));Assert.That(c.CurrentAction,Is.EqualTo("ParryCounter"));m.SetPosition(9f,.5f);c.ManualTick(.2f);
            AssertVector(before,new Vector2(m.X,m.Depth));c.ManualTick(2f);AssertVector(before,new Vector2(m.X,m.Depth));
        }

        [UnityTest] public IEnumerator Iteration03_12_ArcadeHudBuildsAndTracksHealth()
        {
            StageManager stage=null;yield return Load(s=>stage=s);var hud=Object.FindFirstObjectByType<StageHud>();Assert.That(hud,Is.Not.Null);Assert.That(hud.HudBuilt,Is.True);
            stage.PlayerHealth.ReceiveDamage(new DamagePacket(null,25f,0f,0f,0f));yield return null;Assert.That(hud.DisplayedHealthRatio,Is.EqualTo(.75f).Within(.01f));
            Assert.That(Camera.main.GetComponentsInChildren<SpriteRenderer>().Count(x=>x.sortingOrder>=5000),Is.GreaterThanOrEqualTo(3));
        }
    }
}
