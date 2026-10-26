using System.Collections;
using UnityEngine;

public class CurtainControllerScript : MonoBehaviour
{
    [SerializeField] AlembicTimeController controller;
    [SerializeField] MeshRenderer curtains;
    [SerializeField] SimpleSceneArrayLoader simpleSceneArrayLoader;
    [SerializeField] Vector3 positionOffset = new Vector3(0f, 0f, 0f);
    [SerializeField] private float moveDuration = 1.0f;
    [Tooltip("Animation curve for spacial movement")]
    [SerializeField] private AnimationCurve moveCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    private Vector3 baseLocalPosition;
    private Coroutine currentAnimationRoutine;

    private void Awake()
    {
        baseLocalPosition = transform.localPosition;
        curtains.enabled = true;
    }

    private void Start()
    {
        StartCoroutine(InitAndPlayRoutine());
    }

    private IEnumerator InitAndPlayRoutine()
    {
        float timeout = 3f;
        float elapsed = 0f;

        while (controller == null || controller.Duration <= 0f)
        {
            elapsed += Time.unscaledDeltaTime;
            if (elapsed > timeout) break;
            yield return null;
        }

        // Al iniciar la escena, abrimos las cortinas (reverso activado)
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
            // Cerrar: Reproducción normal (de principio a fin)
            controller.playReverse = false;
            controller.PlayAlembic();

            yield return StartCoroutine(MovePositionRoutine(baseLocalPosition + positionOffset, baseLocalPosition));
        }
        else
        {
            // Abrir: Reproducción en reverso (del final al principio)
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
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / moveDuration);
            float curveT = moveCurve.Evaluate(t);

            transform.localPosition = Vector3.Lerp(startPos, targetPos, curveT);

            yield return null;
        }

        transform.localPosition = targetPos;
    }

    void OnDestroy()
    {
        if (controller != null)
        {
            controller.onEndAnimation.RemoveAllListeners();
        }
    }
}