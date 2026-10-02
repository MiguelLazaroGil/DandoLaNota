using UnityEngine;
using UnityEngine.UI;
namespace MLG.DelayedActionsTool.Samples
{
    internal class SliderValues : MonoBehaviour
    {
        public DACodeScript codeScript;

        [SerializeField]
        Slider floatASlider;
        [SerializeField]
        Slider intASlider;
        [SerializeField]
        Text floatAText;
        [SerializeField]
        Text intAText;
        [SerializeField]
        Slider floatBSlider;
        [SerializeField]
        Text floatBText;

        private void Start()
        {
            if (codeScript == null)
            {
                return;
            }
            if (floatASlider != null)
            {

                floatASlider.value = codeScript.floatA;
                floatAText.text = codeScript.floatA.ToString("0.00");

                floatASlider.onValueChanged.AddListener((value) =>
                {
                    codeScript.floatA = value;
                    floatAText.text = value.ToString("0.00");
                });
            }
            if (intASlider != null)
            {
                intASlider.value = codeScript.intA;
                intAText.text = codeScript.intA.ToString();
                intASlider.onValueChanged.AddListener((value) =>
                {
                    codeScript.intA = (int)value;
                    intAText.text = ((int)value).ToString();
                });

            }
            if (floatBSlider != null)
            {
                floatBSlider.value = codeScript.floatB;
                floatBText.text = codeScript.floatB.ToString("0.00");
                floatBSlider.onValueChanged.AddListener((value) =>
                {
                    codeScript.floatB = value;
                    floatBText.text = value.ToString("0.00");
                });
            }

        }
    }
    internal class DACodeScript : MonoBehaviour
    {
        [HideInInspector]
        public float floatA = 2f;
        [HideInInspector]
        public float floatB = 1f;
        [HideInInspector]
        public int intA = 2;

    }
}