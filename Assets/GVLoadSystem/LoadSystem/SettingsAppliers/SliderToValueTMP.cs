using TMPro;
using UnityEngine;

namespace GVLoadSystem.Examples
{
    public class SliderToValueTMP : MonoBehaviour
    {
        public TMP_Text text;

        public void SetValue(float value)
        {
            text.text = value.ToString("0.00"); // or "0.00" if you prefer decimals
        }
    }
}