using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Character.Controls
{
    public interface IGroundDetector
    {
        public bool IsGrounded { get; set; }
        public bool DetectGround();
    }
}