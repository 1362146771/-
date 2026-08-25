using System.Collections;
using NUnit.Framework;
using ThreeKingdoms;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ThreeKingdoms.Tests.PlayMode
{
    public sealed class VisualAnchorFixPlayModeTests
    {
        private static IEnumerator Load(System.Action<StageManager> ready)
        {
            yield return SceneManager.LoadSceneAsync("SC_Stage01_AncientStreet",LoadSceneMode.Single);yield return null;yield return null;
            var stage=Object.FindFirstObjectByType<StageManager>();Assert.That(stage,Is.Not.Null);ready(stage);
        }

        [UnityTest] public IEnumerator VisualAnchor_01_DiaochanUsesBakedShadowOnly()
        {
            yield return Load(stage=>
            {
                Transform shadow=stage.PlayerMotor.transform.Find("Shadow");Assert.That(shadow,Is.Not.Null);Assert.That(shadow.gameObject.activeSelf,Is.False,"duplicate runtime shadow must stay disabled");
                Assert.That(stage.PlayerMotor.GetComponentsInChildren<SpriteRenderer>(false).Length,Is.EqualTo(1),"only the animated sprite renderer should be visible");
            });
        }

        [UnityTest] public IEnumerator VisualAnchor_02_AllAirborneSkillsUseShadowCustomPivot()
        {
            yield return Load(stage=>
            {
                var animator=stage.PlayerMotor.GetComponent<CharacterAnimator>();var renderer=stage.PlayerMotor.transform.Find("VisualRoot").GetComponent<SpriteRenderer>();Vector2 root=new Vector2(stage.PlayerMotor.X,stage.PlayerMotor.Depth);
                foreach(var sample in new[]{("Skill1",.52f),("ParryCounter",.48f),("ChargeSkill2",.72f),("Skill3_A",.55f),("Skill4",.50f)})
                {
                    animator.Sample(sample.Item1,sample.Item2);Sprite sprite=renderer.sprite;Assert.That(sprite,Is.Not.Null,sample.Item1);
                    Vector2 normalized=new Vector2(sprite.pivot.x/sprite.rect.width,sprite.pivot.y/sprite.rect.height);
                    Assert.That(normalized.y,Is.InRange(0f,.22f),sample.Item1+" pivot must be on its baked ground shadow, not canvas center");
                    Assert.That(Vector2.Distance(root,new Vector2(stage.PlayerMotor.X,stage.PlayerMotor.Depth)),Is.LessThan(.001f),sample.Item1+" must not move CharacterRoot");
                    Assert.That(stage.PlayerMotor.transform.Find("VisualRoot").localPosition,Is.EqualTo(Vector3.zero));
                }
            });
        }

        [UnityTest] public IEnumerator VisualAnchor_03_ReplacementSoldierUsesSingleRuntimeShadow()
        {
            StageManager stage=null;yield return Load(s=>stage=s);stage.BeginEncounter(0);yield return null;yield return null;
            var soldier=Object.FindFirstObjectByType<SoldierAI>();Assert.That(soldier,Is.Not.Null);Transform shadow=soldier.transform.Find("Shadow");Assert.That(shadow.gameObject.activeSelf,Is.True,"replacement sprites have no baked checker shadow");
            var animator=soldier.GetComponent<CharacterAnimator>();var renderer=soldier.transform.Find("VisualRoot").GetComponent<SpriteRenderer>();animator.Sample("Attack01",.55f);
            Vector2 normalized=new Vector2(renderer.sprite.pivot.x/renderer.sprite.rect.width,renderer.sprite.pivot.y/renderer.sprite.rect.height);Assert.That(normalized.y,Is.InRange(0f,.22f));
        }
    }
}
