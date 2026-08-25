using UnityEngine;

namespace ThreeKingdoms
{
    public enum ActionPriority { Locomotion=0, NormalCombo=10, HeavyAttack=20, Skill=30, Dodge=40, ParrySuccess=50, Hurt=60, Death=70 }

    [CreateAssetMenu(menuName="Three Kingdoms/Combat/Attack Data")]
    public sealed class AttackData : ScriptableObject
    {
        public string actionId;
        public string animationState;
        public ActionPriority priority=ActionPriority.Skill;
        public AttackImpactType impactType=AttackImpactType.Auto;
        [Min(0f)] public float startup=.2f, active=.2f, recovery=.3f, cooldown=2f;
        [Min(0f)] public float damage=20f, rangeX=2f, rangeDepth=.75f, knockbackX=.7f, knockbackDepth=.08f, hitStop=.04f, hitStun=.2f;
        [Tooltip("Explicit gameplay displacement. Zero means the action is stationary.")]
        public float forwardMove;
        [Tooltip("Keep CharacterRoot X/Depth fixed for the whole action. Player skills use this; explicit enemy movement does not.")]
        public bool lockGroundPosition=true;
        [Tooltip("VisualRoot-only grounding correction. It never changes gameplay X/Depth.")]
        public Vector2 visualOffset;
        public float Duration=>startup+active+recovery;
        public AttackDefinition CreateRuntime(float damageMultiplier=1f,float knockbackMultiplier=1f,float rangeMultiplier=1f)=>new AttackDefinition
        {
            action=actionId,damage=damage*damageMultiplier,rangeX=rangeX*rangeMultiplier,rangeDepth=rangeDepth,
            knockbackX=knockbackX*knockbackMultiplier,knockbackDepth=knockbackDepth,hitStop=hitStop,hitStun=hitStun,
            impactWeight=impactType==AttackImpactType.Heavy?HitWeight.Heavy:impactType==AttackImpactType.Light?HitWeight.Light:priority>=ActionPriority.HeavyAttack?HitWeight.Heavy:HitWeight.Light,
            startup=startup,active=active,recovery=recovery
        };
    }
}
