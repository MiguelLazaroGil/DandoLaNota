using System;
using System.Collections;
using System.Collections.Generic;
using GVLoadSystem.Core;
using UnityEngine;

namespace GVLoadSystem.Examples
{
   [CustomGVData("CustomColor")]
   [Serializable]
   public class CustomColorData
   {
      public float r, g, b, a;
   }
}