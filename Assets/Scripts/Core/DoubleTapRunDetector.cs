using System;
using UnityEngine;

namespace ThreeKingdoms
{
    public enum RunDirection { None, Left, Right, Back, Front }

    [Serializable]
    public sealed class DoubleTapRunDetector
    {
        private float doubleTapWindow;
        private float firstTapMaxDuration;
        private float secondTapHoldThreshold;
        private RunDirection pressedDirection;
        private float pressedAt;
        private RunDirection pendingTapDirection;
        private float pendingTapReleasedAt;
        private RunDirection candidateDirection;
        private float candidatePressedAt;

        public RunDirection RunningDirection { get; private set; }
        public bool IsRunning => RunningDirection != RunDirection.None;

        public DoubleTapRunDetector(float doubleTapWindow = .25f, float firstTapMaxDuration = .20f, float secondTapHoldThreshold = .05f)
        {
            Configure(doubleTapWindow, firstTapMaxDuration, secondTapHoldThreshold);
        }

        public void Configure(float window, float firstTapDuration, float secondHoldThreshold)
        {
            doubleTapWindow = Mathf.Max(.05f, window);
            firstTapMaxDuration = Mathf.Clamp(firstTapDuration, .02f, doubleTapWindow);
            secondTapHoldThreshold = Mathf.Clamp(secondHoldThreshold, .01f, doubleTapWindow);
            Reset();
        }

        public void Press(RunDirection direction, float now)
        {
            if (direction == RunDirection.None) return;
            bool withinWindow = pendingTapDirection == direction && now - pendingTapReleasedAt <= doubleTapWindow;
            candidateDirection = withinWindow ? direction : RunDirection.None;
            candidatePressedAt = now;
            pressedDirection = direction;
            pressedAt = now;
            if (!withinWindow && now - pendingTapReleasedAt > doubleTapWindow) pendingTapDirection = RunDirection.None;
        }

        public void Release(RunDirection direction, float now)
        {
            if (RunningDirection == direction) RunningDirection = RunDirection.None;
            if (candidateDirection == direction) candidateDirection = RunDirection.None;
            if (pressedDirection != direction) return;
            float held = now - pressedAt;
            if (held <= firstTapMaxDuration)
            {
                pendingTapDirection = direction;
                pendingTapReleasedAt = now;
            }
            else pendingTapDirection = RunDirection.None;
            pressedDirection = RunDirection.None;
        }

        public bool Evaluate(Vector2 input, float now)
        {
            if (pendingTapDirection != RunDirection.None && now - pendingTapReleasedAt > doubleTapWindow) pendingTapDirection = RunDirection.None;
            if (RunningDirection != RunDirection.None)
            {
                if (Compatible(input, RunningDirection)) return true;
                RunningDirection = RunDirection.None;
            }
            if (candidateDirection == RunDirection.None || now - candidatePressedAt < secondTapHoldThreshold || !Compatible(input, candidateDirection)) return false;
            RunningDirection = candidateDirection;
            candidateDirection = RunDirection.None;
            pendingTapDirection = RunDirection.None;
            return true;
        }

        public void Reset()
        {
            pressedDirection = pendingTapDirection = candidateDirection = RunningDirection = RunDirection.None;
            pressedAt = pendingTapReleasedAt = candidatePressedAt = 0f;
        }

        public static Vector2 VectorFor(RunDirection direction) => direction switch
        {
            RunDirection.Left => Vector2.left,
            RunDirection.Right => Vector2.right,
            RunDirection.Back => Vector2.up,
            RunDirection.Front => Vector2.down,
            _ => Vector2.zero
        };

        private static bool Compatible(Vector2 input, RunDirection direction)
        {
            if (input.sqrMagnitude < .01f) return false;
            return Vector2.Dot(input.normalized, VectorFor(direction)) >= .65f;
        }
    }
}
