using System.Collections;
using UnityEngine;

namespace ThreeKingdoms
{
    [DisallowMultipleComponent]
    public sealed class SoldierReactionController : MonoBehaviour, ICharacterDamageResponder
    {
        [SerializeField, Min(.05f)] private float hurtDuration=.34f;
        [SerializeField, Min(.05f)] private float deathDuration=.98f;
        private CharacterHealth health;
        private CharacterCombat combat;
        private CharacterAnimator animationDriver;
        private CharacterMotor motor;
        private SoldierAI ai;
        private Coroutine reaction;

        // This source animation already contains its own recoil. Applying DamagePacket knockback as well
        // produces a second, visible root displacement, so the replacement soldier consumes it here.
        public bool SuppressKnockback=>true;
        public bool HandlesDeath=>true;

        private void Awake()
        {
            health=GetComponent<CharacterHealth>();
            combat=GetComponent<CharacterCombat>();
            animationDriver=GetComponent<CharacterAnimator>();motor=GetComponent<CharacterMotor>();
            ai=GetComponent<SoldierAI>();
        }

        public void OnDamageReceived(DamagePacket packet)
        {
            if(health==null||health.Current<=0f)return;
            if(reaction!=null)StopCoroutine(reaction);
            reaction=StartCoroutine(PlayHurt(packet));
        }

        private IEnumerator PlayHurt(DamagePacket packet)
        {
            bool restoreAI=ai!=null&&ai.enabled;
            combat?.InterruptForHurt();
            if(ai!=null)ai.enabled=false;
            ResolvedHitReaction resolved=HitReactionRuntime.Resolve(gameObject,packet,hurtDuration,"HitReact",false);
            animationDriver?.Play(resolved.animationActionId,true);
            float elapsed=0f,progress=0f;while(elapsed<resolved.stunDuration){elapsed+=Time.deltaTime;progress=HitReactionRuntime.ApplyKnockbackStep(motor,resolved,progress,elapsed);yield return null;}
            reaction=null;
            if(health==null||health.IsDead)yield break;
            if(ai!=null&&restoreAI)ai.enabled=true;
            animationDriver?.ReturnToIdle();
        }

        public void OnDeath()
        {
            if(reaction!=null){StopCoroutine(reaction);reaction=null;}
            if(ai!=null)ai.enabled=false;
            if(combat!=null)combat.enabled=false;
            StartCoroutine(PlayDeath());
        }

        private IEnumerator PlayDeath()
        {
            animationDriver?.Play("Death",true);
            yield return new WaitForSeconds(deathDuration);
            gameObject.SetActive(false);
        }
    }
}
