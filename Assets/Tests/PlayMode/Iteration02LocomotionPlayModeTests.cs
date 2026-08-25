using System.Collections;
using NUnit.Framework;
using ThreeKingdoms;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ThreeKingdoms.Tests.PlayMode
{
    public sealed class Iteration02LocomotionPlayModeTests
    {
        [Test]
        public void DoubleTap_HoldOnce_RemainsWalk()
        {
            var detector = new DoubleTapRunDetector(.25f, .20f, .05f);
            detector.Press(RunDirection.Right, 0f);
            Assert.That(detector.Evaluate(Vector2.right, .8f), Is.False);
        }

        [Test]
        public void DoubleTap_SecondHoldWithinWindow_Runs()
        {
            var detector = new DoubleTapRunDetector(.25f, .20f, .05f);
            detector.Press(RunDirection.Right, 0f);
            detector.Release(RunDirection.Right, .10f);
            detector.Press(RunDirection.Right, .20f);
            Assert.That(detector.Evaluate(Vector2.right, .26f), Is.True);
            Assert.That(detector.RunningDirection, Is.EqualTo(RunDirection.Right));
        }

        [Test]
        public void DoubleTap_SecondPressTooLate_RemainsWalk()
        {
            var detector = new DoubleTapRunDetector(.25f, .20f, .05f);
            detector.Press(RunDirection.Right, 0f);
            detector.Release(RunDirection.Right, .10f);
            detector.Press(RunDirection.Right, .40f);
            Assert.That(detector.Evaluate(Vector2.right, .50f), Is.False);
        }

        [UnityTest]
        public IEnumerator Walk_RuntimeSpriteChangesAcrossLoop()
        {
            yield return LoadAndVerifyAnimatedState("Walk", .12f);
        }

        [UnityTest]
        public IEnumerator Run_RuntimeSpriteChangesAcrossLoop()
        {
            yield return LoadAndVerifyAnimatedState("Run", .10f);
        }

        private static IEnumerator LoadAndVerifyAnimatedState(string state, float interval)
        {
            yield return SceneManager.LoadSceneAsync("SC_Stage01_AncientStreet", LoadSceneMode.Single);
            yield return null;
            var stage = Object.FindFirstObjectByType<StageManager>();
            stage.PlayerMotor.GetComponent<PlayerInputController>().enabled = false;
            var driver = stage.PlayerMotor.GetComponent<CharacterAnimator>();
            var renderer = stage.PlayerMotor.transform.Find("VisualRoot").GetComponent<SpriteRenderer>();
            driver.Play(state, true);
            yield return new WaitForSeconds(interval);
            string frameA = renderer.sprite.name;
            yield return new WaitForSeconds(interval);
            string frameB = renderer.sprite.name;
            yield return new WaitForSeconds(interval);
            string frameC = renderer.sprite.name;
            Assert.That(frameB, Is.Not.EqualTo(frameA), state + " did not advance from frame A");
            Assert.That(frameC, Is.Not.EqualTo(frameB), state + " did not advance from frame B");
        }
    }
}
