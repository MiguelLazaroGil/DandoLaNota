using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GVLoadSystem.GVEditor
{
    /// <summary>
    /// Marks a GameObject field that should use the PickGameObjectComponentDrawer.
    /// The drawer will allow picking a GameObject and automatically assign the specified component type.
    /// </summary>
    public class PickGameObjectComponentAttribute : PropertyAttribute
    {
        // No content for now — just a marker for the custom drawer to know which fields to target
    }
}