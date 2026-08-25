using UnityEngine;

namespace ThreeKingdoms
{
    public sealed partial class CharacterCombat
    {
        [Header("Battle Editor V0.1 Compatibility")]
        [SerializeField] private CharacterActionLibrary actionLibrary;
        private ActionRunner unifiedRunner;
        public ActionRunner UnifiedRunner
        {
            get
            {
                if (unifiedRunner == null)
                {
                    unifiedRunner = GetComponent<ActionRunner>();
                    if (unifiedRunner == null) unifiedRunner = gameObject.AddComponent<ActionRunner>();
                    if (actionLibrary != null) unifiedRunner.Configure(actionLibrary);
                }
                return unifiedRunner;
            }
        }
        public CharacterActionLibrary ActionLibrary => actionLibrary;
        public void ConfigureActionLibrary(CharacterActionLibrary value) { actionLibrary = value; UnifiedRunner.Configure(value); }
        private ActionDefinition FindConfiguredAction(string actionId) => actionLibrary == null ? null : actionLibrary.Find(actionId);
        private bool HasAuthoredHitShapes(ActionDefinition action)
        {
            if(action==null||action.frameShapes==null)return false;
            foreach(ActionFrameShape shape in action.frameShapes)if(shape!=null&&shape.enabled&&shape.role!=ActionShapeRole.Hurtbox)return true;
            return false;
        }
        private bool TryPlayAuthoredAction(string actionId,float? damageOverride=null)
        {
            ActionDefinition definition=FindConfiguredAction(actionId);if(!HasAuthoredHitShapes(definition)||!UnifiedRunner.Play(definition))return false;
            UnifiedRunner.SetRuntimeDamageOverride(damageOverride);if(definition.combat.cooldown>0f)cooldownUntil[definition.id]=Time.time+definition.combat.cooldown;currentPriority=definition.combat.priority;return true;
        }
        private bool IsUnifiedCombo=>unifiedRunner!=null&&unifiedRunner.IsPlaying&&unifiedRunner.Current!=null&&unifiedRunner.Current.id=="AttackCombo4";
        private float ResolveActionDamage(string actionId, float fallback)
        {
            ActionDefinition configured=FindConfiguredAction(actionId);
            return configured == null || configured.combat == null ? fallback : Mathf.Max(0f,configured.combat.damage);
        }
        private float ResolveComboDamage(int segmentIndex,float fallback)
        {
            ActionDefinition configured=FindConfiguredAction("AttackCombo4");
            return configured==null||configured.combo==null||segmentIndex<0||segmentIndex>=configured.combo.Count?fallback:Mathf.Max(0f,configured.combo[segmentIndex].damage);
        }
        private ComboSegment[] ResolveRuntimeComboSegments()
        {
            ActionDefinition configured=FindConfiguredAction("AttackCombo4");
            if(configured==null||configured.combo==null||configured.combo.Count!=4)return DiaochanCombo;
            float fps=Mathf.Max(1f,configured.framesPerSecond);var result=new ComboSegment[4];
            for(int i=0;i<result.Length;i++)
            {
                ActionComboSegment source=configured.combo[i];
                int start=configured.ClampFrame(source.startFrame),hitStart=configured.ClampFrame(source.hitStartFrame),hitEnd=configured.ClampFrame(source.hitEndFrame);
                int windowStart=configured.ClampFrame(source.comboWindowStart),windowEnd=configured.ClampFrame(source.comboWindowEnd),end=configured.ClampFrame(source.endFrame);
                hitStart=Mathf.Max(start,hitStart);hitEnd=Mathf.Max(hitStart,hitEnd);windowStart=Mathf.Max(start,windowStart);windowEnd=Mathf.Max(windowStart,windowEnd);end=Mathf.Max(windowEnd,end);
                result[i]=new ComboSegment(start/fps,hitStart/fps,hitEnd/fps,windowStart/fps,windowEnd/fps,(end+1)/fps,source.damage,source.forwardMove,source.knockback);
            }
            return result;
        }
        public bool TryStartUnified(string actionId)
        {
            ActionDefinition definition = FindConfiguredAction(actionId);
            if (definition == null || IsBusyLegacyOnly()) return false;
            if (definition.combat.cooldown > 0f && IsOnCooldown(definition.id)) return false;
            if (!UnifiedRunner.Play(definition)) return false;
            if (definition.combat.cooldown > 0f) cooldownUntil[definition.id] = Time.time + definition.combat.cooldown;
            currentPriority = definition.combat.priority;
            return true;
        }
        private bool IsBusyLegacyOnly() => combo != null && combo.Active || currentAttack != null || charging;
    }
}
