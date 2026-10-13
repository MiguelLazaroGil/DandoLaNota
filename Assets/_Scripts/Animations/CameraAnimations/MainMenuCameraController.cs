using UnityEngine;

public class MainMenuCameraController : MonoBehaviour
{
    Animator animator;
    void OnEnable()
    {
        animator = GetComponent<Animator>();
    }
    public void GoToHome()
    {
        animator.SetTrigger("GoToHome");

    }
    public void GoToPlaySelectionMenu()
    {
        animator.SetTrigger("GoToPlaySelection");
    }
    public void GoToLevelSelection()
    {
        animator.SetTrigger("GoToLevelSelection");
    }
}
