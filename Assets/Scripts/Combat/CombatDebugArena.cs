using System.Collections.Generic;
using UnityEngine;

namespace ThreeKingdoms
{
    [DisallowMultipleComponent]
    public sealed class CombatDebugArena : MonoBehaviour
    {
        [Header("Scene Objects")]
        [SerializeField] private CharacterCombat playerCombat;
        [SerializeField] private CharacterHealth dummyHealth;
        [SerializeField] private Transform dummyTransform;
        [Header("Display")]
        [SerializeField] private bool showHurtboxes = true;
        [SerializeField] private bool showHitboxes = true;
        [SerializeField, Min(.01f)] private float lineWidth = .035f;

        private readonly List<LineRenderer> lines = new List<LineRenderer>();
        private readonly List<FloatingDamage> floatingDamage = new List<FloatingDamage>();
        private ActionRunner playerRunner;
        private Hurtbox playerHurtbox, dummyHurtbox;
        private CharacterMotor dummyMotor;
        private Vector2 dummyAnchor;
        private Material lineMaterial;
        private int usedLines, hitCount;
        private float lastDamage, totalDamage;
        private GUIStyle titleStyle, labelStyle, damageStyle;

        private sealed class FloatingDamage
        {
            public float amount, createdAt;
            public Vector3 world;
        }

        private void Awake()
        {
            Camera arenaCamera=Camera.main??FindAnyObjectByType<Camera>();
            if(arenaCamera!=null){arenaCamera.orthographic=true;arenaCamera.orthographicSize=4.8f;arenaCamera.transform.position=new Vector3(0f,1.35f,-10f);arenaCamera.clearFlags=CameraClearFlags.SolidColor;arenaCamera.backgroundColor=new Color(.035f,.055f,.085f);}
            BuildArenaBackdrop();
            if(playerCombat==null)playerCombat=FindFirstObjectByType<PlayerInputController>()?.GetComponent<CharacterCombat>();
            if(dummyHealth==null)
            {
                GameObject found=GameObject.Find("Infinite_Dummy_Soldier");
                if(found!=null)dummyHealth=found.GetComponent<CharacterHealth>();
            }
            playerRunner=playerCombat==null?null:playerCombat.UnifiedRunner;
            playerHurtbox=playerCombat==null?null:playerCombat.GetComponentInChildren<Hurtbox>(true);
            dummyHurtbox=dummyHealth==null?null:dummyHealth.GetComponentInChildren<Hurtbox>(true);
            dummyTransform=dummyHealth==null?dummyTransform:dummyHealth.transform;
            dummyMotor=dummyHealth==null?null:dummyHealth.GetComponent<CharacterMotor>();
            if(dummyMotor!=null)dummyAnchor=new Vector2(dummyMotor.X,dummyMotor.Depth);
            if(dummyHealth!=null)dummyHealth.ConfigureMaximum(1000000f,true);
            Shader shader=Shader.Find("Sprites/Default")??Shader.Find("Unlit/Color");
            if(shader!=null)lineMaterial=new Material(shader){name="CombatDebugLineMaterial"};
        }

        private void BuildArenaBackdrop()
        {
            if(GameObject.Find("Runtime_DebugFloor")!=null)return;
            var texture=new Texture2D(1,1,TextureFormat.RGBA32,false){name="CombatDebugFloorPixel",filterMode=FilterMode.Point};texture.SetPixel(0,0,new Color(.20f,.23f,.27f));texture.Apply();
            var floor=new GameObject("Runtime_DebugFloor",typeof(SpriteRenderer));floor.transform.position=new Vector3(0f,-1.25f,1f);floor.transform.localScale=new Vector3(14f,.22f,1f);
            var renderer=floor.GetComponent<SpriteRenderer>();renderer.sprite=Sprite.Create(texture,new Rect(0,0,1,1),new Vector2(.5f,.5f),1f);renderer.sortingOrder=-1000;
        }

        private void OnEnable()=>CharacterHealth.DamageApplied+=OnDamageApplied;
        private void OnDisable()=>CharacterHealth.DamageApplied-=OnDamageApplied;
        private void OnDestroy(){if(lineMaterial!=null)Destroy(lineMaterial);}

        private void OnDamageApplied(DamageAppliedEvent hit)
        {
            if(dummyHealth==null||hit.target!=dummyHealth)return;
            lastDamage=hit.damage;totalDamage+=hit.damage;hitCount++;
            floatingDamage.Add(new FloatingDamage{amount=hit.damage,createdAt=Time.unscaledTime,world=dummyTransform.position+new Vector3(0f,2.35f,0f)});
            // Refill inside DamageApplied, before CharacterHealth performs its death check.
            dummyHealth.ConfigureMaximum(1000000f,true);
        }

        private void LateUpdate()
        {
            if(dummyMotor!=null)dummyMotor.SetPosition(dummyAnchor.x,dummyAnchor.y);
            usedLines=0;
            if(showHurtboxes)
            {
                DrawHurtbox(playerHurtbox,new Color(.15f,1f,.35f,1f));
                DrawHurtbox(dummyHurtbox,new Color(.15f,1f,.35f,1f));
            }
            if(showHitboxes&&playerRunner!=null&&playerRunner.IsPlaying)
            {
                int facing=playerCombat.GetComponent<CharacterMotor>()?.Facing??1;
                foreach(ActionFrameShape shape in playerRunner.Current.ShapesAt(playerRunner.CurrentFrame))
                {
                    if(shape.role==ActionShapeRole.Hurtbox)continue;
                    Color color=shape.role==ActionShapeRole.DamageHitbox?new Color(1f,.18f,.12f,1f):
                        shape.role==ActionShapeRole.EffectHitbox?new Color(.1f,.65f,1f,1f):new Color(1f,.2f,1f,1f);
                    DrawShape(playerCombat.transform,shape,facing,color);
                }
            }
            for(int i=usedLines;i<lines.Count;i++)lines[i].enabled=false;
            for(int i=floatingDamage.Count-1;i>=0;i--)if(Time.unscaledTime-floatingDamage[i].createdAt>1.05f)floatingDamage.RemoveAt(i);
        }

        private void DrawHurtbox(Hurtbox hurtbox,Color color)
        {
            if(hurtbox==null||hurtbox.Identity==null)return;
            Vector2 half=hurtbox.LocalSize*.5f,center=hurtbox.LocalCenter;
            Vector2[] points={center+new Vector2(-half.x,-half.y),center+new Vector2(-half.x,half.y),center+half,center+new Vector2(half.x,-half.y)};
            DrawPoints(hurtbox.Identity.transform,points,color);
        }

        private void DrawShape(Transform owner,ActionFrameShape shape,int facing,Color color)
        {
            if(shape==null||!shape.enabled)return;
            if(shape.shapeType==ActionShapeType.Polygon&&shape.points!=null&&shape.points.Count>=3)
            {
                var points=new Vector2[shape.points.Count];for(int i=0;i<points.Length;i++)points[i]=ActionGeometry.Mirror(shape.points[i],facing);DrawPoints(owner,points,color);return;
            }
            Vector2 center=ActionGeometry.Mirror(shape.center,facing),half=shape.size*.5f;
            Vector2[] box={center+new Vector2(-half.x,-half.y),center+new Vector2(-half.x,half.y),center+half,center+new Vector2(half.x,-half.y)};
            DrawPoints(owner,box,color);
        }

        private void DrawPoints(Transform owner,IReadOnlyList<Vector2> points,Color color)
        {
            if(owner==null||points==null||points.Count<2)return;
            LineRenderer line=GetLine();line.enabled=true;line.startColor=line.endColor=color;line.widthMultiplier=lineWidth;line.positionCount=points.Count;
            for(int i=0;i<points.Count;i++)line.SetPosition(i,owner.position+new Vector3(points[i].x,points[i].y,-1f));
        }

        private LineRenderer GetLine()
        {
            if(usedLines<lines.Count)return lines[usedLines++];
            var go=new GameObject("DebugShape_"+lines.Count);go.transform.SetParent(transform,false);
            var line=go.AddComponent<LineRenderer>();line.useWorldSpace=true;line.loop=true;line.alignment=LineAlignment.View;line.numCornerVertices=2;line.numCapVertices=2;line.sortingOrder=10000;
            if(lineMaterial!=null)line.sharedMaterial=lineMaterial;lines.Add(line);usedLines++;return line;
        }

        private void EnsureStyles()
        {
            if(titleStyle!=null)return;
            titleStyle=new GUIStyle(GUI.skin.label){fontSize=18,fontStyle=FontStyle.Bold,normal={textColor=new Color(1f,.85f,.35f)}};
            labelStyle=new GUIStyle(GUI.skin.label){fontSize=14,normal={textColor=Color.white}};
            damageStyle=new GUIStyle(GUI.skin.label){fontSize=26,fontStyle=FontStyle.Bold,alignment=TextAnchor.MiddleCenter,normal={textColor=new Color(1f,.82f,.22f)}};
        }

        private void OnGUI()
        {
            EnsureStyles();
            GUI.Box(new Rect(14,14,435,238),GUIContent.none);
            GUI.Label(new Rect(28,24,400,28),"COMBAT RANGE + DAMAGE TEST",titleStyle);
            ActionDefinition action=playerRunner!=null&&playerRunner.IsPlaying?playerRunner.Current:null;
            string actionName=action==null?"Idle / None":action.id;
            string frame=action==null?"-":playerRunner.CurrentFrame+" / "+(action.frameCount-1);
            string policy=action==null?"-":action.combat.hitPolicy.ToString();
            string configured=CurrentFrameDamage(action);
            GUI.Label(new Rect(28,57,405,21),"Action: "+actionName+"    Frame: "+frame,labelStyle);
            GUI.Label(new Rect(28,82,405,21),"Hit Policy: "+policy,labelStyle);
            GUI.Label(new Rect(28,107,405,21),"Configured damage this frame: "+configured,labelStyle);
            GUI.Label(new Rect(28,132,405,21),"Last actual damage: "+lastDamage.ToString("0.##")+"    Hits: "+hitCount,labelStyle);
            GUI.Label(new Rect(28,157,405,21),"Dummy HP: ∞    Total test damage: "+totalDamage.ToString("0.##"),labelStyle);
            GUI.Label(new Rect(28,187,405,20),"GREEN Hurtbox   RED Damage   BLUE Effect   PURPLE Weapon",labelStyle);
            GUI.Label(new Rect(28,213,405,20),"J Combo  K Heavy  H Skill1  I Charge  U Skill3  O Skill4",labelStyle);
            Camera camera=Camera.main;
            if(camera==null)return;
            foreach(FloatingDamage item in floatingDamage)
            {
                float age=Time.unscaledTime-item.createdAt;Vector3 world=item.world+Vector3.up*(age*.65f);Vector3 screen=camera.WorldToScreenPoint(world);
                Color old=GUI.color;GUI.color=new Color(1f,1f,1f,1f-Mathf.Clamp01(age/.95f));GUI.Label(new Rect(screen.x-60,Screen.height-screen.y-20,120,40),"-"+item.amount.ToString("0.##"),damageStyle);GUI.color=old;
            }
        }

        private string CurrentFrameDamage(ActionDefinition action)
        {
            if(action==null)return "-";var values=new List<string>();
            foreach(ActionFrameShape shape in action.ShapesAt(playerRunner.CurrentFrame))if(shape.role!=ActionShapeRole.Hurtbox)values.Add(shape.EffectiveDamage(playerRunner.EffectiveActionDamage).ToString("0.##"));
            return values.Count==0?"none":string.Join(" / ",values);
        }
    }
}
