using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ThreeKingdoms.EditorTools
{
    public static class ActionEditorUtility
    {
        public static List<Sprite> ReadAnimationFrames(AnimationClip clip)
        {
            var frames = new List<Sprite>();
            if (clip == null) return frames;
            foreach (EditorCurveBinding binding in AnimationUtility.GetObjectReferenceCurveBindings(clip))
            {
                if (binding.type != typeof(SpriteRenderer) || binding.propertyName != "m_Sprite") continue;
                foreach (ObjectReferenceKeyframe key in AnimationUtility.GetObjectReferenceCurve(clip, binding))
                    if (key.value is Sprite sprite) frames.Add(sprite);
                if (frames.Count > 0) break;
            }
            return frames;
        }

        public static List<string> ValidateAction(ActionDefinition action)
        {
            var result = new List<string>();
            if (action == null) { result.Add("ERROR: No ActionDefinition selected."); return result; }
            if (action.animation == null) result.Add("ERROR: AnimationClip is missing.");
            if (action.frameCount < 1) result.Add("ERROR: Frame count must be greater than zero.");
            if (action.startupEndFrame < 0 || action.activeEndFrame < action.startupEndFrame || action.recoveryEndFrame < action.activeEndFrame || action.recoveryEndFrame >= action.frameCount)
                result.Add("ERROR: Startup/Active/Recovery frame range is invalid.");
            if (action.frameShapes != null)
            {
                foreach (ActionFrameShape shape in action.frameShapes)
                {
                    if (shape == null) continue;
                    if (shape.frame < 0 || shape.frame >= action.frameCount) result.Add($"ERROR: Shape frame {shape.frame} is outside the animation.");
                    if (shape.shapeType == ActionShapeType.Polygon && (shape.points == null || shape.points.Count < 3)) result.Add($"ERROR: Frame {shape.frame} {shape.role} polygon has less than 3 points.");
                    if (shape.shapeType == ActionShapeType.Box && (shape.size.x <= 0f || shape.size.y <= 0f)) result.Add($"ERROR: Frame {shape.frame} box size must be positive.");
                }
            }
            if(action.frameVisualOffsets!=null)
            {
                var seen=new HashSet<int>();foreach(ActionFrameVisualOffset item in action.frameVisualOffsets)
                {
                    if(item==null)continue;if(item.frame<0||item.frame>=action.frameCount)result.Add($"ERROR: Visual offset frame {item.frame} is outside the animation.");
                    if(!seen.Add(item.frame))result.Add($"ERROR: Frame {item.frame} has duplicate visual offsets.");
                }
            }
            bool activeHasHitbox = false;
            for (int frame = action.startupEndFrame; frame <= action.activeEndFrame; frame++)
                if (action.frameShapes != null && action.frameShapes.Exists(s => s != null && s.enabled && s.frame == frame && s.role != ActionShapeRole.Hurtbox)) activeHasHitbox = true;
            if (action.category != ActionCategory.Locomotion && action.category != ActionCategory.Defense && !activeHasHitbox) result.Add("WARNING: Active frames have no damage/effect hitbox.");
            if (action.combat.hitPolicy == ActionHitPolicy.RepeatInterval && action.combat.repeatInterval <= 0f) result.Add("ERROR: Repeat interval must be positive.");
            if (action.combo != null) foreach (ActionComboSegment segment in action.combo)
                if (segment.startFrame<0||segment.hitStartFrame > segment.hitEndFrame || segment.comboWindowStart > segment.comboWindowEnd || segment.comboWindowStart<segment.startFrame||segment.comboWindowEnd>segment.endFrame||segment.endFrame >= action.frameCount)
                    result.Add("ERROR: Combo segment frame ordering is invalid.");
            if (action.ai.enabledForAI && (action.ai.maxDistance < action.ai.minDistance || action.ai.depthTolerance < 0f)) result.Add("WARNING: AI usage distance is invalid.");
            if (action.weapon.enabled) result.Add("WARNING: WeaponPoseData is preview/data only; separate weapon execution is NOT IMPLEMENTED in V0.1.");
            if (result.Count == 0) result.Add("PASS: Action data is valid.");
            return result;
        }

        public static bool HasLegacyComboFrames(ActionDefinition action)
        {
            if(action==null||action.combo==null)return false;return action.combo.Exists(s=>s!=null&&(s.startFrame>=action.frameCount||s.hitStartFrame>=action.frameCount||s.hitEndFrame>=action.frameCount||s.comboWindowStart>=action.frameCount||s.comboWindowEnd>=action.frameCount||s.endFrame>=action.frameCount));
        }

        public static void ConvertLegacyComboFrames(ActionDefinition action)
        {
            if(action==null||action.combo==null||action.combo.Count==0)return;float ratio=Mathf.Max(1f,action.framesPerSecond)/60f;
            for(int i=0;i<action.combo.Count;i++)
            {
                ActionComboSegment s=action.combo[i];s.startFrame=action.ClampFrame(Mathf.RoundToInt(s.startFrame*ratio));s.hitStartFrame=action.ClampFrame(Mathf.RoundToInt(s.hitStartFrame*ratio));s.hitEndFrame=action.ClampFrame(Mathf.RoundToInt(s.hitEndFrame*ratio));s.comboWindowStart=action.ClampFrame(Mathf.RoundToInt(s.comboWindowStart*ratio));s.comboWindowEnd=action.ClampFrame(Mathf.RoundToInt(s.comboWindowEnd*ratio));
            }
            for(int i=0;i<action.combo.Count;i++){ActionComboSegment s=action.combo[i];s.endFrame=i+1<action.combo.Count?Mathf.Max(s.comboWindowEnd,action.combo[i+1].startFrame):action.frameCount-1;ClampComboSegment(action,s);}
        }

        public static void ClampComboSegment(ActionDefinition action,ActionComboSegment s)
        {
            if(action==null||s==null)return;s.startFrame=action.ClampFrame(s.startFrame);s.hitStartFrame=Mathf.Clamp(s.hitStartFrame,s.startFrame,action.frameCount-1);s.hitEndFrame=Mathf.Clamp(s.hitEndFrame,s.hitStartFrame,action.frameCount-1);s.endFrame=Mathf.Clamp(s.endFrame,s.startFrame,action.frameCount-1);s.comboWindowStart=Mathf.Clamp(s.comboWindowStart,s.startFrame,s.endFrame);s.comboWindowEnd=Mathf.Clamp(s.comboWindowEnd,s.comboWindowStart,s.endFrame);
        }

        public static ActionFrameShape CreateBox(ActionDefinition action, int frame, ActionShapeRole role, Vector2 center, Vector2 size)
        {
            var shape = new ActionFrameShape { frame = action.ClampFrame(frame), role = role, shapeType = ActionShapeType.Box, center = center, size = size };
            action.frameShapes.Add(shape); return shape;
        }
        public static ActionFrameShape CreatePolygon(ActionDefinition action, int frame, ActionShapeRole role, IEnumerable<Vector2> points)
        {
            var shape = new ActionFrameShape { frame = action.ClampFrame(frame), role = role, shapeType = ActionShapeType.Polygon, points = new List<Vector2>(points) };
            action.frameShapes.Add(shape); return shape;
        }
        public static int CopyPrevious(ActionDefinition action, int frame)
        {
            if (frame <= 0) return 0;
            int count = 0; var copies = new List<ActionFrameShape>();
            foreach (ActionFrameShape shape in action.frameShapes) if (shape.frame == frame - 1) { var clone = shape.Clone(); clone.frame = frame; copies.Add(clone); }
            action.frameShapes.RemoveAll(s => s.frame == frame); action.frameShapes.AddRange(copies); count += copies.Count; return count;
        }
        public static int CopyRange(ActionDefinition action, int sourceFrame, int from, int to)
        {
            var source = action.frameShapes.FindAll(s => s.frame == sourceFrame); int count = 0;
            for (int frame = Mathf.Max(0, from); frame <= Mathf.Min(action.frameCount - 1, to); frame++)
            {
                action.frameShapes.RemoveAll(s => s.frame == frame);
                foreach (ActionFrameShape shape in source) { var copy = shape.Clone(); copy.frame = frame; action.frameShapes.Add(copy); count++; }
            }
            return count;
        }
    }
}
