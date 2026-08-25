using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ThreeKingdoms
{
    public sealed partial class SoldierAI : MonoBehaviour
    {
        public enum State { Idle, Detect, Approach, AlignDepth, AttackSelect, Startup, Attack=Startup, Active, Recovery, Cooldown, Reposition, Dead }
        [SerializeField] private float detectionRange=8f, attackOpportunityX=2.8f, attackAlignDepth=.78f;
        [SerializeField] private float cooldownJitterMin=.18f, cooldownJitterMax=.62f, repositionDuration=.42f;
        [SerializeField] private AttackData attack01Data;
        private CharacterMotor motor; private CharacterAnimator animationDriver; private CharacterCombat combat; private CharacterHealth health;
        private Transform target; private float idleUntil,spawnGraceUntil,cooldownRemaining,repositionRemaining,attackElapsed;
        private AttackData selectedAttack;
        private int repositionSign;
        public State CurrentState { get; private set; }=State.Idle;
        public float LastXDistance { get; private set; }
        public float LastDepthDistance { get; private set; }
        public string SelectedAttackId=>selectedAttack==null?string.Empty:selectedAttack.actionId;
        public float AttackElapsed=>attackElapsed;
        public float CooldownRemaining=>cooldownRemaining;

        private void Awake()
        {
            motor=GetComponent<CharacterMotor>(); animationDriver=GetComponent<CharacterAnimator>(); combat=GetComponent<CharacterCombat>(); health=GetComponent<CharacterHealth>();
            health.Died += _ => CurrentState=State.Dead;idleUntil=Time.time+UnityEngine.Random.Range(.2f,.8f);spawnGraceUntil=Time.time+.35f;
            cooldownRemaining=0f;
        }
        private void Update(){if(Time.time<spawnGraceUntil){CurrentState=State.Idle;animationDriver.Play("Idle");return;}Tick(Time.deltaTime);}
        public void SetTarget(Transform value)=>target=value;
        public void PrepareWallEntrance(Transform value,float delay)
        {
            target=value;spawnGraceUntil=Time.time+Mathf.Max(.35f,delay);
            if(target==null)return;
            int facing=target.position.x>=transform.position.x?1:-1;motor.Face(facing);animationDriver.Face(facing);animationDriver.Play("Idle",true);
        }

        public void Tick(float deltaTime)
        {
            if(CurrentState==State.Dead||health.IsDead)return;
            if(target==null){var p=FindFirstObjectByType<PlayerInputController>();if(p!=null)target=p.transform;if(target==null)return;}
            LastXDistance=Mathf.Abs(target.position.x-transform.position.x); LastDepthDistance=Mathf.Abs(target.position.y-transform.position.y);

            if(CurrentState==State.Startup||CurrentState==State.Active||CurrentState==State.Recovery){TickAttackLifecycle(deltaTime);return;}
            if(cooldownRemaining>0f){cooldownRemaining=Mathf.Max(0f,cooldownRemaining-deltaTime);CurrentState=State.Cooldown;animationDriver.Play("Idle");return;}
            if(repositionRemaining>0f)
            {
                repositionRemaining=Mathf.Max(0f,repositionRemaining-deltaTime);CurrentState=State.Reposition;
                MoveToward(new Vector2(-Mathf.Sign(target.position.x-transform.position.x)*.32f,repositionSign));return;
            }
            if(LastXDistance>detectionRange){CurrentState=State.Idle;animationDriver.Play(Time.time>idleUntil?"IdleLong":"Idle");return;}
            CurrentState=State.Detect;
            if(LastDepthDistance>attackAlignDepth){CurrentState=State.AlignDepth;MoveToward(new Vector2(Mathf.Sign(target.position.x-transform.position.x)*.18f,Mathf.Sign(target.position.y-transform.position.y)));return;}
            if(LastXDistance>Mathf.Min(attackOpportunityX,2.8f)){CurrentState=State.Approach;MoveToward(new Vector2(Mathf.Sign(target.position.x-transform.position.x),Mathf.Sign(target.position.y-transform.position.y)*.20f));return;}
            CurrentState=State.AttackSelect;BeginAttack(SelectAttack());
        }

        private AttackData SelectAttack()
        {
            return attack01Data;
        }

        private bool BeginAttack(AttackData data)
        {
            if(data==null||LastDepthDistance>data.rangeDepth||LastXDistance>data.rangeX)return false;
            int facing=target.position.x>=transform.position.x?1:-1;motor.Face(facing);animationDriver.Face(facing);
            if(!combat.RequestEnemyAttack(data))return false;
            selectedAttack=data;attackElapsed=0f;CurrentState=State.Startup;return true;
        }

        private void TickAttackLifecycle(float deltaTime)
        {
            attackElapsed+=Mathf.Max(0f,deltaTime);
            if(attackElapsed<selectedAttack.startup){CurrentState=State.Startup;return;}
            if(attackElapsed<selectedAttack.startup+selectedAttack.active){CurrentState=State.Active;return;}
            if(attackElapsed<selectedAttack.Duration){CurrentState=State.Recovery;return;}
            cooldownRemaining=selectedAttack.cooldown+UnityEngine.Random.Range(cooldownJitterMin,cooldownJitterMax)+(transform.GetSiblingIndex()%4)*.03f;
            repositionRemaining=repositionDuration+UnityEngine.Random.Range(-.08f,.16f);repositionSign=UnityEngine.Random.value<.5f?-1:1;
            CurrentState=State.Cooldown;
        }

        public bool BeginAttackForTest(string action)
        {
            AttackData data=AttackDataForTest(action);
            LastXDistance=data==null?float.MaxValue:Mathf.Min(1.2f,data.rangeX);LastDepthDistance=0f;return BeginAttack(data);
        }
        public AttackData AttackDataForTest(string action)=>action=="Attack01"?attack01Data:null;

        private void MoveToward(Vector2 input){motor.Move(input,false);animationDriver.Face(motor.Facing);animationDriver.Play("Walk");}
    }

    [RequireComponent(typeof(Camera))]
    public sealed partial class BeatEmUpCamera : MonoBehaviour
    {
        [SerializeField] private float minX=4.2f,maxX=36f,followSharpness=5f;
        [SerializeField] private float cameraY=.15f,depthFollow=.06f;
        private Transform target; private bool locked; private float lockX;
        public bool IsLocked=>locked;
        private void Awake(){var c=GetComponent<Camera>();c.orthographic=true;c.orthographicSize=5.1f;c.backgroundColor=new Color(.18f,.24f,.30f);c.clearFlags=CameraClearFlags.SolidColor;}
        public void SetTarget(Transform value)
        {
            target=value;if(target==null)return;
            transform.position=new Vector3(Mathf.Clamp(target.position.x+2.2f,Mathf.Max(4.2f,minX),maxX),cameraY+target.position.y*depthFollow,-10f);
        }
        public void SetLock(bool value,float x){locked=value;if(value)lockX=x;}
        private void LateUpdate()
        {
            if(target==null)return;
            float x=locked?lockX:Mathf.Clamp(target.position.x+2.2f,Mathf.Max(4.2f,minX),maxX);
            Vector3 desired=new Vector3(x,cameraY+target.position.y*depthFollow,-10f);
            transform.position=Vector3.Lerp(transform.position,desired,1f-Mathf.Exp(-followSharpness*Time.deltaTime));
        }
    }

    public sealed partial class StageVisualBuilder : MonoBehaviour
    {
        [SerializeField] private Sprite[] backgroundSections;
        [SerializeField] private Sprite[] midgroundSections;
        [SerializeField] private Sprite[] gameplaySections;
        [SerializeField] private Sprite[] foregroundSections;
        [SerializeField] private float firstCenterX=3.55f,sectionWorldWidth=17.1f,worldHeight=10.2f,bottomY=-5.1f;

        public int SectionCount => backgroundSections == null ? 0 : backgroundSections.Length;
        private void Awake(){if(enabled)Build();}

        private void Build()
        {
            Transform bg=FindOrCreate("Background"),mid=FindOrCreate("Midground"),game=FindOrCreate("Gameplay"),fg=FindOrCreate("Foreground");
            Clear(bg);Clear(mid);Clear(game);Clear(fg);
            if(!ValidLayers()){Debug.LogError("Iteration 02 fortress background sprites are not wired.",this);return;}

            float verticalScale=worldHeight/10.86f;
            BuildBand(game,gameplaySections,"Ground",firstCenterX,sectionWorldWidth,0f,verticalScale,-100,1f);
            BuildBand(bg,backgroundSections,"BG",firstCenterX,sectionWorldWidth,3.10f,verticalScale,-90,.10f);
            BuildBand(mid,midgroundSections,"Mid",firstCenterX,sectionWorldWidth,.35f,verticalScale,-80,.10f);
            BuildBand(fg,foregroundSections,"FG",firstCenterX,sectionWorldWidth,-4.49f,verticalScale,1600,.20f);

            ConfigureParallax(bg,.18f);ConfigureParallax(mid,.09f);ConfigureParallax(game,0f);ConfigureParallax(fg,-.04f);
        }

        private Transform FindOrCreate(string name){Transform f=transform.Find(name);if(f!=null)return f;var g=new GameObject(name);g.transform.SetParent(transform,false);return g.transform;}
        private bool ValidLayers()=>backgroundSections!=null&&midgroundSections!=null&&gameplaySections!=null&&foregroundSections!=null&&backgroundSections.Length==3&&midgroundSections.Length==3&&gameplaySections.Length==3&&foregroundSections.Length==3;
        private static void Clear(Transform parent){for(int i=parent.childCount-1;i>=0;i--)Destroy(parent.GetChild(i).gameObject);}
        private void BuildBand(Transform parent,Sprite[] sprites,string prefix,float firstX,float spacing,float y,float verticalScale,int order,float alpha)
        {
            for(int slot=0;slot<sprites.Length;slot++)
            {
                int spriteIndex=slot;
                var go=new GameObject(prefix+"_Section_"+(slot+1));go.transform.SetParent(parent,false);
                float horizontalScale=sectionWorldWidth/sprites[spriteIndex].bounds.size.x;
                go.transform.position=new Vector3(firstX+spacing*slot,prefix=="Ground"?bottomY+worldHeight*.5f:y,0);go.transform.localScale=new Vector3(horizontalScale,verticalScale,1f);
                var renderer=go.AddComponent<SpriteRenderer>();renderer.sprite=sprites[spriteIndex];renderer.sortingOrder=order;renderer.color=new Color(1f,1f,1f,alpha);
            }
        }
        private static void ConfigureParallax(Transform layer,float cameraFollow){var p=layer.GetComponent<ParallaxLayer>();if(p==null)p=layer.gameObject.AddComponent<ParallaxLayer>();p.Configure(cameraFollow);}
    }

    [Serializable] public sealed class WaveDefinition{public int enemyCount;public float healthMultiplier=1f;public float triggerX;}
    public sealed partial class SpawnPoint:MonoBehaviour{}
    public sealed class EncounterController
    {
        public int Index{get;} public bool Active{get;private set;} public bool Completed{get;private set;} public int Remaining{get;private set;}
        public EncounterController(int index)=>Index=index;
        public void Begin(int count){Active=true;Remaining=count;}
        public void EnemyDefeated(){Remaining=Mathf.Max(0,Remaining-1);if(Remaining==0){Active=false;Completed=true;}}
    }

    public sealed partial class StageManager : MonoBehaviour
    {
        public enum StageMode { LegacyFullStage, FinalEntrance, FinalApproach, FinalBoss }
        [SerializeField] private GameObject playerPrefab,soldierPrefab,bossPrefab;
        [SerializeField] private BeatEmUpCamera stageCamera;
        [SerializeField] private float exitX=39f,bossTriggerX=36.5f;
        [SerializeField] private StageMode stageMode;
        [SerializeField] private string nextSceneName="SC_FinalBoss_CaoCao";
        [SerializeField] private float portalTriggerX=28.8f,playerSpawnX;
        [SerializeField] private float playerSpawnDepth=-.6f,depthMin=-2.55f,depthMax=-.24f,enemySpawnDepth=-.3f,bossSpawnDepth=-1.95f,arenaCenterX=4.2f;
        private readonly List<CharacterHealth> activeEnemies=new List<CharacterHealth>();
        private readonly EncounterController[] encounters={new EncounterController(0),new EncounterController(1),new EncounterController(2)};
        private readonly float[] triggerX={7f,18f,29f}; private readonly int[] waveCount={2,3,3};
        private int nextEncounter; private CharacterMotor player;
        private CharacterHealth activeBoss;
        public CharacterHealth PlayerHealth{get;private set;}
        public CharacterHealth ActiveBoss=>activeBoss;
        public int EnemyCount=>activeEnemies.Count;
        public bool CameraLocked{get;private set;}
        public bool StageClear{get;private set;}
        public bool BossStarted{get;private set;}
        public bool BossDefeated{get;private set;}
        public int CompletedEncounters=>nextEncounter-(CameraLocked?1:0);
        public CharacterMotor PlayerMotor=>player;

        private bool loadingNextScene;
        public StageMode Mode=>stageMode;
        private void Start(){SpawnPlayer();if(stageMode==StageMode.FinalBoss)StartCoroutine(BeginFinalBoss());}
        private void Update()
        {
            activeEnemies.RemoveAll(item=>item==null||item.IsDead||!item.gameObject.activeInHierarchy);
            if(stageMode==StageMode.FinalBoss){if(BossDefeated&&!CameraLocked)StageClear=true;return;}
            if(CameraLocked&&!BossStarted&&activeEnemies.Count==0)CompleteEncounter();
            int encounterLimit=stageMode==StageMode.FinalEntrance?1:stageMode==StageMode.FinalApproach?2:encounters.Length;
            if(!CameraLocked&&nextEncounter<encounterLimit&&player!=null&&player.X>=EncounterTrigger(nextEncounter))BeginEncounter(nextEncounter);
            if(stageMode==StageMode.FinalEntrance||stageMode==StageMode.FinalApproach)
            {
                if(!CameraLocked&&nextEncounter>=encounterLimit&&player!=null&&player.X>=portalTriggerX&&!loadingNextScene)StartCoroutine(EnterNextRoom());
                return;
            }
            if(!CameraLocked&&!BossStarted&&nextEncounter>=encounters.Length&&player!=null&&player.X>=bossTriggerX)BeginBossEncounter();
            if(BossDefeated&&!CameraLocked)StageClear=true;
        }

        private void SpawnPlayer()
        {
            var go=Instantiate(playerPrefab,new Vector3(playerSpawnX,playerSpawnDepth,0),Quaternion.identity);go.name="Diaochan_Player";
            player=go.GetComponent<CharacterMotor>();PlayerHealth=go.GetComponent<CharacterHealth>();player.SetDepthBounds(depthMin,depthMax);player.SetHorizontalBounds(-1,exitX+1);
            go.GetComponent<CharacterAnimator>().Play("Entrance",true);
            go.GetComponent<PlayerInputController>()?.LockFor(.95f);
            if(stageCamera==null)stageCamera=FindFirstObjectByType<BeatEmUpCamera>();stageCamera?.SetTarget(go.transform);
        }

        public void BeginEncounter(int index)
        {
            if(CameraLocked||index!=nextEncounter||soldierPrefab==null)return;
            bool fixedFinalRoom=stageMode==StageMode.FinalEntrance||stageMode==StageMode.FinalApproach;
            CameraLocked=true;encounters[index].Begin(waveCount[index]);float center=fixedFinalRoom?arenaCenterX:triggerX[index]+2.2f,trigger=EncounterTrigger(index);
            player.SetHorizontalBounds(fixedFinalRoom?-1f:trigger-3.2f,fixedFinalRoom?exitX+1f:trigger+5f);stageCamera?.SetLock(true,center);activeEnemies.Clear();
            int count=waveCount[index];const float wallSpread=4.25f;
            for(int i=0;i<count;i++)
            {
                float t=count<=1?.5f:(float)i/(count-1),x=Mathf.Lerp(center-wallSpread,center+wallSpread,t);
                var enemy=Instantiate(soldierPrefab,new Vector3(x,enemySpawnDepth,0),Quaternion.identity);enemy.name="Soldier_E"+(index+1)+"_"+(i+1);
                var health=enemy.GetComponent<CharacterHealth>();if(index==2)health.ConfigureMaximum(120);health.Died+=OnEnemyDied;
                enemy.GetComponent<SoldierAI>()?.PrepareWallEntrance(player.transform,.65f+i*.18f);activeEnemies.Add(health);
            }
        }

        private void OnEnemyDied(CharacterHealth health){if(health!=null)health.Died-=OnEnemyDied;activeEnemies.Remove(health);if(CameraLocked&&activeEnemies.Count==0)CompleteEncounter();}
        private void CompleteEncounter()
        {
            if(!CameraLocked)return;
            while(encounters[nextEncounter].Remaining>0)encounters[nextEncounter].EnemyDefeated();
            nextEncounter++;CameraLocked=false;player.SetHorizontalBounds(-1,exitX+1);stageCamera?.SetLock(false,0);
        }
        private void BeginBossEncounter()
        {
            if(BossStarted||bossPrefab==null)return;bool finalRoom=stageMode==StageMode.FinalBoss;float arenaCenter=finalRoom?4.2f:36f,bossX=finalRoom?6.3f:38f;
            BossStarted=true;CameraLocked=true;player.SetHorizontalBounds(finalRoom?-1f:bossTriggerX-4.5f,finalRoom?9.2f:exitX+1f);stageCamera?.SetLock(true,arenaCenter);activeEnemies.Clear();
            var boss=Instantiate(bossPrefab,new Vector3(bossX,bossSpawnDepth,0f),Quaternion.identity);boss.name="Boss_CaoCao";activeBoss=boss.GetComponent<CharacterHealth>();
            if(activeBoss==null){Debug.LogError("Cao Cao boss prefab has no CharacterHealth.",boss);return;}
            activeBoss.Died+=OnBossDied;boss.GetComponent<CaoCaoBossAI>()?.SetTarget(player.transform);activeEnemies.Add(activeBoss);
        }
        private IEnumerator BeginFinalBoss(){yield return null;yield return new WaitForSeconds(.25f);BeginBossEncounter();}
        private IEnumerator EnterNextRoom()
        {
            loadingNextScene=true;player?.SetHorizontalBounds(portalTriggerX-.25f,portalTriggerX+.25f);
            yield return new WaitForSeconds(.18f);
            if(!string.IsNullOrWhiteSpace(nextSceneName))SceneManager.LoadScene(nextSceneName);
        }
        private void OnBossDied(CharacterHealth boss)
        {
            if(boss!=null)boss.Died-=OnBossDied;activeEnemies.Remove(boss);StartCoroutine(CompleteBossAfterDeath());
        }
        private IEnumerator CompleteBossAfterDeath()
        {
            yield return new WaitForSeconds(3.6f);BossDefeated=true;CameraLocked=false;player.SetHorizontalBounds(-1,exitX+1);stageCamera?.SetLock(false,0);
        }
        public void ForceClearEncounterForTest()
        {
            if(BossStarted&&!BossDefeated&&activeBoss!=null){activeBoss.KillForTest();return;}
            foreach(var e in activeEnemies.ToArray())e?.KillForTest();activeEnemies.Clear();if(CameraLocked)CompleteEncounter();
        }
        public void BeginBossForTest()=>BeginBossEncounter();
        public void ForceCompleteBossForTest()
        {
            if(!BossStarted)BeginBossEncounter();if(activeBoss!=null&&!activeBoss.IsDead)activeBoss.KillForTest();BossDefeated=true;CameraLocked=false;StageClear=true;player?.SetHorizontalBounds(-1,exitX+1);stageCamera?.SetLock(false,0);
        }
        public void SetPlayerXForTest(float x)=>player?.SetPosition(x,player.Depth);
        public void SetPlayerPositionForTest(float x,float depth)=>player?.SetPosition(x,depth);
        private float EncounterTrigger(int index)=>(stageMode==StageMode.FinalEntrance||stageMode==StageMode.FinalApproach)?(index==0?1.6f:4.8f):triggerX[index];
    }
}
