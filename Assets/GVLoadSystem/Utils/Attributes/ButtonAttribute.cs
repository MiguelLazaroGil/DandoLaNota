using System;

namespace GVUtils.Attributes
{
    /// <summary>
    /// Attribute to mark methods that should be drawn as buttons in the inspector.
    /// </summary>

    [AttributeUsage(AttributeTargets.Method)]
    public class ButtonAttribute : Attribute
    {
        public string Label;

        public ButtonAttribute(string label = null)
        {
            Label = label;

        }
    }
}