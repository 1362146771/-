using System.Collections;
using UnityEngine;

namespace ThreeKingdoms
{
    [DisallowMultipleComponent]
    public sealed class CaoCaoBossAI : MonoBehaviour, ICharacterDamageResponder
    {
        public enum BossState { Entrance, Idle, IdleAction, Approach, Guard, Attack, Stagger, Dead }

        [Header("Boss Combat")]
        [SerializeField] private AttackData thrust,downSlash,downSlashWave,charge,chargedCharge,skill,phase1Ultimate,phase2Ultimate,phase3Ultimate;
        [SerializeField] private float engagementRange=8.5f,alignDepth=.72f,preferredRange=2.35f;
        [SerializeField] private float entranceDuration=4.7f,guardDuration=.88f,phaseStaggerDuration=.60f,hitReactDuration=.72f;
        private CharacterMotor motor;
        private CharacterAnimator animationDriver;
        private CharacterCombat combat;
        private CharacterHealth health;
        private Transform target;
        private AttackData currentAttack;
        private float stateUntil,attackElapsed,nextDecisionAt;
        private int phase=1;
        private bool skillProtected,deathStarted;

        public BossState State { get; private set; }=BossState.Entrance;
        public int Phase=>phase;
        public bool IsSkillProtected=>skillProtected&&State==BossState.Attack;
        public bool SuppressKnockback=>IsSkillProtected||State==BossState.Entrance||State==BossState.Stagger||State==BossState.Dead;
        public bool HandlesDeath=>true;
        public string CurrentAttackId=>currentAttack==null?string.Empty:currentAttack.actionId;

        private void Awake()
        {
            motor=GetComponent<CharacterMotor>();animationDriver=GetComponent<CharacterAnimator>();combat=GetComponent<CharacterCombat>();health=GetComponent<CharacterHealth>();
        }

        private void Start()
        {
            if(target==null){var player=FindFirstObjectByType<PlayerInputController>();if(player!=null)target=player.transform;}
            if(target!=null){int facing=target.position.x>=transform.position.x?1:-1;motor.Face(facing);animationDriver.Face(facing);}
            stateUntil=Time.time+entranceDuration;health.SetInvulnerable(entranceDuration);animationDriver.Play("Entrance",true);State=BossState.Entrance;
        }

        private void Update()=>Tick(Time.deltaTime);
        public void SetTarget(Transform value)=>target=value;
        public AttackData AttackDataForTest(string action)
        {
            if(thrust!=null&&thrust.actionId==action)return thrust;if(downSlash!=null&&downSlash.actionId==action)return downSlash;if(downSlashWave!=null&&downSlashWave.actionId==action)return downSlashWave;
            if(charge!=null&&charge.actionId==action)return charge;if(chargedCharge!=null&&chargedCharge.actionId==action)return chargedCharge;if(skill!=null&&skill.actionId==action)return skill;
            if(phase1Ultimate!=null&&phase1Ultimate.actionId==action)return phase1Ultimate;if(phase2Ultimate!=null&&phase2Ultimate.actionId==action)return phase2Ultimate;if(phase3Ultimate!=null&&phase3Ultimate.actionId==action)return phase3Ultimate;return null;
        }
        public bool BeginAttackForTest(string action)
        {
            AttackData data=AttackDataForTest(action);if(data==null)return false;if(target==null){var player=FindFirstObjectByType<PlayerInputController>();if(player!=null)target=player.transform;}if(target!=null){int facing=target.position.x>=transform.position.x?1:-1;motor.Face(facing);animationDriver.Face(facing);}
            if(!combat.RequestEnemyAttack(data))return false;currentAttack=data;attackElapsed=0f;skillProtected=IsProtectedSkill(data);State=BossState.Attack;return true;
        }

        public void Tick(float deltaTime)
        {
            if(State==BossState.Dead||health.IsDead)return;
            if(target==null){var player=FindFirstObjectByType<PlayerInputController>();if(player!=null)target=player.transform;if(target==null)return;}
            if(State==BossState.Entrance||State==BossState.IdleAction||State==BossState.Guard||State==BossState.Stagger)
            {
                if(Time.time<stateUntil)return;State=BossState.Idle;animationDriver.Play("Idle",true);nextDecisionAt=Time.time+.15f;
            }
            if(State==BossState.Attack)
            {
                attackElapsed+=Mathf.Max(0f,deltaTime);
                ActionDefinition authored=currentAttack==null?null:combat.ActionLibrary?.Find(currentAttack.actionId);float duration=authored==null?currentAttack==null?0f:currentAttack.Duration:authored.Duration;
                if(currentAttack!=null&&attackElapsed<duration)return;
                currentAttack=null;skillProtected=false;State=BossState.Idle;animationDriver.Play("Idle",true);nextDecisionAt=Time.time+Random.Range(.22f,.48f);return;
            }
            if(Time.time<nextDecisionAt){animationDriver.Play("Idle");return;}

            float dx=target.position.x-transform.position.x,depth=target.position.y-transform.position.y;
            float absX=Mathf.Abs(dx),absDepth=Mathf.Abs(depth);
            int facing=dx>=0f?1:-1;motor.Face(facing);animationDriver.Face(facing);
            if(absX>engagementRange){State=BossState.Idle;animationDriver.Play("Idle");return;}
            if(absDepth>alignDepth){State=BossState.Approach;Move(new Vector2(Mathf.Sign(dx)*.12f,Mathf.Sign(depth)));return;}
            if(absX>preferredRange){State=BossState.Approach;Move(new Vector2(Mathf.Sign(dx),Mathf.Sign(depth)*.15f));return;}
            DecideAction(absX);
        }

        private void DecideAction(float distance)
        {
            float roll=Random.value;
            if(roll<.08f){BeginIdleAction();return;}
            if(roll<.16f){BeginGuard();return;}
            AttackData chosen;
            if(phase==1)
            {
                chosen=roll<.28f?thrust:roll<.47f?downSlash:roll<.64f?charge:roll<.84f?skill:phase1Ultimate;
            }
            else if(phase==2)
            {
                chosen=roll<.18f?thrust:roll<.34f?downSlashWave:roll<.52f?chargedCharge:roll<.72f?skill:roll<.87f?phase1Ultimate:phase2Ultimate;
            }
            else
            {
                chosen=roll<.14f?downSlashWave:roll<.30f?chargedCharge:roll<.50f?skill:roll<.68f?phase2Ultimate:phase3Ultimate;
            }
            if(chosen==null||!BeginAttackForTest(chosen.actionId)){nextDecisionAt=Time.time+.12f;return;}
        }

        private void BeginGuard()
        {
            combat.InterruptForHurt();State=BossState.Guard;stateUntil=Time.time+guardDuration;health.SetInvulnerable(guardDuration*.82f);animationDriver.Play("Guard",true);
        }

        private void BeginIdleAction()
        {
            combat.InterruptForHurt();State=BossState.IdleAction;stateUntil=Time.time+1.44f;animationDriver.Play("IdleAction",true);
        }

        private void Move(Vector2 direction){motor.Move(direction,false);animationDriver.Face(motor.Facing);animationDriver.Play("Walk");}
        private bool IsProtectedSkill(AttackData data)=>data==skill||data==phase1Ultimate||data==phase2Ultimate||data==phase3Ultimate;

        public void OnDamageReceived(DamagePacket packet)
        {
            if(State==BossState.Dead||health.Current<=0f||IsSkillProtected)return;
            float ratio=health.Current/health.Maximum;
            int newPhase=ratio<=.33f?3:ratio<=.66f?2:1;
            if(newPhase>phase)
            {
                phase=newPhase;combat.InterruptForHurt();currentAttack=null;skillProtected=false;State=BossState.Stagger;stateUntil=Time.time+phaseStaggerDuration;
                animationDriver.Play(phase==2?"Phase1Stagger":"Phase2Stagger",true);return;
            }
            if(State==BossState.Entrance||State==BossState.Stagger)return;
            combat.InterruptForHurt();currentAttack=null;skillProtected=false;State=BossState.Stagger;stateUntil=Time.time+hitReactDuration;
            animationDriver.Play(Random.value<.5f?"HitReactA":"HitReactB",true);
        }

        public void OnDeath()
        {
            if(deathStarted)return;deathStarted=true;combat.InterruptForHurt();currentAttack=null;skillProtected=false;State=BossState.Dead;animationDriver.Play("Death",true);StartCoroutine(DeathExit());
        }

        private IEnumerator DeathExit()
        {
            yield return new WaitForSeconds(3.6f);gameObject.SetActive(false);
        }
    }
}
