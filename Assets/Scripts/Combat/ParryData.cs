using UnityEngine;

namespace ThreeKingdoms
{
    [CreateAssetMenu(menuName="Three Kingdoms/Combat/Parry Data")]
    public sealed class ParryData : ScriptableObject
    {
        [Min(0f)] public float parryStartup=.08f, parryWindowStart=.08f, parryWindowEnd=.30f, recovery=.28f, cooldown=1.5f;
        public AttackData counterAttack;
        public float Duration=>parryWindowEnd+recovery;
    }
}
