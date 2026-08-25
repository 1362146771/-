using System;
using System.Collections;
using System.Linq;
using System.IO;
using UnityEngine;

namespace ThreeKingdoms
{
    public sealed class BattleEditorTestDirector : MonoBehaviour
    {
        [Header("Dummy Settings")]
        [SerializeField] private float dummyX = 1.2f;
        [SerializeField] private float dummyDepth = .15f;
        [SerializeField] private float dummyHP = 100f;
        [SerializeField] private bool dummyInvincible;
        private ActionRunner runner;
        private CharacterHealth dummy;
        private string requestedAction = "Skill1";
        private string lastHit = "NONE";

        private void Start()
        {
            GameObject player = GameObject.Find("Diaochan_ActionTester"), target = GameObject.Find("Dummy_Soldier");
            runner = player == null ? null : player.GetComponent<ActionRunner>();
            dummy = target == null ? null : target.GetComponent<CharacterHealth>();
            if (target != null) target.GetComponent<CharacterMotor>()?.SetPosition(dummyX, dummyDepth);
            dummy?.ConfigureMaximum(dummyHP);
#if UNITY_EDITOR
            string path = UnityEditor.SessionState.GetString("TK.BattleEditor.TestAction", "");
            ActionDefinition selected = string.IsNullOrEmpty(path) ? null : UnityEditor.AssetDatabase.LoadAssetAtPath<ActionDefinition>(path);
            if (selected != null) requestedAction = selected.id;
#endif
            if(Environment.GetCommandLineArgs().Contains("-battleEditorArenaCapture")){Application.runInBackground=true;StartCoroutine(CaptureArena());}
        }
        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.T) || Input.GetKeyDown(KeyCode.J)) Trigger();
            if (runner != null && runner.LastHitCount > 0) lastHit = "HIT";
        }
        public void Trigger() { if (runner != null) { lastHit = "NONE"; runner.TryPlay(requestedAction); } }
        private IEnumerator CaptureArena()
        {
            var floor=GameObject.CreatePrimitive(PrimitiveType.Quad);floor.name="TestArenaFloor";floor.transform.position=new Vector3(0,-1.35f,.5f);floor.transform.localScale=new Vector3(12,.35f,1);Shader shader=Shader.Find("Sprites/Default")??Shader.Find("Unlit/Color");var floorMaterial=new Material(shader);floorMaterial.color=new Color(.24f,.28f,.32f);floor.GetComponent<MeshRenderer>().material=floorMaterial;
            var label=new GameObject("ArenaCaptureLabel",typeof(TextMesh));label.transform.position=new Vector3(-5.8f,4.55f,0);var text=label.GetComponent<TextMesh>();text.text="BATTLE EDITOR TEST ARENA   •   Skill1\nT / J = TEST ACTION     Foot / Hurtbox / Hitbox Debug";text.fontSize=42;text.characterSize=.055f;text.color=new Color(1f,.82f,.28f);
            requestedAction="Skill1";yield return new WaitForSecondsRealtime(.5f);Trigger();yield return new WaitForSecondsRealtime(.28f);yield return new WaitForEndOfFrame();
            const string path=@"C:\Users\shanghai\Desktop\三国战纪\Screenshots\BattleEditorV01\14_test_arena.png";Directory.CreateDirectory(Path.GetDirectoryName(path));Camera camera=FindFirstObjectByType<Camera>();var rt=new RenderTexture(1440,810,24);camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;var image=new Texture2D(1440,810,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1440,810),0,0);image.Apply();File.WriteAllBytes(path,image.EncodeToPNG());camera.targetTexture=null;RenderTexture.active=null;Destroy(rt);Destroy(image);yield return null;Application.Quit(0);
        }
        private void OnGUI()
        {
            GUI.Box(new Rect(16,16,340,178),GUIContent.none);GUI.Label(new Rect(28,25,310,26),"THREE KINGDOMS • ACTION TEST ARENA");
            string action=runner!=null&&runner.IsPlaying?runner.Current.id:requestedAction;int frame=runner==null?0:runner.CurrentFrame;
            GUI.Label(new Rect(28,55,310,22),"Current Action: "+action);GUI.Label(new Rect(28,78,310,22),"Current Frame: "+frame);
            GUI.Label(new Rect(28,101,310,22),"Hit Result: "+lastHit);GUI.Label(new Rect(28,124,310,22),"Dummy HP: "+(dummy==null?"—":dummy.Current.ToString("0"))+(dummyInvincible?" (Invincible)":""));
            GUI.Label(new Rect(28,148,310,22),"Press T / J to test selected action");
            if(runner!=null&&runner.IsPlaying)
            {
                Camera camera=FindFirstObjectByType<Camera>();ActionFrameShape shape=null;foreach(ActionFrameShape candidate in runner.Current.ShapesAt(runner.CurrentFrame))if(candidate.role!=ActionShapeRole.Hurtbox){shape=candidate;break;}
                if(camera!=null&&shape!=null){Bounds b=ActionGeometry.GetBounds(shape,runner.GetComponent<CharacterMotor>()?.Facing??1);Vector3 min=camera.WorldToScreenPoint(runner.transform.position+(Vector3)b.min),max=camera.WorldToScreenPoint(runner.transform.position+(Vector3)b.max);Rect screen=new Rect(min.x,Screen.height-max.y,max.x-min.x,max.y-min.y);Color old=GUI.color;GUI.color=new Color(.1f,.65f,1f,.28f);GUI.DrawTexture(screen,Texture2D.whiteTexture);GUI.color=old;GUI.Box(screen,"CURRENT HITBOX");}
            }
        }
    }
}
