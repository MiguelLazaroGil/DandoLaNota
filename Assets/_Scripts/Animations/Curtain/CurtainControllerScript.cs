using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class CurtainControllerScript : MonoBehaviour
{
    [SerializeField] AlembicTimeController controller;
    [SerializeField] MeshRenderer curtains;
    [SerializeField] SimpleSceneArrayLoader simpleSceneArrayLoader;
    [SerializeField]
    Vector3 positionOffset = new Vector3(0f, 0f, 0f);
    [SerializeField] private float moveDuration = 1.0f;
    [Tooltip("Animation curve for spacial movement")]
    [SerializeField] private AnimationCurve moveCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    private Vector3 baseLocalPosition;
    private Coroutine currentAnimationRoutine;

    private void Awake()
    {
        // Guardamos la posición Z local original al iniciar
        baseLocalPosition = transform.localPosition;
        curtains.enabled = true;
    }

    private void Start() // al cargarse en escena están cerradas y se abren
    {
        PlayCurtainAnimation(false);
    }
    public void CloseCurtainAndLoadScene(int idx)
    {
        PlayCurtainAnimation(true);
        controller.onEndAnimation.AddListener(() => simpleSceneArrayLoader.LoadSceneAt(idx));


    }
    public void PlayCurtainAnimation(bool close)
    {
        if (!curtains) return;


        // Si ya hay una animación en curso, la detenemos para evitar conflictos
        if (currentAnimationRoutine != null)
        {
            StopCoroutine(currentAnimationRoutine);
        }




        currentAnimationRoutine = StartCoroutine(CurtainSequenceRoutine(close));
    }
    private IEnumerator CurtainSequenceRoutine(bool close)
    {
        if (close)
        {

            yield return StartCoroutine(MovePositionRoutine(baseLocalPosition + positionOffset, baseLocalPosition));

            controller.playReverse = false;
            controller.PlayAlembic();

        }
        else
        {

            controller.playReverse = true;
            controller.PlayAlembic();


            yield return new WaitForSeconds(controller.Duration);


            yield return StartCoroutine(MovePositionRoutine(baseLocalPosition, baseLocalPosition + positionOffset));
        }
    }

    private IEnumerator MovePositionRoutine(Vector3 startPos, Vector3 targetPos)
    {
        float elapsed = 0f;

        while (elapsed < moveDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / moveDuration);

            // Evaluamos la AnimationCurve en lugar de SmoothStep directo
            float curveT = moveCurve.Evaluate(t);

            transform.localPosition = Vector3.Lerp(startPos, targetPos, curveT);

            yield return null;
        }

        // Aseguramos la posición final exacta
        transform.localPosition = targetPos;
    }
    void OnDestroy()
    {
        controller.onEndAnimation.RemoveAllListeners();
    }
}
