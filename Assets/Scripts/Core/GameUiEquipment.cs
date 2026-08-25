using UnityEngine;

namespace ThreeKingdoms
{
    public sealed class HitCounterState
    {
        public int Count{get;private set;}
        public float LastHitAt{get;private set;}=-1000f;
        public bool Visible=>Count>0;
        public void Register(float now,int amount=1){if(amount<1)return;Count+=amount;LastHitAt=now;}
        public bool Tick(float now,float timeout){if(Count<=0||now-LastHitAt<=Mathf.Max(.05f,timeout))return false;Reset();return true;}
        public void Reset(){Count=0;LastHitAt=-1000f;}
    }

    public sealed partial class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }
        private void Awake(){if(Instance!=null&&Instance!=this){Destroy(gameObject);return;}Instance=this;}
    }

    public sealed partial class StageHud : MonoBehaviour
    {
        [SerializeField] private StageManager stage;
        [SerializeField] private Texture2D playerFrame,hpFill,statusPlaque;
        [SerializeField, Range(1f, 1.5f)] private float stageClearBannerDuration=1.35f;
        [SerializeField, Range(.5f, 3f)] private float hitCounterTimeout=1.6f;
        private float stageClearStartedAt=-1f;
        private Transform hudRoot;
        private SpriteRenderer hpRenderer;
        private TextMesh nameText,statusText,hitText,hitShadowText;
        private Transform bossBarRoot;
        private SpriteRenderer bossHpRenderer;
        private TextMesh bossNameText;
        private Vector3 bossHpFullScale;
        private readonly HitCounterState hitCounter=new HitCounterState();
        private Vector3 hpFullScale;
        private float hpCenterX;
        public bool HudBuilt=>hudRoot!=null&&hpRenderer!=null;
        public float DisplayedHealthRatio{get;private set;}=1f;
        public int HitCount=>hitCounter.Count;
        public bool HitCounterVisible=>hitCounter.Visible;
        private void Awake(){if(stage==null)stage=FindFirstObjectByType<StageManager>();}
        private void OnEnable()=>CharacterHealth.DamageApplied+=OnDamageApplied;
        private void OnDisable()=>CharacterHealth.DamageApplied-=OnDamageApplied;
        private void Start()=>BuildArcadeHud();
        private void Update()
        {
            UpdateHitCounter();
            if(stage!=null&&stage.StageClear&&stageClearStartedAt<0f)stageClearStartedAt=Time.unscaledTime;
            else if(stage!=null&&!stage.StageClear)stageClearStartedAt=-1f;
            if(!HudBuilt||stage==null)return;
            DisplayedHealthRatio=stage.PlayerHealth==null?1f:Mathf.Clamp01(stage.PlayerHealth.Current/stage.PlayerHealth.Maximum);
            hpRenderer.transform.localScale=new Vector3(hpFullScale.x*DisplayedHealthRatio,hpFullScale.y,hpFullScale.z);
            float fullWidth=hpFill.width/100f*hpFullScale.x;hpRenderer.transform.localPosition=new Vector3(hpCenterX-fullWidth*(1f-DisplayedHealthRatio)*.5f,3.87f,10f);
            UpdateBossHud();
            if(stage.StageClear)
            {
                bool banner=stageClearStartedAt<0f||Time.unscaledTime-stageClearStartedAt<stageClearBannerDuration;
                statusText.text=banner?"STAGE CLEAR":"DEMO COMPLETE";
            }
            else statusText.text=stage.CameraLocked?"ENCOUNTER  •  ENEMIES "+stage.EnemyCount:"ENEMIES  "+stage.EnemyCount;
        }

        private void BuildArcadeHud()
        {
            var camera=Camera.main;if(camera==null||playerFrame==null||hpFill==null||statusPlaque==null){Debug.LogError("Iteration03 HUD assets are not wired.",this);return;}
            hudRoot=new GameObject("Iteration03_ArcadeHud").transform;hudRoot.SetParent(camera.transform,false);
            CreateSprite("PlayerHudFrame",playerFrame,new Vector3(-5.50f,4.00f,10f),new Vector3(.30f,.30f,1f),5000);
            hpRenderer=CreateSprite("PlayerHpFill",hpFill,new Vector3(-5.13f,3.87f,10f),new Vector3(.20f,.10f,1f),5001);hpFullScale=hpRenderer.transform.localScale;hpCenterX=hpRenderer.transform.localPosition.x;
            CreateSprite("StatusPlaque",statusPlaque,new Vector3(7.05f,4.43f,10f),new Vector3(.16f,.12f,1f),5000);
            nameText=CreateText("PlayerName","DIAOCHAN",new Vector3(-5.95f,4.57f,10f),.045f,5002,new Color(1f,.82f,.28f));
            statusText=CreateText("StatusText","ENEMIES  0",new Vector3(7.05f,4.43f,10f),.038f,5002,new Color(1f,.88f,.48f));
            hitShadowText=CreateText("HitCounterShadow","",new Vector3(-5.57f,2.59f,10f),.060f,5002,new Color(0f,0f,0f,.85f));hitShadowText.anchor=TextAnchor.MiddleLeft;hitShadowText.alignment=TextAlignment.Left;
            hitText=CreateText("HitCounter","",new Vector3(-5.61f,2.63f,10f),.060f,5003,new Color(1f,.92f,.70f));hitText.anchor=TextAnchor.MiddleLeft;hitText.alignment=TextAlignment.Left;
            bossBarRoot=new GameObject("CaoCaoBossHud").transform;bossBarRoot.SetParent(hudRoot,false);bossBarRoot.gameObject.SetActive(false);
            CreateSolidSprite("BossHpBack",new Color(.12f,.03f,.03f,.92f),new Vector3(0f,4.22f,10f),new Vector3(4.1f,.13f,1f),5000,bossBarRoot);
            bossHpRenderer=CreateSolidSprite("BossHpFill",new Color(.82f,.08f,.05f,1f),new Vector3(0f,4.22f,10f),new Vector3(4.0f,.085f,1f),5001,bossBarRoot);bossHpFullScale=bossHpRenderer.transform.localScale;
            bossNameText=CreateText("BossName","CAO CAO  •  PHASE 1",new Vector3(0f,4.48f,10f),.035f,5002,new Color(1f,.78f,.30f));bossNameText.transform.SetParent(bossBarRoot,true);
        }

        private void UpdateBossHud()
        {
            if(bossBarRoot==null||stage==null)return;CharacterHealth boss=stage.ActiveBoss;bool visible=stage.BossStarted&&!stage.BossDefeated&&boss!=null;
            bossBarRoot.gameObject.SetActive(visible);if(!visible)return;float ratio=Mathf.Clamp01(boss.Current/boss.Maximum);
            bossHpRenderer.transform.localScale=new Vector3(bossHpFullScale.x*ratio,bossHpFullScale.y,bossHpFullScale.z);
            bossHpRenderer.transform.localPosition=new Vector3(-2f*(1f-ratio),4.22f,10f);var ai=boss.GetComponent<CaoCaoBossAI>();bossNameText.text="CAO CAO  •  PHASE "+(ai==null?1:ai.Phase);
        }

        private void OnDamageApplied(DamageAppliedEvent hit)
        {
            if(hit.source==null||hit.source.Team!=CharacterTeam.Player)return;
            hitCounter.Register(Time.unscaledTime);RefreshHitCounter(true);
        }
        private void UpdateHitCounter()
        {
            if(hitCounter.Tick(Time.unscaledTime,hitCounterTimeout)){RefreshHitCounter(false);return;}
            if(!hitCounter.Visible||hitText==null)return;
            float age=Time.unscaledTime-hitCounter.LastHitAt,fade=Mathf.Clamp01((hitCounterTimeout-age)/.35f),pulse=1f+Mathf.Clamp01(.16f-age)*.8f;
            hitText.color=new Color(1f,.92f,.70f,fade);hitShadowText.color=new Color(0f,0f,0f,.85f*fade);hitText.transform.localScale=Vector3.one*pulse;hitShadowText.transform.localScale=Vector3.one*pulse;
        }
        private void RefreshHitCounter(bool pulse)
        {
            string value=hitCounter.Visible?hitCounter.Count+" HIT":"";if(hitText!=null){hitText.text=value;hitText.transform.localScale=Vector3.one*(pulse?1.28f:1f);}if(hitShadowText!=null){hitShadowText.text=value;hitShadowText.transform.localScale=Vector3.one*(pulse?1.28f:1f);}
        }

        private SpriteRenderer CreateSprite(string objectName,Texture2D texture,Vector3 position,Vector3 scale,int order)
        {
            var go=new GameObject(objectName);go.transform.SetParent(hudRoot,false);go.transform.localPosition=position;go.transform.localScale=scale;
            var renderer=go.AddComponent<SpriteRenderer>();renderer.sprite=Sprite.Create(texture,new Rect(0,0,texture.width,texture.height),new Vector2(.5f,.5f),100f);renderer.sortingOrder=order;return renderer;
        }

        private SpriteRenderer CreateSolidSprite(string objectName,Color color,Vector3 position,Vector3 scale,int order,Transform parent)
        {
            var texture=new Texture2D(1,1,TextureFormat.RGBA32,false){name=objectName+"Texture",filterMode=FilterMode.Point};texture.SetPixel(0,0,Color.white);texture.Apply();
            var go=new GameObject(objectName);go.transform.SetParent(parent,false);go.transform.localPosition=position;go.transform.localScale=scale;
            var renderer=go.AddComponent<SpriteRenderer>();renderer.sprite=Sprite.Create(texture,new Rect(0,0,1,1),new Vector2(.5f,.5f),1f);renderer.color=color;renderer.sortingOrder=order;return renderer;
        }

        private TextMesh CreateText(string objectName,string value,Vector3 position,float characterSize,int order,Color color)
        {
            var go=new GameObject(objectName);go.transform.SetParent(hudRoot,false);go.transform.localPosition=position;
            var text=go.AddComponent<TextMesh>();text.text=value;text.anchor=TextAnchor.MiddleCenter;text.alignment=TextAlignment.Center;text.fontSize=64;text.characterSize=characterSize;text.fontStyle=FontStyle.Bold;text.color=color;
            text.GetComponent<MeshRenderer>().sortingOrder=order;return text;
        }
    }

    public enum EquipmentSlotType{MainHand,OffHand}

    [CreateAssetMenu(menuName="ThreeKingdoms/Weapon Definition")]
    public sealed partial class WeaponDefinition:ScriptableObject
    {
        public string weaponType;
        public bool legacyBakedWeaponVisual=true;
    }

    [CreateAssetMenu(menuName="ThreeKingdoms/Moveset Definition")]
    public sealed partial class MovesetDefinition:ScriptableObject
    {
        public WeaponDefinition weapon;
        public AttackDefinition[] attacks;
    }

    public sealed partial class EquipmentSlot:MonoBehaviour
    {
        [SerializeField] private EquipmentSlotType slotType=EquipmentSlotType.MainHand;
        [SerializeField] private WeaponDefinition equippedWeapon;
        public WeaponDefinition EquippedWeapon=>equippedWeapon;
        public void Equip(WeaponDefinition weapon)=>equippedWeapon=weapon;
    }
}
