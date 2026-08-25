using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ThreeKingdoms.Tests.PlayMode
{
    public sealed class FinalStageFlowPlayModeTests
    {
        [UnityTest] public IEnumerator FinalStage_EntranceIsIndependentAndAllowsLowerFloor()
        {
            yield return SceneManager.LoadSceneAsync("SC_FinalStage_Entrance",LoadSceneMode.Single);yield return null;yield return null;
            StageManager stage=Object.FindFirstObjectByType<StageManager>();FinalStageBackground background=Object.FindFirstObjectByType<FinalStageBackground>();
            Assert.NotNull(stage);Assert.AreEqual(StageManager.StageMode.FinalEntrance,stage.Mode);Assert.NotNull(background);Assert.AreEqual(1,background.SectionCount);
            Assert.AreEqual(0,stage.EnemyCount);stage.SetPlayerPositionForTest(4.2f,-4f);yield return null;Assert.Less(stage.PlayerMotor.Depth,-3.8f);
            Camera camera=Camera.main;Assert.NotNull(camera);Assert.AreEqual(4.2f,camera.transform.position.x,.05f);
            Capture(camera,"Runtime_01_EntranceLowerFloor.png");
        }

        [UnityTest] public IEnumerator FinalStage_FirstDoorTransfersToTwoWaveStormGate()
        {
            yield return SceneManager.LoadSceneAsync("SC_FinalStage_Entrance",LoadSceneMode.Single);yield return null;yield return null;
            StageManager entrance=Object.FindFirstObjectByType<StageManager>();entrance.BeginEncounter(0);yield return null;Assert.AreEqual(2,entrance.EnemyCount);
            foreach(SoldierAI soldier in Object.FindObjectsByType<SoldierAI>(FindObjectsSortMode.None))Assert.AreEqual(-1.75f,soldier.transform.position.y,.25f);
            entrance.ForceClearEncounterForTest();yield return null;entrance.SetPlayerXForTest(8f);yield return new WaitForSeconds(.55f);
            Assert.AreEqual("SC_FinalStage_StormGate",SceneManager.GetActiveScene().name);
            StageManager stage=Object.FindFirstObjectByType<StageManager>();FinalStageBackground background=Object.FindFirstObjectByType<FinalStageBackground>();
            Assert.AreEqual(StageManager.StageMode.FinalApproach,stage.Mode);Assert.AreEqual(1,background.SectionCount);Assert.IsFalse(stage.BossStarted);
            stage.SetPlayerPositionForTest(4.2f,-3.5f);yield return null;Capture(Camera.main,"Runtime_02_StormGateLowerFloor.png");
        }

        [UnityTest] public IEnumerator FinalStage_TwoWavesThenDoorTransfersToExpandedBossRoom()
        {
            yield return SceneManager.LoadSceneAsync("SC_FinalStage_StormGate",LoadSceneMode.Single);yield return null;yield return null;
            StageManager stage=Object.FindFirstObjectByType<StageManager>();stage.BeginEncounter(0);yield return null;stage.ForceClearEncounterForTest();yield return null;
            stage.BeginEncounter(1);yield return null;stage.ForceClearEncounterForTest();yield return null;stage.SetPlayerXForTest(8f);
            yield return new WaitForSeconds(.55f);Assert.AreEqual("SC_FinalBoss_CaoCao",SceneManager.GetActiveScene().name);
            StageManager bossStage=Object.FindFirstObjectByType<StageManager>();FinalStageBackground background=Object.FindFirstObjectByType<FinalStageBackground>();
            Assert.AreEqual(StageManager.StageMode.FinalBoss,bossStage.Mode);Assert.AreEqual(1,background.SectionCount);
            bossStage.SetPlayerPositionForTest(4.2f,-4f);yield return null;Assert.Less(bossStage.PlayerMotor.Depth,-3.8f);
            yield return new WaitForSeconds(.4f);Assert.IsTrue(bossStage.BossStarted);Assert.NotNull(bossStage.ActiveBoss);Assert.NotNull(bossStage.ActiveBoss.GetComponent<CaoCaoBossAI>());
            Assert.AreEqual(-2.15f,bossStage.ActiveBoss.transform.position.y,.25f);
            Assert.AreEqual(-1,bossStage.ActiveBoss.GetComponent<CharacterMotor>().Facing);
            Assert.IsTrue(bossStage.ActiveBoss.transform.Find("VisualRoot").GetComponent<SpriteRenderer>().flipX);
            yield return new WaitForSeconds(2.3f);
            Capture(Camera.main,"Runtime_03_CaoCaoExpandedLowerFloor.png");
        }

        private static void Capture(Camera camera,string fileName)
        {
            string folder=@"C:\Users\shanghai\Desktop\三国战纪\Screenshots\FinalStage";Directory.CreateDirectory(folder);
            var target=new RenderTexture(1280,720,24,RenderTextureFormat.ARGB32);var texture=new Texture2D(1280,720,TextureFormat.RGBA32,false);
            RenderTexture oldActive=RenderTexture.active;RenderTexture oldTarget=camera.targetTexture;
            try{camera.targetTexture=target;RenderTexture.active=target;camera.Render();texture.ReadPixels(new Rect(0,0,1280,720),0,0);texture.Apply();File.WriteAllBytes(Path.Combine(folder,fileName),texture.EncodeToPNG());}
            finally{camera.targetTexture=oldTarget;RenderTexture.active=oldActive;Object.Destroy(texture);Object.Destroy(target);}
        }
    }
}
