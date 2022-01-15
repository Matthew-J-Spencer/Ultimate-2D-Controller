using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TarodevController {
    public struct FrameInput {
        public float X;
        public bool JumpDown;
        public bool JumpUp;
    }

    public interface IPlayerController {
        public Vector3 Velocity { get; }
        public FrameInput Input { get; }
        public bool JumpingThisFrame { get; }
        public bool LandingThisFrame { get; }
        public Vector3 RawMovement { get; }
        public bool Grounded { get; }
    }

    public struct RayRange {
        public RayRange(float x1, float y1, float x2, float y2, Vector2 dir) {
            Start = new Vector2(x1, y1);
            End = new Vector2(x2, y2);
            Dir = dir;
        }

        public readonly Vector2 Start, End, Dir;
    }
}