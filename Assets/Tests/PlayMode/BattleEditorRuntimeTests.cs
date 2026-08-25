using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ThreeKingdoms.Tests.PlayMode
{
    public sealed class BattleEditorRuntimeTests
    {
        private ActionDefinition MakeAction()
        {
            var a=ScriptableObject.CreateInstance<ActionDefinition>();a.id="Probe";a.ownerId="Test";a.frameCount=4;a.framesPerSecond=10;a.animation=new AnimationClip();a.startupEndFrame=1;a.activeEndFrame=2;a.recoveryEndFrame=3;
            a.frameShapes.Add(new ActionFrameShape{frame=1,role=ActionShapeRole.EffectHitbox,shapeType=ActionShapeType.Box,center=new Vector2(1,1),size=new Vector2(1,2)});return a;
        }
        private GameObject MakeRunner(ActionDefinition a)
        {
            var go=new GameObject("Runner",typeof(CharacterIdentity),typeof(CharacterMotor),typeof(CharacterAnimator),typeof(ActionRunner));var lib=ScriptableObject.CreateInstance<CharacterActionLibrary>();lib.actions.Add(a);go.GetComponent<ActionRunner>().Configure(lib);return go;
        }
        [UnityTest] public IEnumerator ActionDefinition_RuntimeLoads(){var a=MakeAction();Assert.AreEqual("Probe",a.id);Assert.AreEqual(4,a.frameCount);Object.Destroy(a);yield return null;}
        [UnityTest] public IEnumerator ActionRunner_PlayAction(){var a=MakeAction();var go=MakeRunner(a);Assert.IsTrue(go.GetComponent<ActionRunner>().Play(a));Assert.IsTrue(go.GetComponent<ActionRunner>().IsPlaying);Object.Destroy(go);Object.Destroy(a);yield return null;}
        [UnityTest] public IEnumerator ActionRunner_FrameHitboxChanges(){var a=MakeAction();var go=MakeRunner(a);var r=go.GetComponent<ActionRunner>();r.Play(a);r.ManualTick(.11f);Assert.AreEqual(1,r.CurrentFrame);r.ManualTick(.11f);Assert.AreEqual(2,r.CurrentFrame);Object.Destroy(go);Object.Destroy(a);yield return null;}
        [UnityTest] public IEnumerator ActionRunner_AppliesAuthoredHurtboxForCurrentFrame()
        {
            var a=MakeAction();a.frameShapes.Add(new ActionFrameShape{frame=1,role=ActionShapeRole.Hurtbox,shapeType=ActionShapeType.Box,center=new Vector2(.25f,1.4f),size=new Vector2(1.1f,1.7f)});
            var go=MakeRunner(a);var hurtbox=go.AddComponent<Hurtbox>();var runner=go.GetComponent<ActionRunner>();runner.Configure(runner.Library);runner.Play(a);runner.ManualTick(.11f);
            Assert.AreEqual(new Vector2(.25f,1.4f),hurtbox.LocalCenter);Assert.AreEqual(new Vector2(1.1f,1.7f),hurtbox.LocalSize);
            Object.Destroy(go);Object.Destroy(a);yield return null;
        }
        [UnityTest] public IEnumerator ActionRunner_DoesNotSkipCrossedDamageFrames()
        {
            var a=MakeAction();a.combat.damage=1f;a.combat.hitPolicy=ActionHitPolicy.OncePerFrame;a.frameShapes.Add(new ActionFrameShape{frame=2,role=ActionShapeRole.EffectHitbox,shapeType=ActionShapeType.Box,center=new Vector2(1,1),size=new Vector2(2,3)});a.frameShapes[0].size=new Vector2(2,3);
            var source=MakeRunner(a);source.GetComponent<CharacterIdentity>().Configure(CharacterTeam.Player);
            var target=new GameObject("Target",typeof(CharacterIdentity),typeof(CharacterHealth),typeof(DamageReceiver),typeof(Hurtbox));target.transform.position=new Vector3(1,0,0);target.GetComponent<CharacterIdentity>().Configure(CharacterTeam.Enemy);
            var runner=source.GetComponent<ActionRunner>();runner.Play(a);runner.ManualTick(.25f);
            Assert.AreEqual(2,runner.LastHitCount);Assert.AreEqual(98f,target.GetComponent<CharacterHealth>().Current,.001f);
            Object.Destroy(source);Object.Destroy(target);Object.Destroy(a);yield return null;
        }
        [UnityTest] public IEnumerator EffectHitbox_HitsInside(){var a=MakeAction();var go=MakeRunner(a);Assert.IsTrue(go.GetComponent<ActionRunner>().ContainsPointForTest(a.frameShapes[0],new Vector2(1,1),1));Object.Destroy(go);Object.Destroy(a);yield return null;}
        [UnityTest] public IEnumerator EffectHitbox_MissesOutside(){var a=MakeAction();var go=MakeRunner(a);Assert.IsFalse(go.GetComponent<ActionRunner>().ContainsPointForTest(a.frameShapes[0],new Vector2(4,1),1));Object.Destroy(go);Object.Destroy(a);yield return null;}
        [UnityTest] public IEnumerator FacingLeft_HitboxMirrors(){var a=MakeAction();var go=MakeRunner(a);var r=go.GetComponent<ActionRunner>();Assert.IsTrue(r.ContainsPointForTest(a.frameShapes[0],new Vector2(-1,1),-1));Assert.IsFalse(r.ContainsPointForTest(a.frameShapes[0],new Vector2(1,1),-1));Object.Destroy(go);Object.Destroy(a);yield return null;}
        [UnityTest] public IEnumerator Movement_DepthSeparateFromElevation(){var a=MakeAction();a.movement.moveDepth=AnimationCurve.Linear(0,0,1,.5f);a.movement.elevation=AnimationCurve.Linear(0,0,1,1f);Assert.AreNotEqual(a.movement.moveDepth.Evaluate(1),a.movement.elevation.Evaluate(1));Object.Destroy(a);yield return null;}
        [UnityTest] public IEnumerator ActionRunner_AppliesPerFrameVisualOffsetWithElevation(){var a=MakeAction();a.movement.elevation=AnimationCurve.Linear(0,0,1,.9f);a.SetVisualOffset(1,new Vector2(.25f,-.1f));var go=MakeRunner(a);var runner=go.GetComponent<ActionRunner>();runner.Play(a);runner.ManualTick(.11f);Vector2 value=go.GetComponent<CharacterMotor>().ActionVisualOffset;Assert.AreEqual(.25f,value.x,.0001f);Assert.AreEqual(.2f,value.y,.0001f);Object.Destroy(go);Object.Destroy(a);yield return null;}
        [UnityTest] public IEnumerator ActionRunner_VisualOffsetFollowsActualSpriteFlip()
        {
            var texture=new Texture2D(2,2);var sprite=Sprite.Create(texture,new Rect(0,0,2,2),new Vector2(.5f,.5f),1f);sprite.name="NSOL_Attack_Probe";
            var go=new GameObject("SoldierVisualProbe");var visual=new GameObject("VisualRoot");visual.transform.SetParent(go.transform,false);var renderer=visual.AddComponent<SpriteRenderer>();renderer.sprite=sprite;
            go.AddComponent<CharacterIdentity>();var motor=go.AddComponent<CharacterMotor>();var animator=go.AddComponent<CharacterAnimator>();var runner=go.AddComponent<ActionRunner>();var a=MakeAction();a.SetVisualOffset(1,new Vector2(.25f,0f));var lib=ScriptableObject.CreateInstance<CharacterActionLibrary>();lib.actions.Add(a);runner.Configure(lib);
            animator.Face(1);Assert.That(renderer.flipX,Is.False,"right-facing attack source must remain native when facing right");runner.Play(a);runner.ManualTick(.11f);Assert.AreEqual(.25f,motor.ActionVisualOffset.x,.0001f,"native-facing sprite must keep the saved editor correction");
            animator.Face(-1);Assert.That(renderer.flipX,Is.True,"right-facing attack source must flip when facing left");runner.Play(a);runner.ManualTick(.11f);Assert.AreEqual(-.25f,motor.ActionVisualOffset.x,.0001f,"authored source-space correction must mirror with the sprite");
            Object.Destroy(go);Object.Destroy(a);Object.Destroy(lib);Object.Destroy(sprite);Object.Destroy(texture);yield return null;
        }
        [UnityTest] public IEnumerator ActionRunner_UsesExactAuthoredSpriteIndexInsteadOfClipTimeGuess()
        {
            var texture=new Texture2D(4,2);var first=Sprite.Create(texture,new Rect(0,0,2,2),new Vector2(.5f,.5f),1f);var second=Sprite.Create(texture,new Rect(2,0,2,2),new Vector2(.5f,.5f),1f);first.name="ExactFrame0";second.name="ExactFrame1";
            var go=new GameObject("ExactFrameProbe");var visual=new GameObject("VisualRoot");visual.transform.SetParent(go.transform,false);var renderer=visual.AddComponent<SpriteRenderer>();renderer.sprite=first;go.AddComponent<CharacterIdentity>();go.AddComponent<CharacterMotor>();go.AddComponent<CharacterAnimator>();var runner=go.AddComponent<ActionRunner>();var action=MakeAction();action.visualFrames.AddRange(new[]{first,second,second,second});var library=ScriptableObject.CreateInstance<CharacterActionLibrary>();library.actions.Add(action);runner.Configure(library);
            runner.Play(action);runner.ManualTick(.11f);Assert.That(runner.CurrentFrame,Is.EqualTo(1));Assert.That(renderer.sprite,Is.SameAs(second),"runtime frame N must use the same Sprite N shown in Battle Editor");
            Object.Destroy(go);Object.Destroy(action);Object.Destroy(library);Object.Destroy(first);Object.Destroy(second);Object.Destroy(texture);yield return null;
        }
        [UnityTest] public IEnumerator ComboWindow_LoadsCorrectly(){var a=MakeAction();a.combo.Add(new ActionComboSegment{startFrame=0,comboWindowStart=2,comboWindowEnd=3,endFrame=3});Assert.Less(a.combo[0].comboWindowStart,a.combo[0].comboWindowEnd);Object.Destroy(a);yield return null;}
        [Test] public void HitCounter_CountsAndExpires(){var counter=new HitCounterState();counter.Register(1f);counter.Register(1.2f,2);Assert.AreEqual(3,counter.Count);Assert.IsFalse(counter.Tick(2.1f,1f));Assert.IsTrue(counter.Tick(2.21f,1f));Assert.AreEqual(0,counter.Count);}
        [Test] public void ComboSequence_AcceptsInputInsideConfiguredWindow()
        {
            var segments=new[]{new ComboSegment(0,.05f,.1f,.1f,.3f,.4f,1,0,0),new ComboSegment(.4f,.45f,.5f,.5f,.6f,.7f,1,0,0),new ComboSegment(.7f,.75f,.8f,.8f,.9f,1f,1,0,0),new ComboSegment(1f,1.05f,1.1f,1.1f,1.2f,1.3f,1,0,0)};
            var combo=new ComboSequence(segments);combo.Press();combo.Tick(.15f,out _);combo.Press();combo.Tick(.01f,out _);combo.Tick(.25f,out bool advanced);Assert.IsTrue(advanced);Assert.AreEqual(2,combo.MaxSegmentsReached);
        }
        [Test] public void ComboSequence_RejectsInputAfterConfiguredWindow()
        {
            var segments=new[]{new ComboSegment(0,.05f,.1f,.1f,.2f,.3f,1,0,0),new ComboSegment(.3f,.35f,.4f,.4f,.5f,.6f,1,0,0),new ComboSegment(.6f,.65f,.7f,.7f,.8f,.9f,1,0,0),new ComboSegment(.9f,.95f,1f,1f,1.1f,1.2f,1,0,0)};
            var combo=new ComboSequence(segments);combo.Press();combo.Tick(.21f,out _);combo.Press();combo.Tick(.1f,out _);Assert.AreEqual(1,combo.MaxSegmentsReached);Assert.IsFalse(combo.Active);
        }
        [UnityTest] public IEnumerator CharacterHealth_RaisesSuccessfulDamageEvent(){var sourceGo=new GameObject("Source",typeof(CharacterIdentity));sourceGo.GetComponent<CharacterIdentity>().Configure(CharacterTeam.Player);var targetGo=new GameObject("Target",typeof(CharacterIdentity),typeof(CharacterHealth));targetGo.GetComponent<CharacterIdentity>().Configure(CharacterTeam.Enemy);int count=0;System.Action<DamageAppliedEvent> handler=hit=>{if(hit.source==sourceGo.GetComponent<CharacterIdentity>())count++;};CharacterHealth.DamageApplied+=handler;try{Assert.IsTrue(targetGo.GetComponent<CharacterHealth>().ReceiveDamage(new DamagePacket(sourceGo.GetComponent<CharacterIdentity>(),9f,0f,0f,0f)));Assert.AreEqual(1,count);}finally{CharacterHealth.DamageApplied-=handler;Object.Destroy(sourceGo);Object.Destroy(targetGo);}yield return null;}
    }
}
