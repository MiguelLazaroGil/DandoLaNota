using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace Character.Controls
{
    public interface ICharacterController
    {
        public Vector3 MoveDirection { get; }
        public Vector2 inputDir { get; }


        public bool IsGrounded { get; }
        public bool IsMoving { get; }
        public bool IsJumping { get; }

       
    }
}