using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ThreeKingdoms.EditorTools
{
    public sealed class ThreeKingdomsBattleEditorWindow : EditorWindow
    {
        private enum DrawTool { Select, Box, Polygon, FramePose }
        private enum WorkspaceMode { FrameEdit, EmbeddedTest }
        private static readonly Color Green = new Color(.18f, 1f, .35f, .85f), Red = new Color(1f, .2f, .15f, .9f), Blue = new Color(.15f, .65f, 1f, .9f), Purple = new Color(.75f, .3f, 1f, .9f);
        private string owner = "Diaochan", search = "";
        private Vector2 browserScroll, settingsScroll, validationScroll;
        private ActionDefinition selected, draft;
        private SerializedObject serializedDraft;
        private List<ActionDefinition> actions = new List<ActionDefinition>();
        private List<Sprite> frames = new List<Sprite>();
        private List<string> validation = new List<string>();
        private int frame, copyFrom, copyTo;
        private bool playing, facingLeft, loop = true, dirty, fullActionPreview = true;
        private double lastUpdate, playbackFrameAccumulator;
        private float playbackSpeed = 1f, zoom = 1f;
        private DrawTool tool;
        private ActionShapeRole role = ActionShapeRole.EffectHitbox;
        private ActionFrameShape selectedShape;
        private bool selectedDefaultHurtbox;
        private int selectedPoint = -1;
        private Vector2 lastSelectionPosition = new Vector2(float.MinValue, float.MinValue);
        private int selectionCycle;
        private string canvasInputStatus = "Canvas input ready";
        private static readonly int CanvasControlHint = "ThreeKingdomsBattleEditorCanvas".GetHashCode();
        private Vector2 dragStart;
        private Vector2 framePoseDragStartOffset;
        private Vector2 framePoseDragStartMouse;
        private bool boxDragging;
        private readonly List<Vector2> pendingPolygon = new List<Vector2>();
        private bool showSprite = true, showPivot = true, showFoot = true, showHurt = true, showHit = true, showEffect = true, showWeapon = true, showMovement = true, showElevation = true;
        private bool showComboWindowEditor = true;
        private bool showLightReaction = true, showHeavyReaction = true;
        private int selectedComboSegment;
        private WorkspaceMode workspaceMode;
        private List<Sprite> dummyFrames = new List<Sprite>();
        private float testDummyDistance=2.15f,testTotalDamage,testLastDamage,testLastHitTime=-999f;
        private int testHitCount;
        private bool testHitAction,testHitPhase;
        private static readonly string[] Owners = { "Diaochan", "Soldier", "CaoCao" };

        public ActionDefinition SelectedAction => selected;
        public ActionDefinition DraftAction => draft;
        public int CurrentFrame => frame;
        public float EmbeddedTestTotalDamage=>testTotalDamage;
        public int EmbeddedTestHitCount=>testHitCount;
        public void RunEmbeddedTestFrameForTest(int targetFrame,float dummyDistance)
        {
            testDummyDistance=dummyDistance;ResetEmbeddedTest();frame=draft==null?0:draft.ClampFrame(targetFrame);EvaluateEmbeddedTestFrame(frame);
        }

        [MenuItem("Tools/Three Kingdoms/Battle Editor")]
        public static ThreeKingdomsBattleEditorWindow Open()
        {
            var window = GetWindow<ThreeKingdomsBattleEditorWindow>();
            window.titleContent = new GUIContent("Battle Editor"); window.minSize = new Vector2(1050, 680); window.Show(); return window;
        }
        private void OnEnable() { Undo.undoRedoPerformed += OnUndoRedo; EditorApplication.update += EditorTick; ReloadActions(); }
        private void OnDisable() { Undo.undoRedoPerformed -= OnUndoRedo; EditorApplication.update -= EditorTick; if (draft != null) DestroyImmediate(draft); }
        private void OnUndoRedo() { dirty = true; Repaint(); }
        private void EditorTick()
        {
            if (!playing || draft == null || frames.Count == 0) { lastUpdate = EditorApplication.timeSinceStartup; return; }
            double now = EditorApplication.timeSinceStartup, delta = now - lastUpdate; lastUpdate = now;
            if (delta <= 0) return;
            // EditorApplication.update can run much faster than the authored action FPS. Accumulate real
            // elapsed time and only advance when a complete authored frame interval has passed.
            playbackFrameAccumulator += delta * Mathf.Max(1f,draft.framesPerSecond) * playbackSpeed;
            int advance=Mathf.FloorToInt((float)playbackFrameAccumulator);if(advance<1)return;playbackFrameAccumulator-=advance;
            int previous=frame,next = frame + advance;bool wrapped=next>=frames.Count;
            if (wrapped) { if (loop) next %= frames.Count; else { next = frames.Count - 1; playing = false; } }
            if (next != frame)
            {
                if(workspaceMode==WorkspaceMode.EmbeddedTest)
                {
                    if(wrapped&&loop){for(int crossed=previous+1;crossed<frames.Count;crossed++)EvaluateEmbeddedTestFrame(crossed);ResetEmbeddedHitRegister();for(int crossed=0;crossed<=next;crossed++)EvaluateEmbeddedTestFrame(crossed);}
                    else for(int crossed=previous+1;crossed<=next;crossed++)EvaluateEmbeddedTestFrame(crossed);
                }
                frame = next; Repaint();
            }
        }
        public void ReloadActions()
        {
            actions.Clear(); string[] guids = AssetDatabase.FindAssets("t:ActionDefinition");
            foreach (string guid in guids) { var a = AssetDatabase.LoadAssetAtPath<ActionDefinition>(AssetDatabase.GUIDToAssetPath(guid)); if (a != null) actions.Add(a); }
            actions.Sort((a,b) => string.Compare(a.id,b.id,StringComparison.OrdinalIgnoreCase)); Repaint();
        }
        public bool SelectAction(ActionDefinition action)
        {
            if (action == null) return false;
            if (dirty && !EditorUtility.DisplayDialog("Unsaved Action", "Discard unsaved draft changes?", "Discard", "Cancel")) return false;
            if (draft != null) DestroyImmediate(draft);
            selected = action; draft = Instantiate(action); draft.hideFlags = HideFlags.HideAndDontSave; serializedDraft = new SerializedObject(draft);
            // Keep the editable combat settings detached from the source Action until SAVE is pressed.
            draft.combat = action.combat == null ? new ActionCombatData() : action.combat.Clone();
            frames = ActionEditorUtility.ReadAnimationFrames(draft.animation);
            bool frameCountCorrected=frames.Count>0&&draft.frameCount!=frames.Count;
            if(frames.Count>0){int previousLast=Mathf.Max(0,draft.frameCount-1);Vector2 previousLastOffset=draft.VisualOffsetAt(previousLast);draft.frameCount=frames.Count;draft.recoveryEndFrame=Mathf.Max(draft.recoveryEndFrame,frames.Count-1);if(frameCountCorrected&&previousLastOffset!=Vector2.zero)for(int i=previousLast+1;i<frames.Count;i++)draft.SetVisualOffset(i,previousLastOffset);draft.visualFrames=new List<Sprite>(frames);}
            LoadDummyFrames();frame = 0; selectedShape = null; selectedDefaultHurtbox = false; selectedPoint = -1; pendingPolygon.Clear(); dirty = frameCountCorrected;titleContent=new GUIContent(frameCountCorrected?"Battle Editor *":"Battle Editor"); validation.Clear();ResetEmbeddedTest();return true;
        }
        private void LoadDummyFrames()
        {
            dummyFrames.Clear();var library=AssetDatabase.LoadAssetAtPath<CharacterActionLibrary>("Assets/Data/Combat/Actions/LIB_Soldier.asset");ActionDefinition idle=library==null?null:library.Find("Idle")??library.Find("IdleLong")??library.Find("Walk");if(idle!=null)dummyFrames=ActionEditorUtility.ReadAnimationFrames(idle.animation);
        }
        public void SetFrame(int value) { frame = draft == null ? 0 : Mathf.Clamp(value, 0, Mathf.Max(0, frames.Count - 1)); selectedShape = null; selectedDefaultHurtbox = false; selectedPoint = -1; Repaint(); }
        public void ConfigureEvidence(ActionDefinition action, int targetFrame, bool left, int toolIndex, ActionShapeRole targetRole, bool validate = false)
        {
            if (action != null) { owner = action.ownerId; SelectAction(action); }
            SetFrame(targetFrame); facingLeft = left; tool = (DrawTool)Mathf.Clamp(toolIndex, 0, 2); role = targetRole;
            if (validate && draft != null) validation = ActionEditorUtility.ValidateAction(draft); else validation.Clear();
            Repaint(); Focus();
        }
        public void SaveDraft()
        {
            if (selected == null || draft == null) return;
            draft.dataVersion=ActionDefinition.CurrentVersion;
            if(frames.Count>0){draft.frameCount=frames.Count;draft.visualFrames=new List<Sprite>(frames);draft.recoveryEndFrame=Mathf.Clamp(draft.recoveryEndFrame,0,frames.Count-1);}
            // Frame Pose is authored action data.  Never rewrite shared PNG importer pivots
            // while saving an action: runtime ActionRunner consumes these same offsets.
            if(ActionEditorUtility.HasLegacyComboFrames(draft))ActionEditorUtility.ConvertLegacyComboFrames(draft);
            Undo.RecordObject(selected, "Save Battle Action"); EditorUtility.CopySerialized(draft, selected); EditorUtility.SetDirty(selected); AssetDatabase.SaveAssets();
            string backup=BackupSavedAction(selected);dirty = false; titleContent = new GUIContent("Battle Editor");ShowNotification(new GUIContent("Saved current Action\nBackup: "+Path.GetFileName(backup)));Debug.Log("[BATTLE_EDITOR] Saved "+AssetDatabase.GetAssetPath(selected)+"; backup="+backup);
        }
        private static string BackupSavedAction(ActionDefinition action)
        {
            string assetPath=AssetDatabase.GetAssetPath(action),projectRoot=Path.GetFullPath(Path.Combine(Application.dataPath,"..")),source=Path.Combine(projectRoot,assetPath.Replace('/',Path.DirectorySeparatorChar));
            string safeId=string.IsNullOrWhiteSpace(action.id)?"Action":string.Concat(action.id.Split(Path.GetInvalidFileNameChars()));string directory=Path.Combine(projectRoot,"BattleEditorBackups",safeId);Directory.CreateDirectory(directory);
            string backup=Path.Combine(directory,DateTime.Now.ToString("yyyyMMdd_HHmmss_fff")+"_"+Path.GetFileName(source));File.Copy(source,backup,true);return backup;
        }
        private void MarkDirty(string label)
        {
            if (draft == null) return; Undo.RecordObject(draft, label); dirty = true; titleContent = new GUIContent("Battle Editor *");
        }
        private ActionDefinition DummyAction()
        {
            var library=AssetDatabase.LoadAssetAtPath<CharacterActionLibrary>("Assets/Data/Combat/Actions/LIB_Soldier.asset");return library==null?null:library.Find("Idle")??library.Find("IdleLong")??library.Find("Walk");
        }
        private void ResetEmbeddedHitRegister(){testHitAction=false;testHitPhase=false;testLastHitTime=-999f;}
        private void ResetEmbeddedTest(){testTotalDamage=0f;testLastDamage=0f;testHitCount=0;ResetEmbeddedHitRegister();}
        private bool HasOffensiveShapeFrame(int target)
        {
            if(draft==null)return false;foreach(ActionFrameShape shape in draft.ShapesAt(target))if(shape.role!=ActionShapeRole.Hurtbox)return true;return false;
        }
        private void EvaluateEmbeddedTestFrame(int targetFrame)
        {
            if(draft==null||draft.combat==null)return;bool offensive=HasOffensiveShapeFrame(targetFrame);
            if(draft.combat.hitPolicy==ActionHitPolicy.OncePerFrame)testHitPhase=false;
            if(draft.combat.hitPolicy==ActionHitPolicy.OncePerPhase&&offensive&&(targetFrame==0||!HasOffensiveShapeFrame(targetFrame-1)))testHitPhase=false;
            if(!offensive)return;ActionDefinition dummy=DummyAction();Vector2 hurtCenter=dummy==null?new Vector2(0,1.1f):dummy.defaultHurtboxCenter,hurtSize=dummy==null?new Vector2(.8f,2.2f):dummy.defaultHurtboxSize;
            float n=draft.frameCount<=1?0f:targetFrame/(float)(draft.frameCount-1),facing=facingLeft?-1f:1f;float rootX=draft.lockGroundPosition?0f:draft.movement.moveX.Evaluate(n)*facing,rootDepth=draft.lockGroundPosition?0f:draft.movement.moveDepth.Evaluate(n);
            if(Mathf.Abs(rootDepth)>draft.combat.broadPhaseDepth)return;float dummyX=testDummyDistance*facing,actionTime=targetFrame/Mathf.Max(1f,draft.framesPerSecond);
            foreach(ActionFrameShape shape in draft.ShapesAt(targetFrame))
            {
                if(shape.role==ActionShapeRole.Hurtbox)continue;bool canHit=draft.combat.hitPolicy==ActionHitPolicy.OncePerAction?!testHitAction:draft.combat.hitPolicy==ActionHitPolicy.RepeatInterval?actionTime-testLastHitTime>=draft.combat.repeatInterval:!testHitPhase;
                if(!canHit||!ActionGeometry.IntersectsBox(shape,new Vector2(dummyX-rootX,hurtCenter.y),hurtSize,facingLeft?-1:1))continue;
                testLastDamage=shape.EffectiveDamage(draft.combat.damage);testTotalDamage+=testLastDamage;testHitCount++;testHitAction=true;testHitPhase=true;testLastHitTime=actionTime;break;
            }
        }
        private void OnGUI()
        {
            DrawHeader(); Rect body = GUILayoutUtility.GetRect(position.width, Mathf.Max(380, position.height - 240));
            float left = 220f, right = 330f; DrawBrowser(new Rect(body.x, body.y, left, body.height));
            DrawPreview(new Rect(body.x + left + 4, body.y, body.width - left - right - 8, body.height));
            DrawSettings(new Rect(body.xMax - right, body.y, right, body.height));
            DrawTimeline(); DrawFooter();
        }
        private void DrawHeader()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label("THREE KINGDOMS  •  BATTLE EDITOR V0.1", new GUIStyle(EditorStyles.boldLabel){fontSize=18,alignment=TextAnchor.MiddleCenter});
            EditorGUILayout.BeginHorizontal(); GUILayout.Label("Character", GUILayout.Width(70));
            int ownerIndex=Mathf.Max(0,Array.IndexOf(Owners,owner));string next=Owners[EditorGUILayout.Popup(ownerIndex,Owners,GUILayout.Width(130))];
            if (next != owner) { owner = next; selected = null; if (draft != null) DestroyImmediate(draft); draft = null; dirty = false; }
            GUILayout.Space(10); GUILayout.Label("Action", GUILayout.Width(45)); GUILayout.Label(draft == null ? "—" : draft.displayName + (dirty ? " *" : ""), EditorStyles.boldLabel);
            GUILayout.Space(18);Color old=GUI.backgroundColor;if(workspaceMode==WorkspaceMode.FrameEdit)GUI.backgroundColor=new Color(.25f,.65f,1f);if(GUILayout.Button("FRAME EDIT",EditorStyles.miniButton,GUILayout.Width(90))){workspaceMode=WorkspaceMode.FrameEdit;playing=false;}GUI.backgroundColor=workspaceMode==WorkspaceMode.EmbeddedTest?new Color(1f,.55f,.2f):old;if(GUILayout.Button("EMBEDDED TEST",EditorStyles.miniButton,GUILayout.Width(115))){workspaceMode=WorkspaceMode.EmbeddedTest;frame=0;ResetEmbeddedTest();fullActionPreview=true;}GUI.backgroundColor=old;
            GUILayout.FlexibleSpace(); GUILayout.Label("DATA → ActionRunner → Animator + Hitbox + Movement", EditorStyles.miniLabel); EditorGUILayout.EndHorizontal(); EditorGUILayout.EndVertical();
        }
        private void DrawBrowser(Rect rect)
        {
            GUI.Box(rect,GUIContent.none);GUI.Label(new Rect(rect.x+8,rect.y+7,rect.width-16,20),"ACTION BROWSER",EditorStyles.boldLabel);
            search=GUI.TextField(new Rect(rect.x+8,rect.y+30,rect.width-16,20),search,EditorStyles.toolbarSearchField);float y=rect.y+56;
            foreach(ActionCategory category in Enum.GetValues(typeof(ActionCategory)))
            {
                var visible=actions.FindAll(a=>a!=null&&a.ownerId==owner&&a.category==category&&(string.IsNullOrEmpty(search)||a.id.IndexOf(search,StringComparison.OrdinalIgnoreCase)>=0));if(visible.Count==0)continue;
                GUI.Label(new Rect(rect.x+8,y,rect.width-16,18),category.ToString().ToUpperInvariant(),EditorStyles.miniBoldLabel);y+=18;
                foreach(ActionDefinition action in visible){if(y>rect.yMax-34)break;Color old=GUI.backgroundColor;if(selected==action)GUI.backgroundColor=new Color(.25f,.65f,1f);if(GUI.Button(new Rect(rect.x+8,y,rect.width-16,20),action.displayName+"   ["+action.frameCount+"f]",EditorStyles.miniButton)&&selected!=action)SelectAction(action);GUI.backgroundColor=old;y+=22;}
            }
            if(GUI.Button(new Rect(rect.x+8,rect.yMax-27,rect.width-16,20),"Import Missing Actions (Safe)",EditorStyles.miniButton)){BattleEditorMigration.MigrateExistingCombatData();ReloadActions();}
        }
        private void DrawPreview(Rect rect)
        {
            if(workspaceMode==WorkspaceMode.EmbeddedTest){DrawEmbeddedTest(rect);return;}
            GUI.Box(rect, GUIContent.none); Rect toolbar = new Rect(rect.x+5,rect.y+5,rect.width-10,42);
            float toolbarX=toolbar.x+4;DrawTool previousTool=tool;tool=(DrawTool)GUI.Toolbar(new Rect(toolbarX,toolbar.y+4,300,22),(int)tool,new[]{"Select","Box","Polygon","Frame Pose"});if(tool!=previousTool)pendingPolygon.Clear();
            GUI.Label(new Rect(toolbarX+310,toolbar.y+5,35,20),"Role");role=(ActionShapeRole)EditorGUI.EnumPopup(new Rect(toolbarX+345,toolbar.y+4,135,22),role);
            if(GUI.Button(new Rect(toolbarX+490,toolbar.y+4,105,22),facingLeft?"Facing Left":"Facing Right",EditorStyles.miniButton))facingLeft=!facingLeft;
            if(pendingPolygon.Count>0){GUI.Label(new Rect(toolbarX+605,toolbar.y+5,75,20),pendingPolygon.Count+" points");if(GUI.Button(new Rect(toolbarX+680,toolbar.y+4,82,22),"Undo Point",EditorStyles.miniButton))pendingPolygon.RemoveAt(pendingPolygon.Count-1);if(GUI.Button(new Rect(toolbarX+766,toolbar.y+4,62,22),"Cancel",EditorStyles.miniButton))pendingPolygon.Clear();}
            if(draft!=null)DrawFrameShapeSelector(new Rect(toolbarX,toolbar.y+27,toolbar.width-8,18));
            Rect canvas = new Rect(rect.x+8, rect.y+50, rect.width-16, rect.height-100); EditorGUI.DrawRect(canvas,new Color(.055f,.07f,.09f));
            if (draft == null) { GUI.Label(canvas,"Select or migrate an ActionDefinition",new GUIStyle(EditorStyles.centeredGreyMiniLabel){fontSize=16}); return; }
            Sprite sprite = frame >= 0 && frame < frames.Count ? frames[frame] : null;
            Vector2 baseOrigin = new Vector2(canvas.center.x, canvas.yMax-55); float pixelsPerUnit = 72f * zoom;
            float normalized=frames.Count<=1?0f:frame/(float)(frames.Count-1);Vector2 authoredRoot=fullActionPreview&&!draft.lockGroundPosition?new Vector2(draft.movement.moveX.Evaluate(normalized),draft.movement.moveDepth.Evaluate(normalized)):Vector2.zero;
            float elevation=fullActionPreview?draft.movement.elevation.Evaluate(normalized):0f;Vector2 frameOffset=draft.VisualOffsetAt(frame);bool spriteFlip=ShouldFlipSprite(sprite,facingLeft);
            Vector2 rootOrigin=LocalToCanvas(authoredRoot,baseOrigin,pixelsPerUnit),elevatedOrigin=LocalToCanvas(authoredRoot+new Vector2(0,elevation),baseOrigin,pixelsPerUnit),spriteOrigin=VisualOffsetToCanvas(frameOffset,elevatedOrigin,pixelsPerUnit,spriteFlip);DrawShadowOrigin(rootOrigin);
            if (showSprite && sprite != null) DrawSprite(sprite, spriteOrigin, pixelsPerUnit, spriteFlip, PreviewVisualScale(draft.ownerId));
            int canvasControl=GUIUtility.GetControlID(CanvasControlHint,FocusType.Keyboard,canvas);EditorGUIUtility.AddCursorRect(canvas,MouseCursor.Arrow);
            HandleCanvas(canvas, rootOrigin, baseOrigin, pixelsPerUnit,canvasControl);DrawOverlays(canvas, baseOrigin,rootOrigin,elevatedOrigin,spriteOrigin,pixelsPerUnit);
            GUI.Label(new Rect(canvas.x+8,canvas.y+8,220,22),$"FRAME {frame:00} / {Mathf.Max(0,frames.Count-1):00}",new GUIStyle(EditorStyles.boldLabel){fontSize=14});
            GUI.Label(new Rect(canvas.x+8,canvas.y+28,300,20),$"{(sprite==null?"No Sprite":sprite.name)}  •  {draft.framesPerSecond:0.##} FPS",EditorStyles.miniLabel);
            string phase=frame<draft.startupEndFrame?"STARTUP":frame<=draft.activeEndFrame?"ACTIVE":"RECOVERY";int offensive=0;foreach(ActionFrameShape shape in draft.ShapesAt(frame))if(shape.role!=ActionShapeRole.Hurtbox)offensive++;
            GUI.Label(new Rect(canvas.x+8,canvas.y+48,560,20),$"FULL ACTION: {phase}  •  Root ({authoredRoot.x:0.00}, {authoredRoot.y:0.00})  •  Elevation {elevation:0.00}  •  Visual ({frameOffset.x:0.00}, {frameOffset.y:0.00})  •  Offensive shapes {offensive}",ColorLabel(phase=="ACTIVE"?Red:Color.cyan));
            GUI.Label(new Rect(canvas.xMax-260,canvas.y+8,250,20),canvasInputStatus,new GUIStyle(EditorStyles.miniBoldLabel){alignment=TextAnchor.UpperRight});
            Rect toggles = new Rect(rect.x+8,rect.yMax-45,rect.width-16,40); GUILayout.BeginArea(toggles); EditorGUILayout.BeginHorizontal();
            fullActionPreview=GUILayout.Toggle(fullActionPreview,"Full Action");showSprite=GUILayout.Toggle(showSprite,"Sprite");showPivot=GUILayout.Toggle(showPivot,"Root");showFoot=GUILayout.Toggle(showFoot,"Foot");showHurt=GUILayout.Toggle(showHurt,"Hurt");showHit=GUILayout.Toggle(showHit,"Hit");showEffect=GUILayout.Toggle(showEffect,"Effect");showWeapon=GUILayout.Toggle(showWeapon,"Weapon");showMovement=GUILayout.Toggle(showMovement,"Move");showElevation=GUILayout.Toggle(showElevation,"Elevation");
            EditorGUILayout.EndHorizontal(); zoom=EditorGUILayout.Slider("Zoom",zoom,.4f,2.5f); GUILayout.EndArea();
        }
        private void DrawEmbeddedTest(Rect rect)
        {
            GUI.Box(rect,GUIContent.none);if(draft==null){GUI.Label(rect,"Select an ActionDefinition",new GUIStyle(EditorStyles.centeredGreyMiniLabel){fontSize=16});return;}
            Rect bar=new Rect(rect.x+8,rect.y+7,rect.width-16,25);Color old=GUI.backgroundColor;GUI.backgroundColor=playing?new Color(1f,.55f,.2f):old;
            if(GUI.Button(new Rect(bar.x,bar.y,playing?92:82,22),playing?"PAUSE TEST":"PLAY TEST",EditorStyles.miniButton)){if(!playing){frame=0;ResetEmbeddedTest();EvaluateEmbeddedTestFrame(0);}playing=!playing;fullActionPreview=true;lastUpdate=EditorApplication.timeSinceStartup;playbackFrameAccumulator=0;}GUI.backgroundColor=old;
            if(GUI.Button(new Rect(bar.x+98,bar.y,72,22),"RESET",EditorStyles.miniButton)){playing=false;frame=0;ResetEmbeddedTest();}
            GUI.Label(new Rect(bar.x+182,bar.y+2,92,20),"Dummy Distance");testDummyDistance=GUI.HorizontalSlider(new Rect(bar.x+274,bar.y+6,125,18),testDummyDistance,.6f,5f);GUI.Label(new Rect(bar.x+404,bar.y+2,42,20),testDummyDistance.ToString("0.00"));
            if(GUI.Button(new Rect(bar.x+452,bar.y,90,22),facingLeft?"Facing Left":"Facing Right",EditorStyles.miniButton)){facingLeft=!facingLeft;ResetEmbeddedTest();}
            if(GUI.Button(new Rect(bar.xMax-190,bar.y,88,22),"FRAME EDIT",EditorStyles.miniButton)){workspaceMode=WorkspaceMode.FrameEdit;playing=false;}
            if(GUI.Button(new Rect(bar.xMax-98,bar.y,98,22),"OPEN SCENE",EditorStyles.miniButton)){SaveDraft();BattleEditorMigration.OpenTestScene(selected);}
            Rect canvas=new Rect(rect.x+8,rect.y+38,rect.width-16,rect.height-43);EditorGUI.DrawRect(canvas,new Color(.035f,.055f,.08f));float ppu=72f*zoom;Vector2 baseOrigin=new Vector2(canvas.center.x-145,canvas.yMax-85);
            float n=frames.Count<=1?0f:frame/(float)(frames.Count-1);Vector2 rootLocal=draft.lockGroundPosition?Vector2.zero:new Vector2(draft.movement.moveX.Evaluate(n),draft.movement.moveDepth.Evaluate(n));float elevation=draft.movement.elevation.Evaluate(n);Vector2 visual=draft.VisualOffsetAt(frame);Sprite actor=frame>=0&&frame<frames.Count?frames[frame]:null;bool actorFlip=ShouldFlipSprite(actor,facingLeft);
            Vector2 rootOrigin=LocalToCanvas(rootLocal,baseOrigin,ppu),elevatedOrigin=LocalToCanvas(rootLocal+new Vector2(0,elevation),baseOrigin,ppu),spriteOrigin=VisualOffsetToCanvas(visual,elevatedOrigin,ppu,actorFlip);DrawShadowOrigin(rootOrigin);if(actor!=null)DrawSprite(actor,spriteOrigin,ppu,actorFlip,PreviewVisualScale(draft.ownerId));
            Vector2 dummyLocal=new Vector2(testDummyDistance*(facingLeft?-1:1),0),dummyOrigin=LocalToCanvas(dummyLocal,baseOrigin,ppu);Sprite dummySprite=dummyFrames.Count==0?null:dummyFrames[frame%dummyFrames.Count];if(dummySprite!=null)DrawSprite(dummySprite,dummyOrigin,ppu,false,2.38f);
            ActionDefinition dummy=DummyAction();Vector2 dummyHurtCenter=dummy==null?new Vector2(0,1.1f):dummy.defaultHurtboxCenter,dummyHurtSize=dummy==null?new Vector2(.8f,2.2f):dummy.defaultHurtboxSize;
            Handles.BeginGUI();Handles.color=Color.gray;Handles.DrawLine(new Vector2(canvas.x+24,baseOrigin.y),new Vector2(canvas.xMax-24,baseOrigin.y));if(showHurt){DrawShape(new ActionFrameShape{center=draft.defaultHurtboxCenter,size=draft.defaultHurtboxSize},Green,rootOrigin,ppu);DrawShape(new ActionFrameShape{center=dummyHurtCenter,size=dummyHurtSize},Green,dummyOrigin,ppu);}foreach(ActionFrameShape shape in draft.ShapesAt(frame)){if(shape.role==ActionShapeRole.Hurtbox&&!showHurt||shape.role==ActionShapeRole.DamageHitbox&&!showHit||shape.role==ActionShapeRole.EffectHitbox&&!showEffect||shape.role==ActionShapeRole.WeaponHitbox&&!showWeapon)continue;DrawShape(shape,shape.role==ActionShapeRole.Hurtbox?Green:shape.role==ActionShapeRole.DamageHitbox?Red:shape.role==ActionShapeRole.EffectHitbox?Blue:Purple,rootOrigin,ppu);}Handles.EndGUI();
            Rect hud=new Rect(canvas.x+14,canvas.y+14,330,150);EditorGUI.DrawRect(hud,new Color(0,0,0,.82f));GUI.Label(new Rect(hud.x+12,hud.y+9,hud.width-24,24),"EMBEDDED COMBAT TEST",new GUIStyle(EditorStyles.boldLabel){fontSize=15,normal={textColor=new Color(1f,.75f,.2f)}});string phase=frame<draft.startupEndFrame?"STARTUP":frame<=draft.activeEndFrame?"ACTIVE":"RECOVERY";
            GUI.Label(new Rect(hud.x+12,hud.y+38,hud.width-24,20),$"Action: {draft.displayName}   Frame: {frame:00}/{Mathf.Max(0,draft.frameCount-1):00}   {phase}");GUI.Label(new Rect(hud.x+12,hud.y+60,hud.width-24,20),$"Hit Policy: {draft.combat.hitPolicy}");GUI.Label(new Rect(hud.x+12,hud.y+82,hud.width-24,20),$"Last damage: {testLastDamage:0.##}   Hits: {testHitCount}");GUI.Label(new Rect(hud.x+12,hud.y+104,hud.width-24,20),$"Dummy HP: ∞   Total test damage: {testTotalDamage:0.##}");GUI.Label(new Rect(hud.x+12,hud.y+126,hud.width-24,18),"GREEN Hurtbox   RED Damage   BLUE Effect   PURPLE Weapon",EditorStyles.miniLabel);
        }
        private void DrawFrameShapeSelector(Rect rect)
        {
            float x=rect.x;GUI.Label(new Rect(x,rect.y,82,18),"Frame shapes:",EditorStyles.miniBoldLabel);x+=82;
            Color old=GUI.backgroundColor;GUI.backgroundColor=selectedDefaultHurtbox?Color.white:Green;
            if(GUI.Button(new Rect(x,rect.y,72,18),"HURT",EditorStyles.miniButton)){selectedDefaultHurtbox=true;selectedShape=null;selectedPoint=-1;canvasInputStatus="Selected green default HURT";Repaint();}x+=76;
            int shapeIndex=0;foreach(ActionFrameShape shape in draft.ShapesAt(frame))
            {
                if(x+92>rect.xMax)break;GUI.backgroundColor=selectedShape==shape?Color.white:shape.role==ActionShapeRole.DamageHitbox?Red:shape.role==ActionShapeRole.EffectHitbox?Blue:shape.role==ActionShapeRole.Hurtbox?Green:Purple;
                string shortRole=shape.role==ActionShapeRole.DamageHitbox?"DAMAGE":shape.role==ActionShapeRole.EffectHitbox?"EFFECT":shape.role==ActionShapeRole.WeaponHitbox?"WEAPON":"HURT";
                if(GUI.Button(new Rect(x,rect.y,88,18),shortRole+" "+shapeIndex,EditorStyles.miniButton)){selectedShape=shape;selectedDefaultHurtbox=false;selectedPoint=-1;canvasInputStatus="Selected "+shortRole+" "+shapeIndex;Repaint();}x+=92;shapeIndex++;
            }
            GUI.backgroundColor=old;
        }
        private static void DrawSprite(Sprite sprite, Vector2 origin, float ppu, bool flip, float visualScale)
        {
            Rect tr=sprite.textureRect;
            // Sprite.pivot is expressed in the original Sprite rect, while textureRect is
            // the alpha-trimmed Tight-mesh rectangle.  Mixing those two spaces displaced
            // Battle Editor previews by textureRectOffset (128 source pixels on Soldier
            // Attack frame 00), so an apparently correct drag became a huge Game offset.
            // Convert the pivot into the trimmed textureRect space exactly as SpriteRenderer
            // does. Frame Pose can then remain ordinary VisualRoot-local coordinates.
            Vector2 pivot=PreviewTexturePivot(sprite);float w=tr.width/sprite.pixelsPerUnit*ppu*visualScale,h=tr.height/sprite.pixelsPerUnit*ppu*visualScale;
            float px=pivot.x/sprite.pixelsPerUnit*ppu*visualScale,py=pivot.y/sprite.pixelsPerUnit*ppu*visualScale;
            float top=origin.y-(h-py);
            Rect dest = flip ? new Rect(origin.x-(w-px),top,w,h) : new Rect(origin.x-px,top,w,h);
            Rect uv=new Rect(tr.x/sprite.texture.width,tr.y/sprite.texture.height,tr.width/sprite.texture.width,tr.height/sprite.texture.height);
            if(flip){uv.x+=uv.width;uv.width=-uv.width;} GUI.DrawTextureWithTexCoords(dest,sprite.texture,uv,true);
        }
        public static Vector2 PreviewTexturePivot(Sprite sprite)=>sprite==null?Vector2.zero:sprite.pivot-sprite.textureRectOffset;
        private static void DrawShadowOrigin(Vector2 origin)
        {
            Handles.BeginGUI();Handles.color=new Color(0f,1f,1f,.22f);Handles.DrawSolidDisc(origin,Vector3.forward,12f);Handles.color=Color.cyan;Handles.DrawLine(origin+Vector2.left*10f,origin+Vector2.right*10f);Handles.DrawLine(origin+Vector2.up*7f,origin+Vector2.down*7f);Handles.EndGUI();
            GUI.Label(new Rect(origin.x+12f,origin.y-18f,105f,18f),"ROOT / SHADOW",new GUIStyle(EditorStyles.miniBoldLabel){normal={textColor=Color.cyan}});
        }
        private static bool SourceFacesLeft(Sprite sprite)=>sprite!=null&&sprite.name.StartsWith("NSOL_",StringComparison.Ordinal)&&!sprite.name.StartsWith("NSOL_Attack_",StringComparison.Ordinal)&&!sprite.name.StartsWith("NSOL_HitBlood_",StringComparison.Ordinal);
        private static bool ShouldFlipSprite(Sprite sprite,bool targetFacesLeft)=>SourceFacesLeft(sprite)?!targetFacesLeft:targetFacesLeft;
        private static Vector2 VisualOffsetToCanvas(Vector2 offset,Vector2 origin,float ppu,bool spriteFlip)=>origin+new Vector2(offset.x*(spriteFlip?-1f:1f)*ppu,-offset.y*ppu);
        private static float PreviewVisualScale(string ownerId) => ownerId=="CaoCao" ? 1.70f : 2.38f;
        private Vector2 LocalToCanvas(Vector2 local, Vector2 origin,float ppu){if(facingLeft)local.x=-local.x;return new Vector2(origin.x+local.x*ppu,origin.y-local.y*ppu);}
        private Vector2 CanvasToLocal(Vector2 canvas,Vector2 origin,float ppu){Vector2 p=new Vector2((canvas.x-origin.x)/ppu,(origin.y-canvas.y)/ppu);if(facingLeft)p.x=-p.x;return p;}
        private void DrawOverlays(Rect canvas,Vector2 baseOrigin,Vector2 origin,Vector2 elevatedOrigin,Vector2 spriteOrigin,float ppu)
        {
            Handles.BeginGUI();
            if(showPivot){Handles.color=Color.yellow;Handles.DrawLine(origin+Vector2.left*8,origin+Vector2.right*8);Handles.DrawLine(origin+Vector2.up*8,origin+Vector2.down*8);}
            if(showFoot){Vector2 p=LocalToCanvas(draft.footPoint,origin,ppu);Handles.color=Color.white;Handles.DrawLine(p+Vector2.left*7,p+Vector2.right*7);Handles.DrawLine(p+Vector2.up*7,p+Vector2.down*7);}
            if(showHurt){var h=new ActionFrameShape{center=draft.defaultHurtboxCenter,size=draft.defaultHurtboxSize};DrawShape(h,Green,origin,ppu);if(selectedDefaultHurtbox){Vector2 c=LocalToCanvas(draft.defaultHurtboxCenter,origin,ppu);Handles.color=Color.white;Handles.DrawSolidDisc(c,Vector3.forward,5);}}
            foreach(ActionFrameShape shape in draft.ShapesAt(frame))
            {
                if(shape.role==ActionShapeRole.Hurtbox&&!showHurt||shape.role==ActionShapeRole.DamageHitbox&&!showHit||shape.role==ActionShapeRole.EffectHitbox&&!showEffect||shape.role==ActionShapeRole.WeaponHitbox&&!showWeapon)continue;
                DrawShape(shape,shape.role==ActionShapeRole.Hurtbox?Green:shape.role==ActionShapeRole.DamageHitbox?Red:shape.role==ActionShapeRole.EffectHitbox?Blue:Purple,origin,ppu);
            }
            if(showWeapon&&draft.weapon.enabled){Vector2 p=LocalToCanvas(draft.weapon.socket,origin,ppu);Handles.color=Purple;Handles.DrawSolidDisc(p,Vector3.forward,5);}
            if(showMovement){Handles.color=Color.cyan;Handles.DrawLine(baseOrigin,origin);if(Vector2.Distance(baseOrigin,origin)>2f)Handles.ConeHandleCap(0,origin,Quaternion.LookRotation(Vector3.forward,origin-baseOrigin),8,EventType.Repaint);}
            if(showElevation){Handles.color=Color.magenta;Handles.DrawDottedLine(origin,elevatedOrigin,3);}
            Vector2 visualOffset=draft.VisualOffsetAt(frame);if(visualOffset.sqrMagnitude>.000001f||tool==DrawTool.FramePose){Handles.color=new Color(1f,.65f,.1f);Handles.DrawLine(elevatedOrigin,spriteOrigin);Handles.DrawSolidDisc(spriteOrigin,Vector3.forward,tool==DrawTool.FramePose?6:4);}
            if(pendingPolygon.Count>0){Handles.color=Blue;for(int i=1;i<pendingPolygon.Count;i++)Handles.DrawLine(LocalToCanvas(pendingPolygon[i-1],origin,ppu),LocalToCanvas(pendingPolygon[i],origin,ppu));}
            Handles.EndGUI();
            if(selectedDefaultHurtbox)GUI.Label(new Rect(canvas.x+8,canvas.yMax-28,300,20),"SELECTED: DEFAULT HURTBOX  •  Drag to move",EditorStyles.boldLabel);
            else if(selectedShape!=null)GUI.Label(new Rect(canvas.x+8,canvas.yMax-28,500,20),"SELECTED: "+selectedShape.role+" / "+selectedShape.shapeType+"  •  Drag to move  •  Delete removes",EditorStyles.boldLabel);
            else if(tool==DrawTool.FramePose)GUI.Label(new Rect(canvas.x+8,canvas.yMax-28,620,20),"FRAME POSE: drag anywhere to move only this frame's visual; gameplay root and hitboxes stay fixed",ColorLabel(new Color(1f,.65f,.1f)));
        }
        private void DrawShape(ActionFrameShape shape,Color color,Vector2 origin,float ppu)
        {
            bool isSelected=selectedShape==shape;Handles.color=isSelected?Color.white:color;
            if(shape.shapeType==ActionShapeType.Box){Vector2 c=LocalToCanvas(shape.center,origin,ppu),s=shape.size*ppu;if(isSelected){Vector3[] corners={c+new Vector2(-s.x,-s.y)*.5f,c+new Vector2(-s.x,s.y)*.5f,c+new Vector2(s.x,s.y)*.5f,c+new Vector2(s.x,-s.y)*.5f};Handles.DrawSolidRectangleWithOutline(corners,new Color(color.r,color.g,color.b,.13f),Color.white);Handles.DrawSolidDisc(c,Vector3.forward,5);}else Handles.DrawWireCube(c,s);return;}
            if(shape.points==null||shape.points.Count==0)return;
            var points=new Vector3[shape.points.Count+1];for(int i=0;i<shape.points.Count;i++){points[i]=LocalToCanvas(shape.points[i],origin,ppu);if(isSelected)Handles.DrawSolidDisc(points[i],Vector3.forward,i==selectedPoint?7:5);}points[points.Length-1]=points[0];Handles.DrawAAPolyLine(isSelected?5:3,points);
        }
        private void HandleCanvas(Rect canvas,Vector2 origin,Vector2 baseOrigin,float ppu,int controlId)
        {
            Event e=Event.current;EventType eventType=e.GetTypeForControl(controlId);
            if(tool==DrawTool.Polygon&&eventType==EventType.KeyDown)
            {
                if((e.control&&e.keyCode==KeyCode.Z)||e.keyCode==KeyCode.Backspace){if(pendingPolygon.Count>0)pendingPolygon.RemoveAt(pendingPolygon.Count-1);e.Use();Repaint();return;}
                if(e.keyCode==KeyCode.Escape){pendingPolygon.Clear();e.Use();Repaint();return;}
                if(e.keyCode==KeyCode.Return){if(pendingPolygon.Count>=3){MarkDirty("Create Polygon Hitbox");selectedShape=ActionEditorUtility.CreatePolygon(draft,frame,role,pendingPolygon);}pendingPolygon.Clear();e.Use();Repaint();return;}
            }
            if(eventType==EventType.MouseDown&&canvas.Contains(e.mousePosition)){GUIUtility.hotControl=controlId;GUIUtility.keyboardControl=controlId;Focus();}
            bool ownsMouse=GUIUtility.hotControl==controlId;if(!canvas.Contains(e.mousePosition)&&!ownsMouse)return;
            if(tool==DrawTool.FramePose)
            {
                if(eventType==EventType.MouseDown&&e.button==0){MarkDirty("Move Current Frame Visual");framePoseDragStartMouse=e.mousePosition;framePoseDragStartOffset=draft.VisualOffsetAt(frame);canvasInputStatus="Frame visual drag started";e.Use();}
                else if(eventType==EventType.MouseDrag&&e.button==0&&ownsMouse){Sprite current=frame>=0&&frame<frames.Count?frames[frame]:null;bool flip=ShouldFlipSprite(current,facingLeft);Vector2 screenDelta=e.mousePosition-framePoseDragStartMouse;Vector2 visualDelta=new Vector2(screenDelta.x*(flip?-1f:1f)/ppu,-screenDelta.y/ppu);draft.SetVisualOffset(frame,framePoseDragStartOffset+visualDelta);canvasInputStatus="Frame "+frame.ToString("00")+" visual offset "+draft.VisualOffsetAt(frame).ToString("F2");e.Use();Repaint();}
            }
            else if(tool==DrawTool.Box)
            {
                if(eventType==EventType.MouseDown&&e.button==0){canvasInputStatus="Box drag started";MarkDirty("Create Box Hitbox");dragStart=CanvasToLocal(e.mousePosition,origin,ppu);boxDragging=true;e.Use();}
                else if(eventType==EventType.MouseUp&&boxDragging){Vector2 end=CanvasToLocal(e.mousePosition,origin,ppu);Vector2 min=Vector2.Min(dragStart,end),max=Vector2.Max(dragStart,end);selectedShape=ActionEditorUtility.CreateBox(draft,frame,role,(min+max)*.5f,max-min);selectedDefaultHurtbox=false;boxDragging=false;GUIUtility.hotControl=0;canvasInputStatus="Created "+role+" box";e.Use();Repaint();}
            }
            else if(tool==DrawTool.Polygon)
            {
                if(eventType==EventType.MouseDown&&e.button==0){pendingPolygon.Add(CanvasToLocal(e.mousePosition,origin,ppu));GUIUtility.hotControl=0;canvasInputStatus="Polygon point "+pendingPolygon.Count;e.Use();Repaint();}
                if((eventType==EventType.KeyDown&&e.keyCode==KeyCode.Return)||eventType==EventType.MouseDown&&e.button==1)
                {if(pendingPolygon.Count>=3){MarkDirty("Create Polygon Hitbox");selectedShape=ActionEditorUtility.CreatePolygon(draft,frame,role,pendingPolygon);}pendingPolygon.Clear();e.Use();}
            }
            else if(tool==DrawTool.Select&&eventType==EventType.MouseDown&&e.button==0)
            {
                SelectShapeAt(e.mousePosition,origin,ppu);canvasInputStatus=selectedShape!=null?"Selected "+selectedShape.role:selectedDefaultHurtbox?"Selected green default HURT":"Click received; no shape at cursor";e.Use();Repaint();
            }
            else if(tool==DrawTool.Select&&eventType==EventType.MouseDown&&e.button==1)
            {
                Vector2 local=CanvasToLocal(e.mousePosition,origin,ppu);ActionFrameShape target=null;int point=-1;float best=.2f;
                foreach(ActionFrameShape s in draft.ShapesAt(frame))if(s.shapeType==ActionShapeType.Polygon&&s.points!=null)for(int i=0;i<s.points.Count;i++){float d=Vector2.Distance(local,s.points[i]);if(d<best){best=d;target=s;point=i;}}
                if(target!=null&&point>=0&&target.points.Count>3){MarkDirty("Delete Polygon Point");target.points.RemoveAt(point);selectedShape=target;selectedPoint=-1;e.Use();Repaint();}
            }
            else if(tool==DrawTool.Select&&(selectedShape!=null||selectedDefaultHurtbox)&&eventType==EventType.MouseDrag&&e.button==0&&ownsMouse)
            {
                MarkDirty("Move Hitbox");Vector2 local=CanvasToLocal(e.mousePosition,origin,ppu);if(selectedDefaultHurtbox)draft.defaultHurtboxCenter=local;else if(selectedShape.shapeType==ActionShapeType.Box)selectedShape.center=local;else if(selectedPoint>=0)selectedShape.points[selectedPoint]=local;e.Use();Repaint();
            }
            if(eventType==EventType.MouseUp&&ownsMouse){GUIUtility.hotControl=0;e.Use();Repaint();}
            if(eventType==EventType.KeyDown&&(e.keyCode==KeyCode.Delete||e.keyCode==KeyCode.Backspace)&&selectedShape!=null)
            {MarkDirty("Delete Hitbox Point");if(selectedShape.shapeType==ActionShapeType.Polygon&&selectedPoint>=0&&selectedShape.points.Count>3)selectedShape.points.RemoveAt(selectedPoint);else draft.frameShapes.Remove(selectedShape);selectedShape=null;e.Use();}
        }
        private void SelectShapeAt(Vector2 mouse,Vector2 origin,float ppu)
        {
            var hits=new List<ActionFrameShape>();
            foreach(ActionFrameShape shape in draft.ShapesAt(frame))
            {
                if(shape.role==ActionShapeRole.Hurtbox&&!showHurt||shape.role==ActionShapeRole.DamageHitbox&&!showHit||shape.role==ActionShapeRole.EffectHitbox&&!showEffect||shape.role==ActionShapeRole.WeaponHitbox&&!showWeapon)continue;
                if(ScreenShapeContains(shape,mouse,origin,ppu,7f))hits.Add(shape);
            }
            bool hitDefault=showHurt&&Expanded(HurtboxScreenRect(origin,ppu),7f).Contains(mouse);
            bool sameClick=Vector2.Distance(mouse,lastSelectionPosition)<5f;selectionCycle=sameClick?selectionCycle+1:0;lastSelectionPosition=mouse;
            selectedShape=null;selectedDefaultHurtbox=false;selectedPoint=-1;
            int total=hits.Count+(hitDefault?1:0);if(total==0)return;
            int index=selectionCycle%total;
            // Frame attack/effect shapes deliberately come first; repeated clicks cycle to the green default hurtbox.
            if(index<hits.Count)selectedShape=hits[hits.Count-1-index];else selectedDefaultHurtbox=true;
            if(selectedShape!=null&&selectedShape.shapeType==ActionShapeType.Polygon&&selectedShape.points!=null)
            {
                float best=12f;for(int i=0;i<selectedShape.points.Count;i++){float distance=Vector2.Distance(mouse,LocalToCanvas(selectedShape.points[i],origin,ppu));if(distance<best){best=distance;selectedPoint=i;}}
            }
        }
        private bool ScreenShapeContains(ActionFrameShape shape,Vector2 mouse,Vector2 origin,float ppu,float tolerance)
        {
            if(shape==null||!shape.enabled)return false;
            if(shape.shapeType==ActionShapeType.Box){Vector2 c=LocalToCanvas(shape.center,origin,ppu),s=shape.size*ppu;return Expanded(new Rect(c-s*.5f,s),tolerance).Contains(mouse);}
            if(shape.points==null||shape.points.Count<2)return false;
            var screenPoints=new Vector2[shape.points.Count];for(int i=0;i<shape.points.Count;i++)screenPoints[i]=LocalToCanvas(shape.points[i],origin,ppu);
            bool inside=false;if(screenPoints.Length>=3)for(int i=0,j=screenPoints.Length-1;i<screenPoints.Length;j=i++)if(((screenPoints[i].y>mouse.y)!=(screenPoints[j].y>mouse.y))&&mouse.x<(screenPoints[j].x-screenPoints[i].x)*(mouse.y-screenPoints[i].y)/((screenPoints[j].y-screenPoints[i].y)+Mathf.Epsilon)+screenPoints[i].x)inside=!inside;
            if(inside)return true;for(int i=0;i<screenPoints.Length;i++)if(DistanceToSegment(mouse,screenPoints[i],screenPoints[(i+1)%screenPoints.Length])<=tolerance)return true;return false;
        }
        private static float DistanceToSegment(Vector2 p,Vector2 a,Vector2 b){Vector2 ab=b-a;float length=ab.sqrMagnitude;if(length<.0001f)return Vector2.Distance(p,a);float t=Mathf.Clamp01(Vector2.Dot(p-a,ab)/length);return Vector2.Distance(p,a+ab*t);}
        private static Rect Expanded(Rect rect,float amount){return new Rect(rect.x-amount,rect.y-amount,rect.width+amount*2,rect.height+amount*2);}
        private void DrawSettings(Rect rect)
        {
            GUI.Box(rect,GUIContent.none);GUI.Label(new Rect(rect.x+8,rect.y+7,rect.width-16,20),"ACTION SETTINGS",EditorStyles.boldLabel);if(draft==null){GUI.Label(new Rect(rect.x+8,rect.y+32,rect.width-16,20),"No action selected.");return;}
            if(Event.current.type==EventType.MouseDown&&rect.Contains(Event.current.mousePosition))Undo.RecordObject(draft,"Edit Action Settings");
            Rect scrollRect=new Rect(rect.x+3,rect.y+28,rect.width-6,rect.height-31),viewRect=new Rect(0,0,rect.width-23,1460);settingsScroll=GUI.BeginScrollView(scrollRect,settingsScroll,viewRect);
            float x=5,y=2,w=viewRect.width-10,h=18,label=105;EditorGUI.BeginChangeCheck();
            draft.id=EditorGUI.TextField(Row(ref y,x,w,h,"Action ID",label),draft.id);draft.displayName=EditorGUI.TextField(Row(ref y,x,w,h,"Display Name",label),draft.displayName);
            draft.category=(ActionCategory)EditorGUI.EnumPopup(Row(ref y,x,w,h,"Category",label),draft.category);draft.loop=EditorGUI.Toggle(Row(ref y,x,w,h,"Loop",label),draft.loop);draft.lockGroundPosition=EditorGUI.Toggle(Row(ref y,x,w,h,"Lock Ground",label),draft.lockGroundPosition);
            draft.framesPerSecond=Mathf.Max(1f,EditorGUI.FloatField(Row(ref y,x,w,h,"Action FPS",label),draft.framesPerSecond));draft.frameCount=EditorGUI.IntField(Row(ref y,x,w,h,"Frame Count",label),draft.frameCount);
            EditorGUI.LabelField(Row(ref y,x,w,h,"Frame Time",label),(1000f/draft.framesPerSecond).ToString("0.##")+" ms");EditorGUI.LabelField(Row(ref y,x,w,h,"Action Duration",label),(draft.frameCount/draft.framesPerSecond).ToString("0.###")+" s");
            draft.startupEndFrame=EditorGUI.IntField(Row(ref y,x,w,h,"Active Start Frame",label),draft.startupEndFrame);draft.activeEndFrame=EditorGUI.IntField(Row(ref y,x,w,h,"Active End Frame",label),draft.activeEndFrame);draft.recoveryEndFrame=EditorGUI.IntField(Row(ref y,x,w,h,"Action End Frame",label),draft.recoveryEndFrame);
            DrawSelectedShapeSettings(ref y,x,w,h,label);
            draft.defaultHurtboxCenter=EditorGUI.Vector2Field(Row(ref y,x,w,h,"Hurtbox Center",label),GUIContent.none,draft.defaultHurtboxCenter);draft.defaultHurtboxSize=EditorGUI.Vector2Field(Row(ref y,x,w,h,"Hurtbox Size",label),GUIContent.none,draft.defaultHurtboxSize);
            bool editHurtbox=GUI.Toggle(new Rect(x,y,w,20),selectedDefaultHurtbox,"EDIT HURTBOX",EditorStyles.miniButton);if(editHurtbox!=selectedDefaultHurtbox){selectedDefaultHurtbox=editHurtbox;if(editHurtbox){selectedShape=null;selectedPoint=-1;}Repaint();}y+=24;
            bool hitReactionAction=draft.category==ActionCategory.Reaction&&!string.IsNullOrEmpty(draft.id)&&draft.id.StartsWith("HitReact",StringComparison.OrdinalIgnoreCase);
            if(hitReactionAction)DrawReactionSettings(ref y,x,w,h,label);
            else{EditorGUI.LabelField(new Rect(x,y,w,h),"COMBAT  (CURRENT ACTION ONLY)",EditorStyles.miniBoldLabel);y+=20;draft.combat.damage=EditorGUI.FloatField(Row(ref y,x,w,h,"Action Damage",label),draft.combat.damage);draft.combat.hitStop=EditorGUI.FloatField(Row(ref y,x,w,h,"Hit Stop",label),draft.combat.hitStop);draft.combat.impactType=(AttackImpactType)EditorGUI.EnumPopup(Row(ref y,x,w,h,"Reaction Weight",label),draft.combat.impactType);draft.combat.cooldown=EditorGUI.FloatField(Row(ref y,x,w,h,"Cooldown",label),draft.combat.cooldown);draft.combat.priority=(ActionPriority)EditorGUI.EnumPopup(Row(ref y,x,w,h,"Priority",label),draft.combat.priority);draft.combat.hitPolicy=(ActionHitPolicy)EditorGUI.EnumPopup(Row(ref y,x,w,h,"Hit Policy",label),draft.combat.hitPolicy);}
            EditorGUI.LabelField(new Rect(x,y,w,h),"CURRENT FRAME VISUAL POSITION",EditorStyles.miniBoldLabel);y+=20;
            Vector2 currentVisual=draft.VisualOffsetAt(frame),nextVisual=EditorGUI.Vector2Field(Row(ref y,x,w,h,"Visual Offset",label),GUIContent.none,currentVisual);if(nextVisual!=currentVisual){MarkDirty("Edit Current Frame Visual");draft.SetVisualOffset(frame,nextVisual);}
            EditorGUI.LabelField(new Rect(x,y,w,h),"Moves Sprite only; Root / Hurtbox / Hitbox stay fixed.",EditorStyles.wordWrappedMiniLabel);y+=28;
            if(GUI.Button(new Rect(x,y,(w-10)/3f,20),"RESET FRAME",EditorStyles.miniButton)){MarkDirty("Reset Frame Visual");draft.SetVisualOffset(frame,Vector2.zero);}
            if(GUI.Button(new Rect(x+(w-10)/3f+5,y,(w-10)/3f,20),"COPY PREV",EditorStyles.miniButton)){MarkDirty("Copy Previous Frame Visual");draft.SetVisualOffset(frame,draft.VisualOffsetAt(Mathf.Max(0,frame-1)));}
            if(GUI.Button(new Rect(x+2*((w-10)/3f+5),y,(w-10)/3f,20),"COPY RANGE",EditorStyles.miniButton)){MarkDirty("Copy Frame Visual Range");Vector2 value=draft.VisualOffsetAt(frame);for(int target=Mathf.Max(0,copyFrom);target<=Mathf.Min(draft.frameCount-1,copyTo);target++)draft.SetVisualOffset(target,value);}y+=27;
            EditorGUI.LabelField(new Rect(x,y,w,h),"MOVEMENT (Depth ≠ Elevation)",EditorStyles.miniBoldLabel);y+=20;draft.movement.moveX=EditorGUI.CurveField(Row(ref y,x,w,h,"Root X",label),draft.movement.moveX);draft.movement.moveDepth=EditorGUI.CurveField(Row(ref y,x,w,h,"Root Depth",label),draft.movement.moveDepth);draft.movement.elevation=EditorGUI.CurveField(Row(ref y,x,w,h,"Elevation",label),draft.movement.elevation);
            DrawComboWindowSettings(ref y,x,w,h);
            if(draft.ownerId=="Soldier"||draft.ownerId=="CaoCao"){EditorGUI.LabelField(new Rect(x,y,w,h),"AI USAGE",EditorStyles.miniBoldLabel);y+=20;draft.ai.enabledForAI=EditorGUI.Toggle(Row(ref y,x,w,h,"Enabled For AI",label),draft.ai.enabledForAI);draft.ai.usageWeight=EditorGUI.FloatField(Row(ref y,x,w,h,"Usage Weight",label),draft.ai.usageWeight);draft.ai.minDistance=EditorGUI.FloatField(Row(ref y,x,w,h,"Min Distance",label),draft.ai.minDistance);draft.ai.maxDistance=EditorGUI.FloatField(Row(ref y,x,w,h,"Max Distance",label),draft.ai.maxDistance);draft.ai.depthTolerance=EditorGUI.FloatField(Row(ref y,x,w,h,"Depth Tolerance",label),draft.ai.depthTolerance);}
            if(EditorGUI.EndChangeCheck()){dirty=true;titleContent=new GUIContent("Battle Editor *");Repaint();}
            GUI.Label(new Rect(x,y+8,w,18),"WeaponPose: NOT IMPLEMENTED (V0.1)",EditorStyles.miniLabel);GUI.EndScrollView();
        }
        private void DrawReactionSettings(ref float y,float x,float w,float h,float label)
        {
            if(draft.reaction==null)draft.reaction=new ActionReactionData();
            EditorGUI.LabelField(new Rect(x,y,w,h),"REACTION RESPONSE  •  CURRENT CHARACTER ONLY",EditorStyles.miniBoldLabel);y+=20;
            EditorGUI.HelpBox(new Rect(x,y,w,44),"Light and Heavy reactions are independent for this character. Set Stun Seconds and Retreat Distance directly; 0 retreat keeps the shadow/root still.",MessageType.Info);y+=49;
            DrawReactionProfile(ref y,x,w,h,label,"LIGHT WEAPON / LIGHT HIT",draft.reaction.light,ref showLightReaction);
            DrawReactionProfile(ref y,x,w,h,label,"HEAVY WEAPON / HEAVY HIT",draft.reaction.heavy,ref showHeavyReaction);
        }
        private static void DrawReactionProfile(ref float y,float x,float w,float h,float label,string title,HitReactionProfile profile,ref bool expanded)
        {
            if(profile==null)return;EditorGUI.DrawRect(new Rect(x,y,w,expanded?116:24),new Color(.16f,.13f,.08f,.92f));expanded=EditorGUI.Foldout(new Rect(x+5,y+3,w-10,20),expanded,title,true,new GUIStyle(EditorStyles.foldout){fontStyle=FontStyle.Bold});y+=26;if(!expanded)return;
            profile.animationActionId=EditorGUI.TextField(Row(ref y,x+5,w-10,h,"Reaction Action",label),profile.animationActionId);
            profile.stunDuration=Mathf.Max(.01f,EditorGUI.FloatField(Row(ref y,x+5,w-10,h,"Stun Seconds",label),profile.stunDuration));
            profile.retreatDistance=Mathf.Max(0f,EditorGUI.FloatField(Row(ref y,x+5,w-10,h,"Retreat Distance",label),profile.retreatDistance));
            EditorGUI.LabelField(new Rect(x+8,y,w-16,18),"This distance changes only this character and this hit weight.",EditorStyles.miniLabel);y+=22;
        }
        private void DrawComboWindowSettings(ref float y,float x,float w,float h)
        {
            if(draft.combo==null||draft.combo.Count==0)return;
            EditorGUI.DrawRect(new Rect(x,y,w,showComboWindowEditor?286:24),new Color(.12f,.18f,.30f,.9f));
            showComboWindowEditor=EditorGUI.Foldout(new Rect(x+5,y+3,w-10,20),showComboWindowEditor,"COMBO INPUT WINDOW  •  RUNTIME",true,new GUIStyle(EditorStyles.foldout){fontStyle=FontStyle.Bold});y+=25;
            if(!showComboWindowEditor)return;
            EditorGUI.LabelField(new Rect(x+7,y,w-14,18),"Buffered before START; accepted until END.  FPS is not the input window.",EditorStyles.wordWrappedMiniLabel);y+=29;
            bool legacy=ActionEditorUtility.HasLegacyComboFrames(draft);
            if(legacy)
            {
                EditorGUI.HelpBox(new Rect(x+5,y,w-10,38),"Legacy 60 FPS values exceed this "+draft.frameCount+"-frame animation.",MessageType.Warning);y+=41;
                if(GUI.Button(new Rect(x+5,y,w-10,21),"CONVERT LEGACY VALUES TO ANIMATION FRAMES",EditorStyles.miniButton)){MarkDirty("Convert Legacy Combo Windows");ActionEditorUtility.ConvertLegacyComboFrames(draft);}y+=26;
            }
            int count=Mathf.Min(4,draft.combo.Count),last=Mathf.Max(0,draft.frameCount-1);selectedComboSegment=Mathf.Clamp(selectedComboSegment,0,count-1);
            for(int i=0;i<count;i++)
            {
                ActionComboSegment s=draft.combo[i];Color old=GUI.backgroundColor;if(i==selectedComboSegment)GUI.backgroundColor=new Color(.35f,.65f,1f);
                if(GUI.Button(new Rect(x+5,y,70,19),"Segment "+(i+1),EditorStyles.miniButton))selectedComboSegment=i;GUI.backgroundColor=old;
                EditorGUI.LabelField(new Rect(x+80,y,35,19),"Start");s.comboWindowStart=EditorGUI.IntField(new Rect(x+116,y,38,19),s.comboWindowStart);
                EditorGUI.LabelField(new Rect(x+159,y,28,19),"End");s.comboWindowEnd=EditorGUI.IntField(new Rect(x+188,y,38,19),s.comboWindowEnd);
                float seconds=(Mathf.Max(0,s.comboWindowEnd-s.comboWindowStart)+1)/Mathf.Max(1f,draft.framesPerSecond);EditorGUI.LabelField(new Rect(x+231,y,w-236,19),seconds.ToString("0.000")+"s",EditorStyles.miniLabel);y+=21;
                float min=s.comboWindowStart,max=s.comboWindowEnd;EditorGUI.MinMaxSlider(new Rect(x+80,y,w-88,16),ref min,ref max,0,last);s.comboWindowStart=Mathf.RoundToInt(min);s.comboWindowEnd=Mathf.RoundToInt(max);ActionEditorUtility.ClampComboSegment(draft,s);y+=20;
            }
            ActionComboSegment selectedSegment=draft.combo[selectedComboSegment];
            if(GUI.Button(new Rect(x+5,y,(w-15)*.5f,20),"CURRENT FRAME = START",EditorStyles.miniButton)){MarkDirty("Set Combo Window Start");selectedSegment.comboWindowStart=Mathf.Min(frame,selectedSegment.comboWindowEnd);ActionEditorUtility.ClampComboSegment(draft,selectedSegment);}
            if(GUI.Button(new Rect(x+10+(w-15)*.5f,y,(w-15)*.5f,20),"CURRENT FRAME = END",EditorStyles.miniButton)){MarkDirty("Set Combo Window End");selectedSegment.comboWindowEnd=Mathf.Max(frame,selectedSegment.comboWindowStart);ActionEditorUtility.ClampComboSegment(draft,selectedSegment);}y+=24;
            if(GUI.Button(new Rect(x+5,y,w-10,20),"FORGIVING DEFAULTS (HIT END → SEGMENT END)",EditorStyles.miniButton))
            {
                MarkDirty("Apply Forgiving Combo Windows");foreach(ActionComboSegment s in draft.combo){s.comboWindowStart=Mathf.Clamp(s.hitEndFrame,s.startFrame,s.endFrame);s.comboWindowEnd=s.endFrame;ActionEditorUtility.ClampComboSegment(draft,s);}
            }
            y+=27;
        }
        private void DrawSelectedShapeSettings(ref float y,float x,float w,float h,float label)
        {
            EditorGUI.DrawRect(new Rect(x,y,w,selectedShape==null&&!selectedDefaultHurtbox?42:selectedShape!=null?190:112),new Color(.12f,.22f,.28f,.8f));y+=4;
            string title=selectedShape!=null?"SELECTED FRAME SHAPE":selectedDefaultHurtbox?"SELECTED DEFAULT HURTBOX":"SELECT A RED / BLUE / GREEN FRAME SHAPE";
            EditorGUI.LabelField(new Rect(x+4,y,w-8,h),title,EditorStyles.boldLabel);y+=21;
            if(selectedShape!=null)
            {
                selectedShape.enabled=EditorGUI.Toggle(Row(ref y,x+4,w-8,h,"Enabled",label-4),selectedShape.enabled);
                selectedShape.role=(ActionShapeRole)EditorGUI.EnumPopup(Row(ref y,x+4,w-8,h,"Role",label-4),selectedShape.role);
                if(selectedShape.role!=ActionShapeRole.Hurtbox){selectedShape.overrideDamage=EditorGUI.Toggle(Row(ref y,x+4,w-8,h,"Override Damage",label-4),selectedShape.overrideDamage);if(selectedShape.overrideDamage)selectedShape.damage=EditorGUI.FloatField(Row(ref y,x+4,w-8,h,"Shape Damage",label-4),selectedShape.damage);else{EditorGUI.LabelField(new Rect(x+4,y,w-8,h),"Uses this Action's Damage: "+draft.combat.damage.ToString("0.##"),EditorStyles.miniLabel);y+=20;}}
                if(selectedShape.shapeType==ActionShapeType.Box){selectedShape.center=EditorGUI.Vector2Field(Row(ref y,x+4,w-8,h,"Center",label-4),GUIContent.none,selectedShape.center);selectedShape.size=EditorGUI.Vector2Field(Row(ref y,x+4,w-8,h,"Size",label-4),GUIContent.none,selectedShape.size);}
                else
                {
                    EditorGUI.LabelField(new Rect(x+4,y,w-8,h),"Polygon points: "+(selectedShape.points==null?0:selectedShape.points.Count)+" (drag white vertices)");y+=20;
                    if(selectedPoint>=0&&selectedShape.points!=null&&selectedPoint<selectedShape.points.Count)selectedShape.points[selectedPoint]=EditorGUI.Vector2Field(Row(ref y,x+4,w-8,h,"Point "+selectedPoint,label-4),GUIContent.none,selectedShape.points[selectedPoint]);
                }
                if(GUI.Button(new Rect(x+4,y,w-8,20),"DELETE SELECTED FRAME SHAPE",EditorStyles.miniButton)){MarkDirty("Delete Selected Hitbox");draft.frameShapes.Remove(selectedShape);selectedShape=null;selectedPoint=-1;}y+=25;
            }
            else if(selectedDefaultHurtbox)
            {
                draft.defaultHurtboxCenter=EditorGUI.Vector2Field(Row(ref y,x+4,w-8,h,"Center",label-4),GUIContent.none,draft.defaultHurtboxCenter);draft.defaultHurtboxSize=EditorGUI.Vector2Field(Row(ref y,x+4,w-8,h,"Size",label-4),GUIContent.none,draft.defaultHurtboxSize);y+=5;
            }
            else {EditorGUI.LabelField(new Rect(x+4,y,w-8,h),"Select tool: click inside or near an outline.",EditorStyles.miniLabel);y+=21;}
            y+=6;
        }
        private static Rect Row(ref float y,float x,float width,float height,string text,float labelWidth){EditorGUI.LabelField(new Rect(x,y,labelWidth,height),text);Rect r=new Rect(x+labelWidth,y,width-labelWidth,height);y+=20;return r;}
        private Rect HurtboxScreenRect(Vector2 origin,float ppu){Vector2 center=LocalToCanvas(draft.defaultHurtboxCenter,origin,ppu),size=draft.defaultHurtboxSize*ppu;return new Rect(center-size*.5f,size);}
        private void DrawTimeline()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);EditorGUILayout.BeginHorizontal();GUILayout.Label("FRAME TIMELINE",EditorStyles.boldLabel,GUILayout.Width(125));GUILayout.Label("Startup",ColorLabel(new Color(.8f,.55f,.15f)));GUILayout.Label("Active",ColorLabel(new Color(.75f,.15f,.15f)));GUILayout.Label("Recovery",ColorLabel(new Color(.25f,.45f,.75f)));GUILayout.FlexibleSpace();GUILayout.Label($"Current Frame: {frame:00}",EditorStyles.boldLabel);EditorGUILayout.EndHorizontal();
            if(draft!=null)
            {
                EditorGUILayout.BeginHorizontal();for(int i=0;i<Mathf.Min(draft.frameCount,40);i++){Color old=GUI.backgroundColor;GUI.backgroundColor=i<draft.startupEndFrame?new Color(.8f,.55f,.15f):i<=draft.activeEndFrame?new Color(.75f,.15f,.15f):new Color(.25f,.45f,.75f);bool chooseFrame=GUILayout.Toggle(i==frame,i.ToString("00"),"Button",GUILayout.MinWidth(26));if(chooseFrame&&i!=frame)SetFrame(i);GUI.backgroundColor=old;}EditorGUILayout.EndHorizontal();
                EditorGUILayout.BeginHorizontal();GUILayout.Label("Hitbox",GUILayout.Width(55));for(int i=0;i<Mathf.Min(draft.frameCount,40);i++){bool has=HasAttackShape(i);GUILayout.Label(has?"■":"·",new GUIStyle(EditorStyles.miniLabel){alignment=TextAnchor.MiddleCenter,normal={textColor=has?Red:Color.gray}},GUILayout.MinWidth(26));}EditorGUILayout.EndHorizontal();
                if(draft.combo!=null&&draft.combo.Count>0){EditorGUILayout.BeginHorizontal();GUILayout.Label("Combo J",GUILayout.Width(55));for(int i=0;i<Mathf.Min(draft.frameCount,40);i++){bool accepts=false;foreach(ActionComboSegment s in draft.combo)if(i>=s.comboWindowStart&&i<=s.comboWindowEnd){accepts=true;break;}GUILayout.Label(accepts?"◆":"·",new GUIStyle(EditorStyles.miniLabel){alignment=TextAnchor.MiddleCenter,normal={textColor=accepts?Purple:Color.gray}},GUILayout.MinWidth(26));}EditorGUILayout.EndHorizontal();}
                EditorGUILayout.BeginHorizontal();GUILayout.Label($"Frame {frame:00}",EditorStyles.boldLabel,GUILayout.Width(65));bool currentHas=HasAttackShape(frame);
                if(GUILayout.Button(currentHas?"REPLACE FROM NEAREST":"ADD ATTACK FRAME",EditorStyles.miniButton,GUILayout.Width(145)))AddCurrentAttackFrame(true);
                GUI.enabled=currentHas;if(GUILayout.Button("REMOVE FRAME ATTACKS",EditorStyles.miniButton,GUILayout.Width(145)))RemoveCurrentAttackFrame();GUI.enabled=true;
                if(GUILayout.Button("SET ACTIVE START",EditorStyles.miniButton,GUILayout.Width(115))){MarkDirty("Set Active Start");draft.startupEndFrame=frame;if(draft.activeEndFrame<frame)draft.activeEndFrame=frame;}
                if(GUILayout.Button("SET ACTIVE END",EditorStyles.miniButton,GUILayout.Width(105))){MarkDirty("Set Active End");draft.activeEndFrame=Mathf.Max(frame,draft.startupEndFrame);}
                GUILayout.Space(8);GUILayout.Label("Copy current to",GUILayout.Width(90));copyFrom=EditorGUILayout.IntField(copyFrom,GUILayout.Width(35));GUILayout.Label("–",GUILayout.Width(10));copyTo=EditorGUILayout.IntField(copyTo,GUILayout.Width(35));
                GUI.enabled=currentHas;if(GUILayout.Button("COPY TO RANGE",EditorStyles.miniButton,GUILayout.Width(105))){MarkDirty("Copy Shape Range");ActionEditorUtility.CopyRange(draft,frame,copyFrom,copyTo);}GUI.enabled=true;EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndVertical();
        }
        private bool HasAttackShape(int targetFrame)=>draft!=null&&draft.frameShapes.Exists(s=>s!=null&&s.enabled&&s.frame==targetFrame&&s.role!=ActionShapeRole.Hurtbox);
        private void AddCurrentAttackFrame(bool replace)
        {
            if(draft==null)return;MarkDirty("Add Attack Frame");if(replace)draft.frameShapes.RemoveAll(s=>s!=null&&s.frame==frame&&s.role!=ActionShapeRole.Hurtbox);
            int source=-1;for(int distance=1;distance<draft.frameCount&&source<0;distance++){int before=frame-distance,after=frame+distance;if(before>=0&&HasAttackShape(before))source=before;else if(after<draft.frameCount&&HasAttackShape(after))source=after;}
            if(source>=0){var copies=draft.frameShapes.FindAll(s=>s!=null&&s.frame==source&&s.role!=ActionShapeRole.Hurtbox);foreach(ActionFrameShape original in copies){ActionFrameShape copy=original.Clone();copy.frame=frame;draft.frameShapes.Add(copy);selectedShape=copy;}}
            else selectedShape=ActionEditorUtility.CreateBox(draft,frame,role==ActionShapeRole.Hurtbox?ActionShapeRole.DamageHitbox:role,new Vector2(1f,1f),new Vector2(1.5f,1.8f));
            if(frame<draft.startupEndFrame)draft.startupEndFrame=frame;if(frame>draft.activeEndFrame)draft.activeEndFrame=frame;draft.recoveryEndFrame=Mathf.Max(draft.recoveryEndFrame,frame);selectedDefaultHurtbox=false;Repaint();
        }
        private void RemoveCurrentAttackFrame(){if(draft==null)return;MarkDirty("Remove Attack Frame");draft.frameShapes.RemoveAll(s=>s!=null&&s.frame==frame&&s.role!=ActionShapeRole.Hurtbox);selectedShape=null;selectedPoint=-1;Repaint();}
        private static GUIStyle ColorLabel(Color c){return new GUIStyle(EditorStyles.miniBoldLabel){normal={textColor=c}};}
        private void DrawFooter()
        {
            if(validation.Count>0){bool error=validation.Exists(x=>x.StartsWith("ERROR"));EditorGUILayout.HelpBox(string.Join("  •  ",validation),error?MessageType.Error:validation.Exists(x=>x.StartsWith("WARNING"))?MessageType.Warning:MessageType.Info);}
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);GUI.enabled=draft!=null;
            if(GUILayout.Button("◀ Prev",EditorStyles.toolbarButton,GUILayout.Width(65)))SetFrame(frame-1);if(GUILayout.Button("Next ▶",EditorStyles.toolbarButton,GUILayout.Width(65)))SetFrame(frame+1);if(GUILayout.Button(playing?"Pause Action":"Play Action",EditorStyles.toolbarButton,GUILayout.Width(82))){playing=!playing;fullActionPreview=true;lastUpdate=EditorApplication.timeSinceStartup;playbackFrameAccumulator=0;}if(GUILayout.Button("Stop",EditorStyles.toolbarButton,GUILayout.Width(45))){playing=false;playbackFrameAccumulator=0;SetFrame(0);}GUILayout.Label("Speed",EditorStyles.miniLabel,GUILayout.Width(38));playbackSpeed=EditorGUILayout.Slider(playbackSpeed,.25f,3f,GUILayout.Width(105));GUILayout.Label(playbackSpeed.ToString("0.00")+"x",EditorStyles.miniLabel,GUILayout.Width(38));if(GUILayout.Button("1x",EditorStyles.toolbarButton,GUILayout.Width(28)))playbackSpeed=1f;
            facingLeft=GUILayout.Toggle(facingLeft,facingLeft?"Facing Left":"Facing Right",EditorStyles.toolbarButton,GUILayout.Width(90));loop=GUILayout.Toggle(loop,"Loop",EditorStyles.toolbarButton,GUILayout.Width(45));
            if(GUILayout.Button("Copy Previous",EditorStyles.toolbarButton,GUILayout.Width(95))){MarkDirty("Copy Previous Shapes");ActionEditorUtility.CopyPrevious(draft,frame);}copyFrom=EditorGUILayout.IntField(copyFrom,GUILayout.Width(28));copyTo=EditorGUILayout.IntField(copyTo,GUILayout.Width(28));if(GUILayout.Button("Copy Range",EditorStyles.toolbarButton,GUILayout.Width(75))){MarkDirty("Copy Shape Range");ActionEditorUtility.CopyRange(draft,frame,copyFrom,copyTo);}
            GUILayout.FlexibleSpace();if(GUILayout.Button("VALIDATE",EditorStyles.toolbarButton,GUILayout.Width(75))){validation=ActionEditorUtility.ValidateAction(draft);ShowValidation();}if(GUILayout.Button("EMBEDDED TEST",EditorStyles.toolbarButton,GUILayout.Width(105))){workspaceMode=WorkspaceMode.EmbeddedTest;playing=false;frame=0;ResetEmbeddedTest();fullActionPreview=true;}if(GUILayout.Button("SAVE",EditorStyles.toolbarButton,GUILayout.Width(60)))SaveDraft();GUI.enabled=true;EditorGUILayout.EndHorizontal();
        }
        private void ShowValidation(){string message=string.Join("\n",validation);EditorUtility.DisplayDialog(validation.Exists(x=>x.StartsWith("ERROR"))?"Validation Errors":"Validation Result",message,"OK");}
    }
}
