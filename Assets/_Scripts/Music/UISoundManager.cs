
using UnityEngine;
using FMODUnity;

public class UISoundManager : MonoBehaviour
{

    public void PlayClick(EventReference clickSound)
    {
        RuntimeManager.PlayOneShot(clickSound);
    }

    public void PlayHover(EventReference hoverSound)
    {
        RuntimeManager.PlayOneShot(hoverSound);
    }

    public void PlayBack(EventReference backSound)
    {
        RuntimeManager.PlayOneShot(backSound);
    }
}
