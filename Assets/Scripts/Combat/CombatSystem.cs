using System;
using System.Collections.Generic;
using UnityEngine;

namespace ThreeKingdoms
{
    [Serializable]
    public sealed class AttackDefinition
    {
        public string action;
        public float damage = 10f, startup = .12f, active = .12f, recovery = .2f;
        public float rangeX = 1.4f, rangeDepth = .55f, forwardMove = .15f;
        public float knockbackX = .45f, knockbackDepth = .1f, hitStop = .035f, hitStun = .2f;
        public HitWeight impactWeight = HitWeight.Light;
        public float comboWindowStart, comboWindowEnd;
    }

    [Serializable]
    public struct ComboSegment
    {
        public float startTime, hitStart, hitEnd, comboWindowStart, comboWindowEnd, segmentEnd, damage, forwardMove, knockback;
        public ComboSegment(float start, float hs, float he, float ws, float we, float end, float damage, float move, float knockback)
        { startTime = start; hitStart = hs; hitEnd = he; comboWindowStart = ws; comboWindowEnd = we; segmentEnd = end; this.damage = damage; forwardMove = move; this.knockback = knockback; }
    }

    public readonly struct DamagePacket
    {
        public readonly CharacterIdentity source;
        public readonly float damage, knockbackX, knockbackDepth, hitStop, hitStun;
        public readonly HitWeight hitWeight;
        public DamagePacket(CharacterIdentity source, float damage, float x, float depth, float stop,float stun=.2f,HitWeight weight=HitWeight.Light)
        { this.source = source; this.damage = damage; knockbackX = x; knockbackDepth = depth; hitStop = stop;hitStun=stun;hitWeight=weight; }
    }

    public readonly struct DamageAppliedEvent
    {
        public readonly CharacterIdentity source;
        public readonly CharacterHealth target;
        public readonly float damage;
        public DamageAppliedEvent(CharacterIdentity source,CharacterHealth target,float damage){this.source=source;this.target=target;this.damage=damage;}
    }

    public sealed class ComboSequence
    {
        private readonly ComboSegment[] segments;
        private int bufferedInputs, targetSegments, currentSegment, maxSegmentsReached;
        private bool consumedWindow;
        private float elapsed;
        public bool Active { get; private set; }
        public float Elapsed => elapsed;
        public int CurrentSegment => Active ? currentSegment : -1;
        public int TargetSegments => targetSegments;
        public int MaxSegmentsReached => maxSegmentsReached;
        public ComboSegment Segment => segments[currentSegment];

        public ComboSequence(ComboSegment[] segments)
        {
            this.segments = segments ?? throw new ArgumentNullException(nameof(segments));
            if (segments.Length != 4) throw new ArgumentException("Exactly four combo segments are required.");
        }

        public void Press()
        {
            if (!Active)
            {
                Active = true; elapsed = 0f; currentSegment = 0; targetSegments = 1; bufferedInputs = 0; maxSegmentsReached = 1; consumedWindow = false;
                return;
            }
            ComboSegment segment=segments[currentSegment];
            if(elapsed<=segment.comboWindowEnd)bufferedInputs = Math.Min(segments.Length - targetSegments, bufferedInputs + 1);
        }

        public bool Tick(float deltaTime, out bool advanced)
        {
            advanced = false;
            if (!Active) return false;
            elapsed += Math.Max(0f, deltaTime);
            var segment = segments[currentSegment];
            if (!consumedWindow && elapsed >= segment.comboWindowStart && elapsed <= segment.comboWindowEnd && bufferedInputs > 0)
            {
                consumedWindow = true;
                if (currentSegment + 1 < segments.Length)
                {
                    bufferedInputs--;
                    targetSegments = Math.Max(targetSegments, currentSegment + 2);
                    maxSegmentsReached = Math.Max(maxSegmentsReached, targetSegments);
                }
            }
            else if(!consumedWindow&&elapsed>segment.comboWindowEnd){consumedWindow=true;bufferedInputs=0;}
            if (elapsed < segment.segmentEnd) return false;
            if (targetSegments > currentSegment + 1 && currentSegment + 1 < segments.Length)
            {
                currentSegment++; consumedWindow = false; advanced = true; return false;
            }
            Active = false;
            return true;
        }

        public void Cancel() { Active = false; bufferedInputs = targetSegments = currentSegment = 0; elapsed = 0f; }
    }

    public sealed partial class Hurtbox : MonoBehaviour
    {
        private static readonly HashSet<Hurtbox> ActiveSet = new HashSet<Hurtbox>();
        public static IEnumerable<Hurtbox> Active => ActiveSet;
        public CharacterIdentity Identity { get; private set; }
        public DamageReceiver Receiver { get; private set; }
        private void Awake() { Identity = GetComponentInParent<CharacterIdentity>(); Receiver = GetComponent<DamageReceiver>() ?? GetComponentInParent<DamageReceiver>(); }
        private void OnEnable() => ActiveSet.Add(this);
        private void OnDisable() => ActiveSet.Remove(this);
    }

    public sealed partial class DamageReceiver : MonoBehaviour
    {
        [SerializeField] private CharacterHealth health;
        private void Awake() { if (health == null) health = GetComponentInParent<CharacterHealth>(); }
        public bool Receive(DamagePacket packet) => health != null && health.ReceiveDamage(packet);
    }

    public sealed partial class Hitbox : MonoBehaviour
    {
        private readonly HashSet<DamageReceiver> hitThisActivation = new HashSet<DamageReceiver>();
        private CharacterIdentity owner;
        private AttackDefinition definition;
        private bool active;
        public bool ActiveNow => active;
        private void Awake() => owner = GetComponentInParent<CharacterIdentity>();
        public void Begin(AttackDefinition attack) { definition = attack; active = true; hitThisActivation.Clear(); }
        public void End() { active = false; hitThisActivation.Clear(); }

        public int TickHits()
        {
            if (!active || owner == null || definition == null) return 0;
            int hits = 0;
            float facing = GetComponentInParent<CharacterMotor>()?.Facing ?? 1;
            Vector3 origin = owner.transform.position;
            foreach (var hurtbox in Hurtbox.Active)
            {
                if (hurtbox == null || hurtbox.Identity == null || hurtbox.Receiver == null) continue;
                if (hurtbox.Identity == owner || hurtbox.Identity.Team == owner.Team || hitThisActivation.Contains(hurtbox.Receiver)) continue;
                Vector3 target = hurtbox.Identity.transform.position;
                float forward = (target.x - origin.x) * facing;
                float depth = Mathf.Abs(target.y - origin.y);
                if (forward < -.2f || forward > definition.rangeX || depth > definition.rangeDepth) continue;
                if (hurtbox.Receiver.Receive(new DamagePacket(owner, definition.damage, definition.knockbackX * facing, definition.knockbackDepth, definition.hitStop,definition.hitStun,definition.impactWeight)))
                { hitThisActivation.Add(hurtbox.Receiver); hits++; }
            }
            return hits;
        }
    }

    public sealed partial class CharacterCombat : MonoBehaviour
    {
        public static readonly ComboSegment[] DiaochanCombo =
        {
            new ComboSegment(0f,.14f,.28f,.28f,.35f,.35f,10f,.10f,.28f),
            new ComboSegment(.35f,.42f,.56f,.56f,.63f,.63f,12f,.12f,.34f),
            new ComboSegment(.63f,.70f,.84f,.84f,.98f,.98f,14f,.16f,.42f),
            new ComboSegment(.98f,1.05f,1.26f,1.18f,1.40f,1.68f,22f,.24f,.75f)
        };

        private CharacterMotor motor;
        private CharacterAnimator animationDriver;
        private CharacterHealth health;
        private CharacterIdentity identity;
        private Hitbox hitbox;
        private ComboSequence combo;
        private AttackDefinition currentAttack;
        private AttackData currentAttackData;
        [Header("Iteration 02 Data-Driven Skills")]
        [SerializeField] private AttackData skill1Data, chargeSkill2Data, skill3Data, skill4Data, parryCounterData;
        [SerializeField] private ParryData parryData;
        private readonly Dictionary<string,float> cooldownUntil=new Dictionary<string,float>();
        private float actionTime, actionHitStart, actionHitEnd, actionEnd, chargeStarted;
        private Vector2 actionGroundAnchor;
        private bool lockActionGround;
        private bool actionHitboxOn, crouching, charging, parryWindowOpened, comboUsesAuthoredHitboxes;
        private ActionPriority currentPriority=ActionPriority.Locomotion;
        public bool IsBusy => combo.Active || currentAttack != null || charging || (unifiedRunner != null && unifiedRunner.IsPlaying);
        public bool IsComboActive => combo.Active;
        public int ComboTargetSegments => combo.TargetSegments;
        public int ComboMaxSegmentsReached => combo.MaxSegmentsReached;
        public string CurrentAction => charging?"ChargeSkill2_Charging":combo.Active?"AttackCombo4":unifiedRunner!=null&&unifiedRunner.IsPlaying?unifiedRunner.Current.id:currentAttack?.action??"";
        public ActionPriority CurrentPriority=>currentPriority;
        public int LastChargeLevel { get; private set; }=-1;
        public float LastAttackDamage=>currentAttack?.damage??0f;
        public float LastAttackKnockback=>currentAttack?.knockbackX??0f;
        public bool LastParrySucceeded { get; private set; }
        public event Action<int> ComboSegmentStarted;
        public event Action DropItemRequested;

        private void Awake()
        {
            motor = GetComponent<CharacterMotor>(); animationDriver = GetComponent<CharacterAnimator>(); health = GetComponent<CharacterHealth>();
            identity = GetComponent<CharacterIdentity>(); hitbox = GetComponentInChildren<Hitbox>(true); combo = new ComboSequence(ResolveRuntimeComboSegments());
            if (health != null) health.ParrySucceeded += OnParrySucceeded;
        }
        private void OnDestroy() { if (health != null) health.ParrySucceeded -= OnParrySucceeded; }
        private void Update() => ManualTick(Time.deltaTime);
        public void ManualTick(float deltaTime)
        {
            if(charging&&lockActionGround)motor.SetPosition(actionGroundAnchor.x,actionGroundAnchor.y);
            if(combo.Active)
            {
                if(comboUsesAuthoredHitboxes&&IsUnifiedCombo)
                {
                    unifiedRunner.SetRuntimeDamageOverride(ResolveComboDamage(combo.CurrentSegment,combo.Segment.damage));
                    unifiedRunner.ManualTick(deltaTime);
                }
                TickCombo(deltaTime,comboUsesAuthoredHitboxes);
            }
            else if(unifiedRunner!=null&&unifiedRunner.IsPlaying)unifiedRunner.ManualTick(deltaTime);
            else if(currentAttack!=null)TickAction(deltaTime);
        }
        public float CooldownRemaining(string actionId)=>cooldownUntil.TryGetValue(actionId,out float until)?Mathf.Max(0f,until-Time.time):0f;
        public bool IsOnCooldown(string actionId)=>CooldownRemaining(actionId)>0f;

        public void RequestComboAttack()
        {
            if (identity.Team != CharacterTeam.Player || motor.IsAirborne || crouching || currentAttack != null || (!combo.Active&&unifiedRunner!=null&&unifiedRunner.IsPlaying)) return;
            bool starting = !combo.Active; combo.Press();
            if (starting)
            {
                currentPriority=ActionPriority.NormalCombo;
                comboUsesAuthoredHitboxes=TryPlayAuthoredAction("AttackCombo4",ResolveComboDamage(0,DiaochanCombo[0].damage));
                if(!comboUsesAuthoredHitboxes)animationDriver.Play("AttackCombo4",true);
                motor.ApplyGroundImpulse(DiaochanCombo[0].forwardMove*motor.Facing,0f);ComboSegmentStarted?.Invoke(1);
            }
        }

        public void RequestHeavyAttack() { if (!IsBusy && !motor.IsAirborne&&!TryPlayAuthoredAction("HeavyAttack")) StartAction("HeavyAttack",.88f,.28f,.44f,28f,2.5f,.78f,.92f,ActionPriority.HeavyAttack); }
        public void RequestCrouchAttack() { if (!IsBusy && crouching && !motor.IsAirborne&&!TryPlayAuthoredAction("CrouchAttack")) StartAction("CrouchAttack",.62f,.18f,.34f,14f,1.45f,.50f,.48f); }
        public void RequestJumpAttack() { if (!IsBusy && motor.IsAirborne&&!TryPlayAuthoredAction("JumpAttack")) StartAction("JumpAttack",.70f,.20f,.48f,18f,1.55f,.60f,.60f); }
        public void RequestDodge()
        {
            if (IsBusy || motor.IsAirborne) return;
            StartAction("Dodge",.48f,2f,1f,0f,0f,0f,0f,ActionPriority.Dodge); health.SetInvulnerable(.34f); motor.ApplyGroundImpulse(motor.Facing * 1.35f, 0f);
        }
        public void RequestEnemyAttack(string action,float damage,float rangeX) { if (!IsBusy) StartAction(action,action=="RunAttack"?.90f:.72f,.24f,.42f,damage,rangeX,.58f,.40f); }
        public bool RequestEnemyAttack(AttackData data)=>data!=null&&TryStartUnified(data.actionId)||StartDataAttack(data);
        public void RequestSkill1(){if(!TryStartUnified("Skill1"))StartDataAttack(skill1Data);}
        public void RequestSkill3(){if(!TryStartUnified("Skill3_A"))StartDataAttack(skill3Data);}
        public void RequestSkill4(){if(!TryStartUnified("Skill4"))StartDataAttack(skill4Data);}
        public void RequestParry()
        {
            if(parryData==null||!CanStart(ActionPriority.Skill)||IsOnCooldown("Parry"))return;
            CancelLowerPriority();LastParrySucceeded=false;parryWindowOpened=false;currentPriority=ActionPriority.Skill;
            actionGroundAnchor=new Vector2(motor.X,motor.Depth);lockActionGround=true;motor.SetActionVisualOffset(Vector2.zero);
            currentAttack=new AttackDefinition{action="Parry",damage=0f};actionTime=0f;actionHitStart=2f;actionHitEnd=1f;actionEnd=parryData.Duration;actionHitboxOn=false;
            cooldownUntil["Parry"]=Time.time+parryData.cooldown;animationDriver.Play("CombatIdle");
        }
        public void BeginCharge()
        {
            if(chargeSkill2Data==null||!CanStart(ActionPriority.Skill)||IsOnCooldown(chargeSkill2Data.actionId))return;
            CancelLowerPriority();charging=true;chargeStarted=Time.time;LastChargeLevel=-1;currentPriority=ActionPriority.Skill;
            actionGroundAnchor=new Vector2(motor.X,motor.Depth);lockActionGround=chargeSkill2Data.lockGroundPosition;motor.SetActionVisualOffset(chargeSkill2Data.visualOffset);animationDriver.Play(chargeSkill2Data.animationState,true);
        }
        public void ReleaseCharge()
        {
            if(!charging)return;ReleaseChargeInternal(Mathf.Clamp(Time.time-chargeStarted,0f,1.4f));
        }
        public void ReleaseChargeForTest(float heldSeconds){if(charging)ReleaseChargeInternal(Mathf.Clamp(heldSeconds,0f,1.4f));}
        public void RequestDropItem() { if (!IsBusy) { animationDriver.Play("DropItem",true); DropItemRequested?.Invoke(); } }
        public void SetCrouch(bool value) { crouching = value; if (!IsBusy && !motor.IsAirborne) animationDriver.Play(value?"Crouch":"CombatIdle"); }
        public void InterruptForHurt()
        {
            CancelLowerPriority();charging=false;chargeStarted=0f;parryWindowOpened=false;currentPriority=ActionPriority.Hurt;
        }

        private void TickCombo(float deltaTime,bool authoredHitboxes)
        {
            var segment = combo.Segment;
            if(!authoredHitboxes)
            {
                if (!actionHitboxOn && combo.Elapsed >= segment.hitStart && combo.Elapsed < segment.hitEnd) { hitbox.Begin(ForCombo(segment)); actionHitboxOn = true; }
                if (actionHitboxOn)
                {
                    hitbox.TickHits();
                    if (combo.Elapsed >= segment.hitEnd) { hitbox.End(); actionHitboxOn = false; }
                }
            }
            bool ended = combo.Tick(deltaTime,out bool advanced);
            if (advanced) { var next=combo.Segment;if(authoredHitboxes&&IsUnifiedCombo)unifiedRunner.SetRuntimeDamageOverride(ResolveComboDamage(combo.CurrentSegment,next.damage));motor.ApplyGroundImpulse(next.forwardMove*motor.Facing,0f); ComboSegmentStarted?.Invoke(combo.CurrentSegment+1); }
            if (ended) { if(authoredHitboxes)unifiedRunner?.Finish();hitbox.End();actionHitboxOn=false;comboUsesAuthoredHitboxes=false;currentPriority=ActionPriority.Locomotion;animationDriver.ReturnToIdle(); }
        }

        private AttackDefinition ForCombo(ComboSegment s) => new AttackDefinition { action="AttackCombo4",damage=ResolveComboDamage(combo.CurrentSegment,s.damage),rangeX=2.35f,rangeDepth=.78f,knockbackX=s.knockback,knockbackDepth=.08f,hitStop=.035f };

        private void StartAction(string action,float duration,float hitStart,float hitEnd,float damage,float rangeX,float rangeDepth,float knockback,ActionPriority priority=ActionPriority.NormalCombo)
        {
            currentAttackData=null;lockActionGround=false;motor.SetActionVisualOffset(Vector2.zero);
            currentAttack = new AttackDefinition { action=action,damage=ResolveActionDamage(action,damage),rangeX=rangeX,rangeDepth=rangeDepth,knockbackX=knockback,knockbackDepth=.08f,hitStop=.04f };
            currentPriority=priority;actionTime=0f; actionHitStart=hitStart; actionHitEnd=hitEnd; actionEnd=duration; actionHitboxOn=false; animationDriver.Play(action,true);
        }

        private bool StartDataAttack(AttackData data,float damageMultiplier=1f,float knockbackMultiplier=1f,float rangeMultiplier=1f,bool bypassPriority=false)
        {
            if(data==null||IsOnCooldown(data.actionId)||(!bypassPriority&&!CanStart(data.priority)))return false;
            CancelLowerPriority();currentAttackData=data;currentAttack=data.CreateRuntime(damageMultiplier,knockbackMultiplier,rangeMultiplier);ActionDefinition configured=FindConfiguredAction(data.actionId);if(configured!=null&&configured.combat!=null)currentAttack.damage=Mathf.Max(0f,configured.combat.damage)*damageMultiplier;currentPriority=data.priority;
            actionGroundAnchor=new Vector2(motor.X,motor.Depth);lockActionGround=data.lockGroundPosition;motor.SetActionVisualOffset(data.visualOffset);
            actionTime=0f;actionHitStart=data.startup;actionHitEnd=data.startup+data.active;actionEnd=data.Duration;actionHitboxOn=false;
            if(data.cooldown>0f)cooldownUntil[data.actionId]=Time.time+data.cooldown;
            if(Mathf.Abs(data.forwardMove)>.001f)motor.ApplyGroundImpulse(data.forwardMove*motor.Facing,0f);
            animationDriver.Play(data.animationState,true);return true;
        }

        private bool CanStart(ActionPriority requested)
        {
            if(charging)return false;
            if(!combo.Active&&currentAttack==null)return true;
            bool inComboRecovery=combo.Active&&combo.Elapsed>=combo.Segment.hitEnd;
            bool inActionRecovery=currentAttack!=null&&actionTime>=actionHitEnd;
            return requested>currentPriority&&(inComboRecovery||inActionRecovery);
        }

        private void CancelLowerPriority()
        {
            if(combo.Active)combo.Cancel();comboUsesAuthoredHitboxes=false;if(unifiedRunner!=null&&unifiedRunner.IsPlaying)unifiedRunner.Finish();hitbox.End();actionHitboxOn=false;currentAttack=null;currentAttackData=null;lockActionGround=false;motor.SetActionVisualOffset(Vector2.zero);
        }

        private void ReleaseChargeInternal(float heldSeconds)
        {
            charging=false;chargeStarted=0f;
            LastChargeLevel=heldSeconds<.35f?0:heldSeconds<.90f?1:2;
            float[] damage={1f,1.45f,2f},knockback={1f,1.35f,1.8f},range={1f,1.08f,1.18f};
            ActionDefinition configured=FindConfiguredAction(chargeSkill2Data.actionId);float baseDamage=configured==null||configured.combat==null?chargeSkill2Data.damage:configured.combat.damage;
            if(TryPlayAuthoredAction(chargeSkill2Data.actionId,baseDamage*damage[LastChargeLevel])){lockActionGround=false;motor.SetActionVisualOffset(Vector2.zero);return;}
            StartDataAttack(chargeSkill2Data,damage[LastChargeLevel],knockback[LastChargeLevel],range[LastChargeLevel],true);
        }

        private void TickAction(float deltaTime)
        {
            actionTime += deltaTime;
            if(lockActionGround)motor.SetPosition(actionGroundAnchor.x,actionGroundAnchor.y);
            if(currentAttack.action=="Parry"&&parryData!=null&&!parryWindowOpened&&actionTime>=parryData.parryWindowStart)
            {
                parryWindowOpened=true;health.OpenParry(parryData.parryWindowEnd-parryData.parryWindowStart);
            }
            if (!actionHitboxOn && actionTime >= actionHitStart && actionTime < actionHitEnd) { hitbox.Begin(currentAttack); actionHitboxOn=true; }
            if (actionHitboxOn) { hitbox.TickHits(); if (actionTime >= actionHitEnd) { hitbox.End(); actionHitboxOn=false; } }
            if (actionTime < actionEnd) return;
            hitbox.End();currentAttack=null;currentAttackData=null;lockActionGround=false;motor.SetActionVisualOffset(Vector2.zero);currentPriority=ActionPriority.Locomotion;animationDriver.ReturnToIdle();
        }

        private void OnParrySucceeded()
        {
            if(currentAttack==null||currentAttack.action!="Parry")return;
            LastParrySucceeded=true;currentAttack=null;hitbox.End();actionHitboxOn=false;
            AttackData counter=parryCounterData!=null?parryCounterData:parryData?.counterAttack;
            if(counter==null||!TryPlayAuthoredAction(counter.actionId))StartDataAttack(counter,1f,1f,1f,true);
        }
    }
}
