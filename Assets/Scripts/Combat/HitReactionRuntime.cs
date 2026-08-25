using UnityEngine;

namespace ThreeKingdoms
{
    public readonly struct ResolvedHitReaction
    {
        public readonly string animationActionId;
        public readonly float stunDuration;
        public readonly float totalRetreat;

        public ResolvedHitReaction(string actionId,float duration,float retreat)
        {
            animationActionId=string.IsNullOrWhiteSpace(actionId)?"HitReact":actionId;
            stunDuration=Mathf.Max(.01f,duration);
            totalRetreat=retreat;
        }
        public float Progress(float normalized)=>Mathf.Clamp01(normalized);
    }

    public static class HitReactionRuntime
    {
        public static ResolvedHitReaction Resolve(GameObject target,DamagePacket packet,float fallbackDuration,string fallbackAction,bool fallbackUsesAttackKnockback)
        {
            ActionRunner runner=target==null?null:target.GetComponent<ActionRunner>();
            ActionDefinition reaction=runner?.Library?.Find("HitReact");
            HitReactionProfile profile=reaction?.reaction?.Profile(packet.hitWeight);
            if(profile==null)return new ResolvedHitReaction(fallbackAction,fallbackDuration,fallbackUsesAttackKnockback?packet.knockbackX:0f);
            float direction=Mathf.Abs(packet.knockbackX)<.0001f?0f:Mathf.Sign(packet.knockbackX);
            return new ResolvedHitReaction(profile.animationActionId,profile.stunDuration,Mathf.Max(0f,profile.retreatDistance)*direction);
        }

        public static float ApplyKnockbackStep(CharacterMotor motor,ResolvedHitReaction reaction,float previousProgress,float elapsed)
        {
            float next=reaction.Progress(elapsed/reaction.stunDuration),delta=next-previousProgress;
            if(motor!=null&&Mathf.Abs(delta)>.000001f)motor.ApplyGroundImpulse(reaction.totalRetreat*delta,0f);
            return next;
        }
    }
}
