using System;
using System.Collections.Generic;
using UnityEngine;

namespace ThreeKingdoms
{
    public enum ActionCategory { Locomotion, NormalAttack, HeavyAttack, Skill, Defense, Dodge, Jump, Reaction, Death }
    public enum ActionShapeType { Box, Polygon }
    public enum ActionShapeRole { Hurtbox, DamageHitbox, EffectHitbox, WeaponHitbox }
    public enum ActionHitPolicy { OncePerAction, OncePerPhase, OncePerFrame, RepeatInterval }
    public enum AttackImpactType { Auto, Light, Heavy }
    public enum HitWeight { Light, Heavy }

    [Serializable]
    public sealed class ActionFrameShape
    {
        public int frame;
        public ActionShapeRole role = ActionShapeRole.DamageHitbox;
        public ActionShapeType shapeType = ActionShapeType.Box;
        public bool enabled = true;
        [Tooltip("When enabled, this frame shape uses its own damage instead of the Action damage.")]
        public bool overrideDamage;
        [Min(0f)] public float damage = 10f;
        public Vector2 center = new Vector2(1f, 1f);
        public Vector2 size = new Vector2(1.5f, 1.8f);
        public List<Vector2> points = new List<Vector2>();

        public ActionFrameShape Clone()
        {
            return new ActionFrameShape { frame = frame, role = role, shapeType = shapeType, enabled = enabled, overrideDamage = overrideDamage, damage = damage,
                center = center, size = size, points = points == null ? new List<Vector2>() : new List<Vector2>(points) };
        }
        public float EffectiveDamage(float actionDamage) => overrideDamage ? Mathf.Max(0f, damage) : Mathf.Max(0f, actionDamage);
    }

    [Serializable]
    public sealed class ActionCombatData
    {
        public float damage = 10f;
        public float hitStop = .035f;
        public float hitStun = .2f;
        public float knockbackX = .45f;
        public float knockbackDepth = .1f;
        public float cooldown;
        public ActionPriority priority = ActionPriority.NormalCombo;
        public float invulnerability;
        public bool superArmor;
        public ActionHitPolicy hitPolicy = ActionHitPolicy.OncePerAction;
        public float repeatInterval = .15f;
        public float broadPhaseDepth = .75f;
        [Tooltip("Auto: Normal attacks are Light; HeavyAttack and Skill actions are Heavy.")]
        public AttackImpactType impactType = AttackImpactType.Auto;
        public ActionCombatData Clone() => new ActionCombatData
        {
            damage=damage,hitStop=hitStop,hitStun=hitStun,knockbackX=knockbackX,knockbackDepth=knockbackDepth,cooldown=cooldown,
            priority=priority,invulnerability=invulnerability,superArmor=superArmor,hitPolicy=hitPolicy,repeatInterval=repeatInterval,broadPhaseDepth=broadPhaseDepth,impactType=impactType
        };
    }

    [Serializable]
    public sealed class HitReactionProfile
    {
        [Tooltip("Action/Animator state used for this reaction.")]
        public string animationActionId = "HitReact";
        [Tooltip("How long this character remains unable to act after this hit.")]
        [Min(.01f)] public float stunDuration = .34f;
        [Tooltip("Total gameplay-root retreat during this reaction. 0 means no retreat. Direction comes from the incoming hit.")]
        [Min(0f)] public float retreatDistance;
    }

    [Serializable]
    public sealed class ActionReactionData
    {
        public HitReactionProfile light = new HitReactionProfile();
        public HitReactionProfile heavy = new HitReactionProfile();
        public HitReactionProfile Profile(HitWeight weight)
        {
            if(light==null)light=new HitReactionProfile();if(heavy==null)heavy=new HitReactionProfile();return weight==HitWeight.Heavy?heavy:light;
        }
    }

    [Serializable]
    public sealed class ActionMovementData
    {
        public AnimationCurve moveX = AnimationCurve.Linear(0f, 0f, 1f, 0f);
        public AnimationCurve moveDepth = AnimationCurve.Linear(0f, 0f, 1f, 0f);
        public AnimationCurve elevation = AnimationCurve.Linear(0f, 0f, 1f, 0f);
    }

    [Serializable]
    public sealed class ActionFrameVisualOffset
    {
        public int frame;
        public Vector2 offset;
    }

    [Serializable]
    public sealed class ActionComboSegment
    {
        public string input = "J";
        public int startFrame, hitStartFrame, hitEndFrame, comboWindowStart, comboWindowEnd, endFrame;
        public float damage = 10f, forwardMove, knockback = .3f;
    }

    [Serializable]
    public sealed class ActionAIUsageData
    {
        public bool enabledForAI;
        public float usageWeight = 1f;
        public float minDistance;
        public float maxDistance = 2.5f;
        public float depthTolerance = .75f;
        public float cooldown = 1f;
        public bool canUseWhileRunning;
    }

    [Serializable]
    public sealed class WeaponPoseData
    {
        public bool enabled;
        public Vector2 socket;
        [TextArea] public string status = "NOT IMPLEMENTED - data/preview only in V0.1";
    }

    [CreateAssetMenu(menuName = "Three Kingdoms/Combat/Action Definition", fileName = "ACT_NewAction")]
    public sealed class ActionDefinition : ScriptableObject
    {
        public const int CurrentVersion = 4;
        public int dataVersion = CurrentVersion;
        public string id;
        public string ownerId;
        public string displayName;
        public ActionCategory category;
        public AnimationClip animation;
        public string animatorState;
        public bool loop;
        public bool lockGroundPosition;
        [Min(1f)] public float framesPerSecond = 12f;
        [Min(1)] public int frameCount = 1;
        public int startupEndFrame;
        public int activeEndFrame;
        public int recoveryEndFrame;
        public Vector2 footPoint;
        public Vector2 defaultHurtboxCenter = new Vector2(0f, 1.1f);
        public Vector2 defaultHurtboxSize = new Vector2(.8f, 2.2f);
        public List<ActionFrameShape> frameShapes = new List<ActionFrameShape>();
        [Tooltip("Per-frame visual correction. This moves only the rendered character, not the gameplay root or hitboxes.")]
        public List<ActionFrameVisualOffset> frameVisualOffsets = new List<ActionFrameVisualOffset>();
        [Tooltip("Exact ordered Sprite frames used by runtime. Battle Editor synchronizes this list from the AnimationClip on Save.")]
        public List<Sprite> visualFrames = new List<Sprite>();
        public ActionMovementData movement = new ActionMovementData();
        public ActionCombatData combat = new ActionCombatData();
        [Tooltip("Per-character Light/Heavy response. Used when this is the character's HitReact action.")]
        public ActionReactionData reaction = new ActionReactionData();
        public List<ActionComboSegment> combo = new List<ActionComboSegment>();
        public ActionAIUsageData ai = new ActionAIUsageData();
        public WeaponPoseData weapon = new WeaponPoseData();
        public AttackData legacyAttackData;

        public float Duration => frameCount / Mathf.Max(1f, framesPerSecond);
        public int ClampFrame(int value) => Mathf.Clamp(value, 0, Mathf.Max(0, frameCount - 1));
        public IEnumerable<ActionFrameShape> ShapesAt(int frame)
        {
            if (frameShapes == null) yield break;
            for (int i = 0; i < frameShapes.Count; i++)
                if (frameShapes[i] != null && frameShapes[i].enabled && frameShapes[i].frame == frame) yield return frameShapes[i];
        }
        public ActionFrameShape FindShape(int frame, ActionShapeRole role)
        {
            if (frameShapes == null) return null;
            return frameShapes.Find(s => s != null && s.frame == frame && s.role == role);
        }
        public Vector2 VisualOffsetAt(int frame)
        {
            if(frameVisualOffsets==null)return Vector2.zero;
            ActionFrameVisualOffset value=frameVisualOffsets.Find(item=>item!=null&&item.frame==ClampFrame(frame));
            return value==null?Vector2.zero:value.offset;
        }
        public void SetVisualOffset(int frame,Vector2 offset)
        {
            if(frameVisualOffsets==null)frameVisualOffsets=new List<ActionFrameVisualOffset>();
            int target=ClampFrame(frame);ActionFrameVisualOffset value=frameVisualOffsets.Find(item=>item!=null&&item.frame==target);
            if(offset.sqrMagnitude<.00000001f){if(value!=null)frameVisualOffsets.Remove(value);return;}
            if(value==null){value=new ActionFrameVisualOffset{frame=target};frameVisualOffsets.Add(value);}value.offset=offset;
        }
        public Sprite VisualFrameAt(int frame)
        {
            if(visualFrames==null||visualFrames.Count==0)return null;
            return visualFrames[Mathf.Clamp(frame,0,visualFrames.Count-1)];
        }
        public HitWeight ResolveImpactWeight()
        {
            if(combat!=null&&combat.impactType==AttackImpactType.Light)return HitWeight.Light;
            if(combat!=null&&combat.impactType==AttackImpactType.Heavy)return HitWeight.Heavy;
            return category==ActionCategory.HeavyAttack||category==ActionCategory.Skill?HitWeight.Heavy:HitWeight.Light;
        }
    }

    public static class ActionGeometry
    {
        public static Vector2 Mirror(Vector2 point, int facing) => new Vector2(facing < 0 ? -point.x : point.x, point.y);
        public static Rect BoxRect(ActionFrameShape shape, int facing)
        {
            Vector2 center = Mirror(shape.center, facing);
            return new Rect(center - shape.size * .5f, shape.size);
        }
        public static bool Contains(ActionFrameShape shape, Vector2 localPoint, int facing)
        {
            if (shape == null || !shape.enabled) return false;
            if (shape.shapeType == ActionShapeType.Box) return BoxRect(shape, facing).Contains(localPoint);
            if (shape.points == null || shape.points.Count < 3) return false;
            bool inside = false;
            for (int i = 0, j = shape.points.Count - 1; i < shape.points.Count; j = i++)
            {
                Vector2 a = Mirror(shape.points[i], facing), b = Mirror(shape.points[j], facing);
                if (((a.y > localPoint.y) != (b.y > localPoint.y)) &&
                    localPoint.x < (b.x - a.x) * (localPoint.y - a.y) / ((b.y - a.y) + Mathf.Epsilon) + a.x) inside = !inside;
            }
            return inside;
        }
        public static bool IntersectsBox(ActionFrameShape shape, Vector2 boxCenter, Vector2 boxSize, int facing)
        {
            Rect box=new Rect(boxCenter-boxSize*.5f,boxSize);
            if(shape.shapeType==ActionShapeType.Box)return BoxRect(shape,facing).Overlaps(box,true);
            if(shape.points==null||shape.points.Count<3)return false;
            for(int i=0;i<shape.points.Count;i++)if(box.Contains(Mirror(shape.points[i],facing)))return true;
            Vector2[] corners={box.min,new Vector2(box.xMax,box.yMin),box.max,new Vector2(box.xMin,box.yMax)};
            for(int i=0;i<corners.Length;i++)if(Contains(shape,corners[i],facing))return true;
            for(int i=0;i<shape.points.Count;i++){Vector2 a=Mirror(shape.points[i],facing),b=Mirror(shape.points[(i+1)%shape.points.Count],facing);for(int j=0;j<4;j++)if(SegmentsIntersect(a,b,corners[j],corners[(j+1)%4]))return true;}
            return false;
        }
        private static bool SegmentsIntersect(Vector2 a,Vector2 b,Vector2 c,Vector2 d)
        {
            float ab1=Cross(b-a,c-a),ab2=Cross(b-a,d-a),cd1=Cross(d-c,a-c),cd2=Cross(d-c,b-c);return ab1*ab2<=0f&&cd1*cd2<=0f;
        }
        private static float Cross(Vector2 a,Vector2 b)=>a.x*b.y-a.y*b.x;
        public static Bounds GetBounds(ActionFrameShape shape, int facing)
        {
            if (shape.shapeType == ActionShapeType.Box)
            {
                Rect r = BoxRect(shape, facing); return new Bounds(r.center, r.size);
            }
            if (shape.points == null || shape.points.Count == 0) return new Bounds();
            Vector2 p = Mirror(shape.points[0], facing); Bounds bounds = new Bounds(p, Vector3.zero);
            for (int i = 1; i < shape.points.Count; i++) bounds.Encapsulate(Mirror(shape.points[i], facing));
            return bounds;
        }
    }
}
