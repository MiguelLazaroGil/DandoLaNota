using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class CurtainControllerScript : MonoBehaviour
{
    [SerializeField] AlembicTimeController controller;
    [SerializeField] MeshRenderer curtains;
    [SerializeField]
    Vector3 positionOffset = new Vector3(0f, 0f, 0f);
    [SerializeField] private float moveDuration = 1.0f;
    private Vector3 baseLocalPosition;
    private Coroutine currentAnimationRoutine;

    private void Awake()
    {
        // Guardamos la posición Z local original al iniciar
        baseLocalPosition = transform.localPosition;
    }

    private void Start() // al cargarse en escena están cerradas y se abren
    {
        PlayCurtainAnimation(false);
    }

    public void PlayCurtainAnimation(bool close)
    {
        if (!curtains) return;


        // Si ya hay una animación en curso, la detenemos para evitar conflictos
        if (currentAnimationRoutine != null)
        {
            StopCoroutine(currentAnimationRoutine);
        }




        // Lanzamos la corrutina que mueve la Z de forma suave y luego ejecuta el Alembic
        currentAnimationRoutine = StartCoroutine(MovePositionAndPlayRoutine(close));
    }

    private IEnumerator MovePositionAndPlayRoutine(bool open)
    {
        Vector3 startPos = transform.localPosition;

        // Si se abren, el destino es la posición base más el offset completo en Vector3.
        // Si se cierran, volvemos a la posición base original.
        Vector3 targetPos = open ? (baseLocalPosition + positionOffset) : baseLocalPosition;

        float elapsed = 0f;
        controller.playReverse = !open;

        // Bucle de interpolación suave durante el intervalo de tiempo especificado
        while (elapsed < moveDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / moveDuration);
            float smoothT = Mathf.SmoothStep(0f, 1f, t); // Curva suave (Easing)

            transform.localPosition = Vector3.Lerp(startPos, targetPos, smoothT);

            yield return null;
        }

        // Aseguramos la posición final exacta
        transform.localPosition = targetPos;

        // Gestionamos el renderizado de las cortinas


        // Una vez finalizado por completo el desplazamiento vectorial, activamos el Alembic
        controller.PlayAlembic();
    }


}
