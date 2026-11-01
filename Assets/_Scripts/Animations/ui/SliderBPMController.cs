using UnityEngine;
using UnityEngine.UI;

public class SliderBPMController : MonoBehaviour
{
    [SerializeField]
    MusicScoreManager MSManager;
    [SerializeField]
    Slider slider;
    public void Start()
    {
        if (MSManager == null)
        {
            MSManager = FindAnyObjectByType<MusicScoreManager>();
            if (MSManager == null)
            {
                gameObject.SetActive(false);
            }
        }
    }
    public void Update()
    {
        if (MSManager)
        {
            slider.value = MSManager.currentRhythm;
        }
    }
}
