using System.Collections.Generic;
using UnityEngine;

namespace ThreeKingdoms
{
    [DisallowMultipleComponent]
    public sealed class ActionRunner : MonoBehaviour
    {
        [SerializeField] private CharacterActionLibrary library;
        private CharacterMotor motor;
        private CharacterAnimator animationDriver;
        private CharacterIdentity identity;
        private Hurtbox ownHurtbox;
        private Vector2 defaultHurtboxCenter, defaultHurtboxSize;
        private readonly HashSet<DamageReceiver> hitAction = new HashSet<DamageReceiver>();
        private readonly HashSet<DamageReceiver> hitPhase = new HashSet<DamageReceiver>();
        private readonly Dictionary<DamageReceiver, float> lastHitAt = new Dictionary<DamageReceiver, float>();
        private float elapsed, previousMoveX, previousMoveDepth;
        private float? runtimeDamageOverride;
        private Vector2 groundAnchor;
        private int previousFrame = -1;
        public ActionDefinition Current { get; private set; }
        public int CurrentFrame { get; private set; }
        public bool IsPlaying => Current != null;
        public int LastHitCount { get; private set; }
        public CharacterActionLibrary Library => library;
        public float EffectiveActionDamage => runtimeDamageOverride ?? (Current==null||Current.combat==null?0f:Current.combat.damage);

        private void Awake()
        {
            motor = GetComponent<CharacterMotor>();
            animationDriver = GetComponent<CharacterAnimator>();
            identity = GetComponent<CharacterIdentity>();
            ownHurtbox = GetComponentInChildren<Hurtbox>(true);
            ApplyLibraryDefaultHurtbox();
        }
        public void Configure(CharacterActionLibrary value) { library = value; ApplyLibraryDefaultHurtbox(); }
        private void ApplyLibraryDefaultHurtbox()
        {
            if(ownHurtbox==null)ownHurtbox=GetComponentInChildren<Hurtbox>(true);if(ownHurtbox==null||library==null)return;
            ActionDefinition idle=library.Find("CombatIdle")??library.Find("Idle");if(idle==null)return;defaultHurtboxCenter=idle.defaultHurtboxCenter;defaultHurtboxSize=idle.defaultHurtboxSize;ownHurtbox.ConfigureShape(defaultHurtboxCenter,defaultHurtboxSize);
        }
        public bool TryPlay(string actionId) => Play(library == null ? null : library.Find(actionId));
        public bool Play(ActionDefinition definition)
        {
            if (definition == null || definition.animation == null || definition.frameCount < 1) return false;
            Finish(); Current = definition; elapsed = 0f; CurrentFrame = 0; previousFrame = -1;
            groundAnchor = motor == null ? Vector2.zero : new Vector2(motor.X, motor.Depth);
            ownHurtbox?.ConfigureShape(definition.defaultHurtboxCenter, definition.defaultHurtboxSize);
            previousMoveX = previousMoveDepth = 0f; LastHitCount = 0; hitAction.Clear(); hitPhase.Clear(); lastHitAt.Clear();
            runtimeDamageOverride=null;
            string animationState=string.IsNullOrEmpty(definition.animatorState) ? definition.id : definition.animatorState;
            animationDriver?.Play(animationState, true);SampleCurrentVisualFrame(animationState,0f);ApplyMovement(0f);
            return true;
        }
        public void ManualTick(float deltaTime)
        {
            if (Current == null) return;
            elapsed += Mathf.Max(0f, deltaTime);
            if (Current.lockGroundPosition) motor?.SetPosition(groundAnchor.x, groundAnchor.y);
            int targetFrame = Current.ClampFrame(Mathf.FloorToInt(elapsed * Current.framesPerSecond));
            CurrentFrame=targetFrame;
            ApplyCurrentFrameHurtbox();
            float normalized = Current.frameCount <= 1 ? 0f : CurrentFrame / (float)(Current.frameCount - 1);
            // Scene rendering may run at 60/120 FPS, while Battle Editor advances discrete authored frames.
            // Use the exact saved Sprite index when available; normalized Animator sampling is only a legacy fallback.
            string animationState=string.IsNullOrEmpty(Current.animatorState)?Current.id:Current.animatorState;SampleCurrentVisualFrame(animationState,normalized);
            ApplyMovement(normalized);
            if(previousFrame<targetFrame)
            {
                int first=previousFrame<0?0:previousFrame+1;
                for(int crossed=first;crossed<=targetFrame;crossed++)
                {
                    CurrentFrame=crossed;
                    ApplyCurrentFrameHurtbox();
                    if(Current.combat.hitPolicy==ActionHitPolicy.OncePerFrame)hitPhase.Clear();
                    if(Current.combat.hitPolicy==ActionHitPolicy.OncePerPhase&&HasOffensiveShape(crossed)&&(crossed==0||!HasOffensiveShape(crossed-1)))hitPhase.Clear();
                    if(previousFrame<Current.startupEndFrame&&crossed>=Current.startupEndFrame)hitPhase.Clear();
                    previousFrame=crossed;LastHitCount+=TickHitShapes();
                }
            }
            else LastHitCount+=TickHitShapes();
            CurrentFrame=targetFrame;
            ApplyCurrentFrameHurtbox();
            if (elapsed >= Current.Duration)
            {
                if (Current.loop) { elapsed = 0f; previousFrame = -1; hitAction.Clear(); hitPhase.Clear(); }
                else Finish();
            }
        }
        private void ApplyCurrentFrameHurtbox()
        {
            if(ownHurtbox==null||Current==null)return;
            ActionFrameShape authored=Current.FindShape(CurrentFrame,ActionShapeRole.Hurtbox);
            if(authored!=null&&authored.enabled&&authored.shapeType==ActionShapeType.Box)ownHurtbox.ConfigureShape(authored.center,authored.size);
            else ownHurtbox.ConfigureShape(Current.defaultHurtboxCenter,Current.defaultHurtboxSize);
        }
        private void SampleCurrentVisualFrame(string animationState,float normalized)
        {
            Sprite exact=Current?.VisualFrameAt(CurrentFrame);
            if(exact!=null)animationDriver?.SampleFrame(animationState,exact);
            else animationDriver?.Sample(animationState,normalized);
        }
        private void ApplyMovement(float normalized)
        {
            float x = Current.movement.moveX.Evaluate(normalized), depth = Current.movement.moveDepth.Evaluate(normalized);
            if (!Current.lockGroundPosition) motor?.ApplyGroundImpulse((x - previousMoveX) * (motor?.Facing ?? 1), depth - previousMoveDepth);
            previousMoveX = x; previousMoveDepth = depth;
            Vector2 frameOffset=Current.VisualOffsetAt(CurrentFrame);
            int visualMirror=animationDriver?.VisualMirrorSign??1;
            motor?.SetActionVisualOffset(new Vector2(frameOffset.x*visualMirror,Current.movement.elevation.Evaluate(normalized)+frameOffset.y));
        }
        private int TickHitShapes()
        {
            if (identity == null) return 0;
            int hits = 0, facing = motor?.Facing ?? 1;
            foreach (ActionFrameShape shape in Current.ShapesAt(CurrentFrame))
            {
                if (shape.role != ActionShapeRole.DamageHitbox && shape.role != ActionShapeRole.EffectHitbox && shape.role != ActionShapeRole.WeaponHitbox) continue;
                foreach (Hurtbox hurtbox in Hurtbox.Active)
                {
                    if (hurtbox == null || hurtbox.Identity == null || hurtbox.Receiver == null || hurtbox.Identity == identity || hurtbox.Identity.Team == identity.Team) continue;
                    if (Mathf.Abs(hurtbox.Identity.transform.position.y - transform.position.y) > Current.combat.broadPhaseDepth) continue;
                    DamageReceiver receiver = hurtbox.Receiver;
                    if (!CanHit(receiver)) continue;
                    Vector3 world = hurtbox.Identity.transform.position;
                    Vector2 local = new Vector2(world.x - transform.position.x, 0f) + hurtbox.LocalCenter;
                    if (!ActionGeometry.IntersectsBox(shape, local, hurtbox.LocalSize, facing)) continue;
                    float damage=shape.EffectiveDamage(EffectiveActionDamage);
                    var packet = new DamagePacket(identity, damage, Current.combat.knockbackX * facing, Current.combat.knockbackDepth, Current.combat.hitStop,Current.combat.hitStun,Current.ResolveImpactWeight());
                    if (!receiver.Receive(packet)) continue;
                    RecordHit(receiver); hits++;
                }
            }
            return hits;
        }
        private bool HasOffensiveShape(int frame)
        {
            foreach(ActionFrameShape shape in Current.ShapesAt(frame))if(shape.role==ActionShapeRole.DamageHitbox||shape.role==ActionShapeRole.EffectHitbox||shape.role==ActionShapeRole.WeaponHitbox)return true;
            return false;
        }
        private bool CanHit(DamageReceiver receiver)
        {
            switch (Current.combat.hitPolicy)
            {
                case ActionHitPolicy.OncePerAction: return !hitAction.Contains(receiver);
                case ActionHitPolicy.OncePerPhase:
                case ActionHitPolicy.OncePerFrame: return !hitPhase.Contains(receiver);
                case ActionHitPolicy.RepeatInterval: return !lastHitAt.TryGetValue(receiver, out float time) || elapsed - time >= Current.combat.repeatInterval;
                default: return true;
            }
        }
        private void RecordHit(DamageReceiver receiver) { hitAction.Add(receiver); hitPhase.Add(receiver); lastHitAt[receiver] = elapsed; }
        public void SetRuntimeDamageOverride(float? value){runtimeDamageOverride=value.HasValue?Mathf.Max(0f,value.Value):value;}
        public bool ContainsPointForTest(ActionFrameShape shape, Vector2 localPoint, int facing) => ActionGeometry.Contains(shape, localPoint, facing);
        public void Finish()
        {
            if (Current != null) motor?.SetActionVisualOffset(Vector2.zero);
            animationDriver?.ClearSampledFrame();
            if(ownHurtbox!=null)ownHurtbox.ConfigureShape(defaultHurtboxCenter,defaultHurtboxSize);
            Current = null; elapsed = 0f; CurrentFrame = 0; previousFrame = -1;runtimeDamageOverride=null;
        }
    }
}
