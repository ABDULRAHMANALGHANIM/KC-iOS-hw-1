using UnityEngine;

namespace NeonRequiem.Player
{
    /// <summary>Outcome of one collide-and-slide move.</summary>
    public struct MotorMoveResult
    {
        /// <summary>How far the body actually moved after collision resolution.</summary>
        public Vector3 ActualDisplacement;

        /// <summary>True when the body rests on ground after the move.</summary>
        public bool Grounded;

        /// <summary>True when the move was blocked horizontally.</summary>
        public bool HitSides;

        /// <summary>True when the move was blocked from above (head bump).</summary>
        public bool HitAbove;
    }

    /// <summary>
    /// The physical body the locomotion logic drives. Separating it from the decision logic lets
    /// <see cref="PlayerLocomotion"/> run against a simulated body in unit tests.
    /// </summary>
    public interface ICharacterMotor
    {
        Vector3 Position { get; }

        bool IsGrounded { get; }

        /// <summary>Surface normal under the body; up when airborne or unknown.</summary>
        Vector3 GroundNormal { get; }

        /// <summary>Moves by <paramref name="displacement"/>, resolving collisions.</summary>
        MotorMoveResult Move(Vector3 displacement);

        /// <summary>Places the body at <paramref name="position"/> without sweeping.</summary>
        void Teleport(Vector3 position);
    }
}
