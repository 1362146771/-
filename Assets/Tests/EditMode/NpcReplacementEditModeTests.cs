using System.Linq;
using NUnit.Framework;
using ThreeKingdoms;
using UnityEditor;
using UnityEngine;

namespace ThreeKingdoms.Tests.EditMode
{
    public sealed class NpcReplacementEditModeTests
    {
        [Test] public void ReplacementSoldier_AllRuntimeStatesUseNewSprites()
        {
            string[] ids={"Idle","IdleLong","Walk","Attack01","HitReact","Death"};
            foreach(string id in ids)
            {
                var action=AssetDatabase.LoadAssetAtPath<ActionDefinition>($"Assets/Data/Combat/Actions/Soldier/SOL_{id}.asset");
                Assert.That(action,Is.Not.Null,id);Assert.That(action.animation,Is.Not.Null,id);
                ObjectReferenceKeyframe[] keys=AnimationUtility.GetObjectReferenceCurve(action.animation,EditorCurveBinding.PPtrCurve("VisualRoot",typeof(SpriteRenderer),"m_Sprite"));
                Assert.That(keys,Is.Not.Empty,id);Assert.That(AssetDatabase.GetAssetPath(keys[0].value),Does.Contain("SoldierReplacement"),id);
            }
        }

        [Test] public void ReplacementSoldier_HasOnlyOneNineteenFrameTwoStageSlashWithoutWalkTail()
        {
            var attack=AssetDatabase.LoadAssetAtPath<ActionDefinition>("Assets/Data/Combat/Actions/Soldier/SOL_Attack01.asset");Assert.That(attack,Is.Not.Null);Assert.AreEqual(19,attack.frameCount);Assert.AreEqual(ActionHitPolicy.OncePerPhase,attack.combat.hitPolicy);
            ObjectReferenceKeyframe[] keys=AnimationUtility.GetObjectReferenceCurve(attack.animation,EditorCurveBinding.PPtrCurve("VisualRoot",typeof(SpriteRenderer),"m_Sprite"));Assert.AreEqual(20,keys.Length);
            Assert.That(AssetDatabase.GetAssetPath(keys[0].value),Does.EndWith("NSOL_Attack_000.png"));Assert.That(AssetDatabase.GetAssetPath(keys[18].value),Does.EndWith("NSOL_Attack_018.png"));
            foreach(string id in new[]{"Attack02","RunAttack","JumpAttack"})Assert.That(AssetDatabase.LoadAssetAtPath<ActionDefinition>($"Assets/Data/Combat/Actions/Soldier/SOL_{id}.asset"),Is.Null,id+" must be removed");
        }

        [Test] public void ReplacementSoldier_AttackUsesOneCanvasPivotWithoutPerFrameTeleport()
        {
            var attack=AssetDatabase.LoadAssetAtPath<ActionDefinition>("Assets/Data/Combat/Actions/Soldier/SOL_Attack01.asset");ObjectReferenceKeyframe[] keys=AnimationUtility.GetObjectReferenceCurve(attack.animation,EditorCurveBinding.PPtrCurve("VisualRoot",typeof(SpriteRenderer),"m_Sprite"));
            Vector2 FramePivot(int index){Sprite sprite=(Sprite)keys[index].value;return new Vector2(sprite.pivot.x/sprite.rect.width,sprite.pivot.y/sprite.rect.height);}
            Vector2 expected=new Vector2(147f/252f,38f/151f);
            for(int i=0;i<19;i++)Assert.That(Vector2.Distance(FramePivot(i),expected),Is.LessThan(.005f),$"frame {i} must preserve the common source canvas pivot");
        }

        [Test] public void ReplacementSoldier_HitReactUsesBloodFramesAndDeathStaysBloodless()
        {
            var hurt=AssetDatabase.LoadAssetAtPath<ActionDefinition>("Assets/Data/Combat/Actions/Soldier/SOL_HitReact.asset");
            var death=AssetDatabase.LoadAssetAtPath<ActionDefinition>("Assets/Data/Combat/Actions/Soldier/SOL_Death.asset");
            Assert.AreEqual(9,hurt.frameCount);Assert.AreEqual(45,death.frameCount);
            var hurtKeys=AnimationUtility.GetObjectReferenceCurve(hurt.animation,EditorCurveBinding.PPtrCurve("VisualRoot",typeof(SpriteRenderer),"m_Sprite"));
            Assert.That(AssetDatabase.GetAssetPath(hurtKeys[0].value),Does.EndWith("NSOL_HitBlood_000.png"));
            Assert.That(AssetDatabase.GetAssetPath(hurtKeys[8].value),Does.EndWith("NSOL_HitBlood_025.png"));
            var deathKeys=AnimationUtility.GetObjectReferenceCurve(death.animation,EditorCurveBinding.PPtrCurve("VisualRoot",typeof(SpriteRenderer),"m_Sprite"));
            Assert.That(AssetDatabase.GetAssetPath(deathKeys[0].value),Does.EndWith("NSOL_HurtDeath_034.png"));
            Assert.That(AssetDatabase.GetAssetPath(deathKeys[44].value),Does.EndWith("NSOL_HurtDeath_078.png"));
        }

        [Test] public void ReplacementSoldier_HitReactFramesUseDetectedFootAnchorsAndSourceTiming()
        {
            var hurt=AssetDatabase.LoadAssetAtPath<ActionDefinition>("Assets/Data/Combat/Actions/Soldier/SOL_HitReact.asset");ObjectReferenceKeyframe[] keys=AnimationUtility.GetObjectReferenceCurve(hurt.animation,EditorCurveBinding.PPtrCurve("VisualRoot",typeof(SpriteRenderer),"m_Sprite"));
            float minX=1f,maxX=0f;
            foreach(ObjectReferenceKeyframe key in keys.Take(9))
            {
                Sprite sprite=(Sprite)key.value;Vector2 normalized=new Vector2(sprite.pivot.x/sprite.rect.width,sprite.pivot.y/sprite.rect.height);
                Assert.That(normalized.y,Is.EqualTo(.02f).Within(.011f),sprite.name+" must anchor to the visible foot pixels");
                minX=Mathf.Min(minX,normalized.x);maxX=Mathf.Max(maxX,normalized.x);
            }
            Assert.That(maxX-minX,Is.GreaterThan(.2f),"horizontal foot anchor must compensate the pose shift without moving the root");
            Assert.That(hurt.animation.length,Is.EqualTo(.34f).Within(.011f));
        }

        [Test] public void DiaochanDeath_UsesReplacementFiftyFrameClip()
        {
            var action=AssetDatabase.LoadAssetAtPath<ActionDefinition>("Assets/Data/Combat/Actions/Diaochan/DIA_Death.asset");Assert.That(action,Is.Not.Null);Assert.AreEqual(50,action.frameCount);
            ObjectReferenceKeyframe[] keys=AnimationUtility.GetObjectReferenceCurve(action.animation,EditorCurveBinding.PPtrCurve("VisualRoot",typeof(SpriteRenderer),"m_Sprite"));Assert.That(keys.Length,Is.EqualTo(51));
            Assert.That(AssetDatabase.GetAssetPath(keys[0].value),Does.Contain("DeathReplacementFrames"));
        }

        [Test] public void ReplacementSoldier_PrefabHasReactionControllerAndShadow()
        {
            GameObject prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Characters/PF_CommonSoldier.prefab");SoldierReactionController reaction=prefab.GetComponent<SoldierReactionController>();Assert.That(reaction,Is.Not.Null);Assert.That(prefab.transform.Find("Shadow").gameObject.activeSelf,Is.True);
            Assert.That(new SerializedObject(reaction).FindProperty("hurtDuration").floatValue,Is.EqualTo(.34f).Within(.001f));
        }
    }
}
