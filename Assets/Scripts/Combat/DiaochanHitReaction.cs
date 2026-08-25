using System.Collections;
using UnityEngine;

namespace ThreeKingdoms
{
    [DisallowMultipleComponent]
    public sealed class DiaochanHitReaction : MonoBehaviour, ICharacterDamageResponder
    {
        [SerializeField, Min(.05f)] private float reactionDuration=.54f;
        [SerializeField] private string reactionState="HitReact";
        private CharacterHealth health;
        private CharacterCombat combat;
        private CharacterAnimator animationDriver;
        private CharacterMotor motor;
        private PlayerInputController playerInput;
        private Coroutine reactionRoutine;
        private bool restoreInputAfterReaction;

        public bool SuppressKnockback=>true;
        public bool HandlesDeath=>false;
        public bool IsReacting=>reactionRoutine!=null;

        private void Awake()
        {
            health=GetComponent<CharacterHealth>();combat=GetComponent<CharacterCombat>();animationDriver=GetComponent<CharacterAnimator>();motor=GetComponent<CharacterMotor>();playerInput=GetComponent<PlayerInputController>();
        }

        public void OnDamageReceived(DamagePacket packet)
        {
            // CharacterHealth invokes this after HP is reduced but before its death callback. A lethal hit
            // must go directly to Death and must never be overwritten by the non-lethal reaction animation.
            if(health==null||health.Current<=0f)return;
            if(reactionRoutine==null)restoreInputAfterReaction=playerInput!=null&&playerInput.enabled;
            else StopCoroutine(reactionRoutine);
            reactionRoutine=StartCoroutine(PlayReaction(packet));
        }

        private IEnumerator PlayReaction(DamagePacket packet)
        {
            combat?.InterruptForHurt();if(playerInput!=null)playerInput.enabled=false;ResolvedHitReaction resolved=HitReactionRuntime.Resolve(gameObject,packet,reactionDuration,reactionState,true);animationDriver?.Play(resolved.animationActionId,true);
            float elapsed=0f,progress=0f;while(elapsed<resolved.stunDuration){elapsed+=Time.deltaTime;progress=HitReactionRuntime.ApplyKnockbackStep(motor,resolved,progress,elapsed);yield return null;}
            reactionRoutine=null;
            if(health!=null&&health.IsDead)yield break;
            if(playerInput!=null&&restoreInputAfterReaction)playerInput.enabled=true;restoreInputAfterReaction=false;
            animationDriver?.ReturnToIdle();
        }

        public void OnDeath()
        {
            if(reactionRoutine!=null){StopCoroutine(reactionRoutine);reactionRoutine=null;}
            restoreInputAfterReaction=false;if(playerInput!=null)playerInput.enabled=false;
        }
    }
}
