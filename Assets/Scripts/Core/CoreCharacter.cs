using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ThreeKingdoms
{
    public interface ICharacterDamageResponder
    {
        bool SuppressKnockback { get; }
        bool HandlesDeath { get; }
        void OnDamageReceived(DamagePacket packet);
        void OnDeath();
    }

    [Serializable]
    public struct GamePosition
    {
        public float x, depth, elevation;
        public GamePosition(float x, float depth, float elevation = 0f) { this.x = x; this.depth = depth; this.elevation = elevation; }
        public Vector3 GroundWorld => new Vector3(x, depth, 0f);
    }

    public enum CharacterTeam { Player, Enemy }

    public sealed partial class CharacterIdentity : MonoBehaviour
    {
        [SerializeField] private CharacterTeam team;
        public CharacterTeam Team => team;
        public void Configure(CharacterTeam value) => team = value;
    }

    public sealed partial class CharacterMotor : MonoBehaviour
    {
        [SerializeField] private Transform visualRoot, shadowRoot;
        [SerializeField] private CharacterVisualScale visualScale = new CharacterVisualScale();
        [SerializeField] private float walkSpeed = 2.8f, runSpeed = 4.6f, depthSpeedScale = .72f;
        [SerializeField] private float depthMin = -2.55f, depthMax = 1.45f, gravity = 18f, jumpVelocity = 7.2f;
        private float elevationVelocity, jumpElapsed, minX = -1000f, maxX = 1000f;
        private StageNavigationMap navigationMap;
        private Vector2 actionVisualOffset;
        public GamePosition Position { get; private set; }
        public float X => Position.x;
        public float Depth => Position.depth;
        public float Elevation => Position.elevation;
        public bool IsAirborne => Position.elevation > .001f || elevationVelocity > 0f;
        public float JumpNormalizedTime=>IsAirborne?Mathf.Clamp01(jumpElapsed/Mathf.Max(.01f,2f*jumpVelocity/gravity)):1f;
        public int Facing { get; private set; } = 1;
        public float VisualScale => visualScale == null ? 1f : visualScale.Character;
        public Vector2 ActionVisualOffset=>actionVisualOffset;

        private void Awake()
        {
            if (visualRoot == null) visualRoot = transform.Find("VisualRoot");
            if (shadowRoot == null) shadowRoot = transform.Find("Shadow");
            if (visualScale == null) visualScale = new CharacterVisualScale();
            visualScale.Apply(visualRoot, shadowRoot);
            Position = new GamePosition(transform.position.x, transform.position.y, 0f);
            ApplyPosition();
        }

        private void Update() => ManualTick(Time.deltaTime);

        public void ManualTick(float deltaTime)
        {
            if (!IsAirborne) return;
            jumpElapsed+=Mathf.Max(0f,deltaTime);
            elevationVelocity -= gravity * deltaTime;
            Position = new GamePosition(Position.x, Position.depth, Mathf.Max(0f, Position.elevation + elevationVelocity * deltaTime));
            if (Position.elevation <= 0f && elevationVelocity < 0f) { Position = new GamePosition(Position.x, Position.depth, 0f); elevationVelocity = 0f; }
            ApplyPosition();
        }

        public void Move(Vector2 input, bool run) => MoveForTest(input, run, Time.deltaTime);

        public void MoveForTest(Vector2 input, bool run, float deltaTime)
        {
            if (input.sqrMagnitude > 1f) input.Normalize();
            float speed = run ? runSpeed : walkSpeed;
            Position = new GamePosition(
                Mathf.Clamp(Position.x + input.x * speed * deltaTime, minX, maxX),
                Mathf.Clamp(Position.depth + input.y * speed * depthSpeedScale * deltaTime, depthMin, depthMax),
                Position.elevation);
            if (Mathf.Abs(input.x) > .01f) Facing = input.x > 0 ? 1 : -1;
            ApplyPosition();
        }

        public bool Jump()
        {
            if (IsAirborne) return false;
            elevationVelocity = jumpVelocity;jumpElapsed=0f;
            Position = new GamePosition(Position.x, Position.depth, .01f);
            ApplyPosition();
            return true;
        }

        public void ApplyGroundImpulse(float x, float depth)
        {
            Position = new GamePosition(Mathf.Clamp(Position.x + x, minX, maxX), Mathf.Clamp(Position.depth + depth, depthMin, depthMax), Position.elevation);
            ApplyPosition();
        }

        public void SetPosition(float x, float depth)
        {
            Position = new GamePosition(Mathf.Clamp(x, minX, maxX), Mathf.Clamp(depth, depthMin, depthMax), Position.elevation);
            ApplyPosition();
        }

        public void SetHorizontalBounds(float minimum, float maximum) { minX = minimum; maxX = maximum; SetPosition(Position.x, Position.depth); }
        public void SetDepthBounds(float minimum, float maximum) { depthMin = minimum; depthMax = maximum; SetPosition(Position.x, Position.depth); }
        public void Face(int direction){if(direction!=0)Facing=direction<0?-1:1;}
        public void SetActionVisualOffset(Vector2 value){actionVisualOffset=value;ApplyPosition();}

        private void ApplyPosition()
        {
            if(navigationMap==null)navigationMap=FindFirstObjectByType<StageNavigationMap>();
            Vector2 constrained=navigationMap==null?new Vector2(Position.x,Position.depth):navigationMap.Constrain(new Vector2(transform.position.x,transform.position.y),new Vector2(Position.x,Position.depth));
            Position=new GamePosition(Mathf.Clamp(constrained.x,minX,maxX),constrained.y,Position.elevation);
            transform.position = new Vector3(Position.x, Position.depth, 0f);
            if (visualRoot != null) visualRoot.localPosition = new Vector3(actionVisualOffset.x, Position.elevation+actionVisualOffset.y, 0f);
            if (shadowRoot != null) shadowRoot.localPosition = Vector3.zero;
        }
    }

    public sealed partial class CharacterAnimator : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private string idleState = "CombatIdle";
        private int facingDirection=1;
        private Sprite forcedFrame;
        private CharacterMotor characterMotor;
        private ActionRunner actionRunner;
        private ActionDefinition dataDrivenAction;
        private float dataDrivenElapsed;
        public string CurrentState { get; private set; }
        // Per-frame visual offsets are authored in the source sprite's local axis.  They must
        // follow the renderer's real flip state (not merely the gameplay facing direction),
        // because some replacement captures face left while the original character faces right.
        public int VisualMirrorSign
        {
            get
            {
                ApplyFacing();
                return spriteRenderer != null && spriteRenderer.flipX ? -1 : 1;
            }
        }

        private void Awake()
        {
            if (animator == null) animator = GetComponent<Animator>();
            if (spriteRenderer == null) spriteRenderer = transform.Find("VisualRoot")?.GetComponent<SpriteRenderer>();
            characterMotor=GetComponent<CharacterMotor>();actionRunner=GetComponent<ActionRunner>();
            CurrentState = idleState;dataDrivenAction=ResolveAction(idleState);dataDrivenElapsed=0f;
        }

        public void Play(string state, bool restart = false)
        {
            if (animator == null || string.IsNullOrEmpty(state) || (!restart && CurrentState == state)) return;
            forcedFrame=null;
            CurrentState = state;
            dataDrivenAction=ResolveAction(state);dataDrivenElapsed=0f;
            if(dataDrivenAction==null)characterMotor?.SetActionVisualOffset(Vector2.zero);
            animator.Play(state, 0, 0f);
            animator.speed = 1f;
        }

        public void Sample(string state, float normalizedTime)
        {
            if (animator == null || string.IsNullOrEmpty(state)) return;
            forcedFrame=null;
            dataDrivenAction=null;
            CurrentState = state;
            animator.Play(state, 0, Mathf.Clamp01(normalizedTime));
            animator.Update(0f);
        }

        public void SampleFrame(string state, Sprite sprite)
        {
            if(string.IsNullOrEmpty(state)||sprite==null)return;
            CurrentState=state;
            forcedFrame=sprite;
            if(spriteRenderer!=null)spriteRenderer.sprite=sprite;
            ApplyFacing();
        }
        public void ClearSampledFrame(){forcedFrame=null;dataDrivenAction=null;}

        public void Face(int direction)
        {
            if(spriteRenderer==null||direction==0)return;
            facingDirection=direction<0?-1:1;
            ApplyFacing();
        }
        private void LateUpdate()
        {
            if(forcedFrame==null&&dataDrivenAction!=null&&dataDrivenAction.frameCount>0)
            {
                dataDrivenElapsed+=Mathf.Max(0f,Time.deltaTime);int raw=Mathf.FloorToInt(dataDrivenElapsed*Mathf.Max(1f,dataDrivenAction.framesPerSecond));
                int frame=dataDrivenAction.loop?raw%dataDrivenAction.frameCount:dataDrivenAction.ClampFrame(raw);Sprite exact=dataDrivenAction.VisualFrameAt(frame);
                if(exact!=null&&spriteRenderer!=null)spriteRenderer.sprite=exact;ApplyFacing();Vector2 offset=dataDrivenAction.VisualOffsetAt(frame);int mirror=spriteRenderer!=null&&spriteRenderer.flipX?-1:1;
                characterMotor?.SetActionVisualOffset(new Vector2(offset.x*mirror,offset.y));
            }
            // Animator evaluates after regular Update and otherwise replaces the exact sprite
            // selected by ActionRunner with its own time-keyed frame.  Battle Editor previews
            // ActionDefinition frame indices, so enforce that same frame after Animator has run.
            if(forcedFrame!=null&&spriteRenderer!=null)spriteRenderer.sprite=forcedFrame;
            ApplyFacing();
        }
        private ActionDefinition ResolveAction(string state)
        {
            if(actionRunner==null)actionRunner=GetComponent<ActionRunner>();CharacterActionLibrary library=actionRunner==null?null:actionRunner.Library;if(library==null||library.actions==null)return null;
            foreach(ActionDefinition action in library.actions)if(action!=null&&(action.id==state||action.animatorState==state))return action;return null;
        }
        private void ApplyFacing()
        {
            if(spriteRenderer==null||spriteRenderer.sprite==null)return;
            string spriteName=spriteRenderer.sprite.name;
            // Most replacement soldier captures face left, while the supplied blood
            // HitReact capture faces right. Re-evaluate after every Animator frame change.
            bool replacementSoldier=spriteName.StartsWith("NSOL_",StringComparison.Ordinal);
            // Walk captures face left, but the supplied two-stage slash and blood-hit
            // captures face right in their source PNGs.
            bool sourceFacesRight=spriteName.StartsWith("NSOL_Attack_",StringComparison.Ordinal)||spriteName.StartsWith("NSOL_HitBlood_",StringComparison.Ordinal);
            bool sourceFacesLeft=replacementSoldier&&!sourceFacesRight;
            spriteRenderer.flipX=sourceFacesLeft?facingDirection>0:facingDirection<0;
        }
        public void ReturnToIdle() => Play(idleState, true);
        public void SetIdleState(string state) => idleState = state;
    }

    public sealed partial class DepthSorter : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer visual, shadow;
        [SerializeField] private int baseOrder = 1000;
        [SerializeField] private float orderPerDepth = 100f;

        private void Awake()
        {
            if (visual == null) visual = transform.Find("VisualRoot")?.GetComponent<SpriteRenderer>();
            if (shadow == null) shadow = transform.Find("Shadow")?.GetComponent<SpriteRenderer>();
            RefreshSort();
        }
        private void LateUpdate() => RefreshSort();
        public void RefreshSort()
        {
            int order = baseOrder - Mathf.RoundToInt(transform.position.y * orderPerDepth);
            if (shadow != null) shadow.sortingOrder = order - 1;
            if (visual != null) visual.sortingOrder = order;
        }
    }

    public sealed partial class ShadowVisual : MonoBehaviour
    {
        private void Awake()
        {
            var renderer = GetComponent<SpriteRenderer>() ?? gameObject.AddComponent<SpriteRenderer>();
            if (renderer.sprite != null) return;
            const int width = 64, height = 24;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, name = "RuntimeEllipseShadow" };
            var pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                float nx = (x - width * .5f) / (width * .5f), ny = (y - height * .5f) / (height * .5f);
                pixels[y * width + x] = new Color32(0, 0, 0, nx * nx + ny * ny <= 1f ? (byte)105 : (byte)0);
            }
            texture.SetPixels32(pixels); texture.Apply();
            renderer.sprite = Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(.5f, .5f), 64f);
        }
    }

    public sealed partial class CharacterHealth : MonoBehaviour
    {
        [SerializeField] private float maxHealth = 100f;
        [SerializeField] private SpriteRenderer visual;
        private CharacterMotor motor;
        private CharacterAnimator animationDriver;
        private CharacterIdentity identity;
        private float invulnerableUntil, parryUntil;
        public float Current { get; private set; }
        public float Maximum => maxHealth;
        public bool IsDead { get; private set; }
        public bool IsInvulnerable => Time.time < invulnerableUntil;
        public event Action<CharacterHealth> Died;
        public event Action ParrySucceeded;
        public static event Action<DamageAppliedEvent> DamageApplied;

        private void Awake()
        {
            Current = maxHealth;
            motor = GetComponent<CharacterMotor>();
            animationDriver = GetComponent<CharacterAnimator>();
            identity = GetComponent<CharacterIdentity>();
            if (visual == null) visual = transform.Find("VisualRoot")?.GetComponent<SpriteRenderer>();
        }

        public void ConfigureMaximum(float value, bool refill = true) { maxHealth = Mathf.Max(1f, value); if (refill) Current = maxHealth; }
        public void SetInvulnerable(float seconds) => invulnerableUntil = Mathf.Max(invulnerableUntil, Time.time + seconds);
        public void OpenParry(float seconds) => parryUntil = Time.time + seconds;

        public bool ReceiveDamage(DamagePacket packet)
        {
            if (IsDead || IsInvulnerable) return false;
            if (Time.time <= parryUntil) { parryUntil = 0f; ParrySucceeded?.Invoke(); return false; }
            ICharacterDamageResponder responder=GetComponent<ICharacterDamageResponder>();
            Current = Mathf.Max(0f, Current - packet.damage);
            if(responder==null||!responder.SuppressKnockback)motor?.ApplyGroundImpulse(packet.knockbackX, packet.knockbackDepth);
            // Keep the old flash only as a fallback for characters without an authored damage
            // responder.  Soldiers, Diaochan and Cao Cao already play real reaction animations;
            // toggling their renderer on top of that causes the visible hit-reaction flicker.
            if (visual != null && responder == null) StartCoroutine(PlaceholderHitFlash());
            DamageApplied?.Invoke(new DamageAppliedEvent(packet.source,this,packet.damage));
            responder?.OnDamageReceived(packet);
            if (Current <= 0f) Die();
            return true;
        }

        public void KillForTest() { Current = 0f; Die(); }

        private void Die()
        {
            if (IsDead) return;
            IsDead = true;
            Died?.Invoke(this);
            if (identity != null && identity.Team == CharacterTeam.Player) { GetComponent<ICharacterDamageResponder>()?.OnDeath();animationDriver?.Play("Death", true); enabled = false; }
            else
            {
                ICharacterDamageResponder responder=GetComponent<ICharacterDamageResponder>();
                if(responder!=null&&responder.HandlesDeath)responder.OnDeath();
                else StartCoroutine(PlaceholderEnemyDeath());
            }
        }

        private IEnumerator PlaceholderHitFlash()
        {
            for (int i = 0; i < 4; i++) { visual.enabled = !visual.enabled; yield return new WaitForSeconds(.045f); }
            visual.enabled = true;
        }

        private IEnumerator PlaceholderEnemyDeath()
        {
            yield return new WaitForSeconds(.25f);
            if (visual != null)
            {
                Color start = visual.color;
                for (float t = 0f; t < .45f; t += Time.deltaTime) { visual.color = new Color(start.r, start.g, start.b, 1f - t / .45f); yield return null; }
            }
            gameObject.SetActive(false);
        }
    }

    public sealed partial class PlayerInputController : MonoBehaviour
    {
        private static readonly HashSet<KeyCode> ProbedKeys = new HashSet<KeyCode>();
        private static readonly KeyCode[] RequiredProbeKeys = { KeyCode.A, KeyCode.D, KeyCode.W, KeyCode.S, KeyCode.J, KeyCode.K, KeyCode.LeftShift, KeyCode.LeftControl, KeyCode.Space, KeyCode.H, KeyCode.F, KeyCode.I, KeyCode.U, KeyCode.O, KeyCode.Q };
        private CharacterMotor motor;
        private CharacterAnimator animationDriver;
        private CharacterCombat combat;
        private float inputLockedUntil;
        private bool crouchHeld;
        private bool doubleTapRunProbed;
        private DoubleTapRunDetector runDetector;
        [Header("Double Tap Run")]
        [SerializeField] private float doubleTapWindow = .25f;
        [SerializeField] private float firstTapMaxDuration = .20f;
        [SerializeField] private float secondTapHoldThreshold = .05f;
        [SerializeField] private bool phaseBEnabled = true;
        public bool IsRunning => runDetector != null && runDetector.IsRunning;
        public float DoubleTapWindow => doubleTapWindow;
        private void Awake()
        {
            motor = GetComponent<CharacterMotor>(); animationDriver = GetComponent<CharacterAnimator>(); combat = GetComponent<CharacterCombat>();
            runDetector = new DoubleTapRunDetector(doubleTapWindow, firstTapMaxDuration, secondTapHoldThreshold);
        }
        public void LockFor(float seconds) => inputLockedUntil = Mathf.Max(inputLockedUntil, Time.time + seconds);
        public void EnablePhaseB() => phaseBEnabled = true;

        private void Update()
        {
            ProbeStandaloneInput();
            if (Time.time < inputLockedUntil) return;
            bool crouch = Input.GetKey(KeyCode.LeftControl);
            if (crouch != crouchHeld) { crouchHeld = crouch; combat.SetCrouch(crouch); }
            TrackRunKeyEvents();
            if (Input.GetKeyDown(KeyCode.Space) && motor.Jump()) animationDriver.Sample("Jump",0f);
            if (Input.GetKeyDown(KeyCode.J))
            {
                if (crouch) combat.RequestCrouchAttack();
                else if (motor.IsAirborne) combat.RequestJumpAttack();
                else combat.RequestComboAttack();
            }
            if (Input.GetKeyDown(KeyCode.K)) combat.RequestHeavyAttack();
            if (Input.GetKeyDown(KeyCode.LeftShift)) combat.RequestDodge();
            if (phaseBEnabled)
            {
                if (Input.GetKeyDown(KeyCode.H)) { combat.RequestSkill1(); ProbeSkillAction(KeyCode.H); }
                if (Input.GetKeyDown(KeyCode.F)) { combat.RequestParry(); ProbeSkillAction(KeyCode.F); }
                if (Input.GetKeyDown(KeyCode.I)) { combat.BeginCharge(); ProbeSkillAction(KeyCode.I); }
                if (Input.GetKeyUp(KeyCode.I)) { combat.ReleaseCharge(); ProbeSkillAction(KeyCode.I); }
                if (Input.GetKeyDown(KeyCode.U)) { combat.RequestSkill3(); ProbeSkillAction(KeyCode.U); }
                if (Input.GetKeyDown(KeyCode.O)) { combat.RequestSkill4(); ProbeSkillAction(KeyCode.O); }
                if (Input.GetKeyDown(KeyCode.Q)) combat.RequestDropItem();
            }

            Vector2 input = new Vector2((Input.GetKey(KeyCode.D) ? 1f : 0f) - (Input.GetKey(KeyCode.A) ? 1f : 0f), (Input.GetKey(KeyCode.W) ? 1f : 0f) - (Input.GetKey(KeyCode.S) ? 1f : 0f));
            if (input.sqrMagnitude > .01f && !combat.IsBusy && !crouch)
            {
                bool running = runDetector.Evaluate(input, Time.time);
                if(running&&!doubleTapRunProbed&&IsInputProbeEnabled()){doubleTapRunProbed=true;Debug.Log("[THREE_KINGDOMS_INPUT] DOUBLE_TAP_RUN direction="+runDetector.RunningDirection);}
                motor.Move(input, running);
                animationDriver.Face(motor.Facing);
                if(motor.IsAirborne)animationDriver.Sample("Jump",motor.JumpNormalizedTime);else animationDriver.Play(running ? "Run" : "Walk");
            }
            else
            {
                if (crouch || combat.IsBusy) runDetector.Reset();
                if (!combat.IsBusy && !crouch){if(motor.IsAirborne)animationDriver.Sample("Jump",motor.JumpNormalizedTime);else animationDriver.Play("CombatIdle");}
            }
        }

        private void TrackRunKeyEvents()
        {
            TrackKey(KeyCode.A, RunDirection.Left);
            TrackKey(KeyCode.D, RunDirection.Right);
            TrackKey(KeyCode.W, RunDirection.Back);
            TrackKey(KeyCode.S, RunDirection.Front);
        }

        private void TrackKey(KeyCode key, RunDirection direction)
        {
            if (Input.GetKeyDown(key)) runDetector.Press(direction, Time.time);
            if (Input.GetKeyUp(key)) runDetector.Release(direction, Time.time);
        }

        private static void ProbeStandaloneInput()
        {
            if (!IsInputProbeEnabled()) return;
            foreach (var key in RequiredProbeKeys)
            {
                if (!(Input.GetKeyDown(key) || ((key == KeyCode.A || key == KeyCode.D || key == KeyCode.W || key == KeyCode.S || key == KeyCode.LeftControl) && Input.GetKey(key)))) continue;
                if (ProbedKeys.Add(key)) Debug.Log("[THREE_KINGDOMS_INPUT] received=" + key);
            }
            if (ProbedKeys.Count == RequiredProbeKeys.Length)
            {
                Debug.Log("[THREE_KINGDOMS_INPUT] COMPLETE count=" + ProbedKeys.Count);
                Application.Quit(0);
            }
        }

        private void ProbeSkillAction(KeyCode key)
        {
            if (!IsInputProbeEnabled()) return;
            Debug.Log("[THREE_KINGDOMS_INPUT] skill=" + key + " action=" + combat.CurrentAction + " accepted=" + !string.IsNullOrEmpty(combat.CurrentAction));
        }

        private static bool IsInputProbeEnabled()
        {
            foreach(string argument in Environment.GetCommandLineArgs())if(argument=="-inputProbe")return true;return false;
        }
    }
}
