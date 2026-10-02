using UnityEngine;

namespace GVUtils.Attributes
{
    public class ShowIfAttribute : PropertyAttribute
    {
        public string expression;
        public object expectedValue;
        public bool comparison;

#if UNITY_6000_0_OR_NEWER
        public ShowIfAttribute(string expression, bool expectedValue = true) : base(applyToCollection: true)
        {
#else
        public ShowIfAttribute(string expression, bool expectedValue = true)
        {
#endif
            this.expression = expression;
            this.expectedValue = expectedValue;
            this.comparison = true;
        }

#if UNITY_6000_0_OR_NEWER
        public ShowIfAttribute(string expression, object expectedValue) : base(applyToCollection: true)
        {
#else
        public ShowIfAttribute(string expression, object expectedValue)
        {
#endif
            this.expression = expression;
            this.expectedValue = expectedValue;
            this.comparison = true;
        }
    }
}
namespace GVUtils.Attributes
{

    public class HideIfAttribute : ShowIfAttribute
    {
        public HideIfAttribute(string expression) : base(expression, true)
        {
            this.comparison = false;
        }

        public HideIfAttribute(string expression, object expectedValue) : base(expression, expectedValue)
        {
            this.comparison = false;
        }
    }
}