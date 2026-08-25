using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ThreeKingdoms.Tests.PlayMode
{
    public sealed class AgentGatePlayModeTests
    {
        [UnityTest]
        public IEnumerator EntersPlayModeAndLoadsGateObjects()
        {
            yield return SceneManager.LoadSceneAsync("AgentGate", LoadSceneMode.Single);
            yield return null;
            Assert.That(Application.isPlaying, Is.True);
            Assert.That(GameObject.Find("GateRoot"), Is.Not.Null);
            Assert.That(GameObject.Find("GateVisual"), Is.Not.Null);
            Assert.That(Camera.main, Is.Not.Null);
        }
    }
}
