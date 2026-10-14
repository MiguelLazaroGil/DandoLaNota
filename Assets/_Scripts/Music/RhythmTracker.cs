
using System;
using UnityEngine;

public class RhythmTracker : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private BatutaController batutaController;
    [SerializeField] private Transform mano;

    [Header("Configuracion musical")]
    [SerializeField, Min(1f)] private float bpm = 100f;

    [SerializeField, Range(0.05f, 0.3f)]
    private float toleranciaTiempo = 0.18f;
    [Header("Puntuacion musical")]
    [SerializeField] private MusicScoreManager musicScoreManager;

    [Header("Deteccion de gestos")]
    [SerializeField, Min(0.01f)]
    private float distanciaMinima = 0.12f;

    [SerializeField, Range(0.5f, 1f)]
    private float precisionDireccion = 0.75f;

    [Header("Deteccion de trayectoria")]
    [SerializeField, Min(0.01f)]
    private float velocidadInicio = 0.12f;

    [SerializeField, Range(0.1f, 0.8f)]
    private float porcentajeFrenado = 0.30f;

    [SerializeField, Min(1)]
    private int framesFrenado = 2;

    [SerializeField, Range(0.1f, 1f)]
    private float toleranciaDireccionEsperada = 0.40f;

    [SerializeField, Range(0.1f, 1f)]
    private float minimoEjeEsperado = 0.45f;

    [Header("Sincronizacion")]
    [SerializeField, Range(0f, 0.3f)]
    private float margenDeteccion = 0.12f;

    [Header("Pruebas")]
    [SerializeField]
    private bool iniciarAlActivarDirector = true;

    private readonly Vector2[] direcciones =
    {
        Vector2.down,
        Vector2.left,
        Vector2.right,
        Vector2.up
    };

    private readonly string[] nombres =
    {
        "ABAJO",
        "IZQUIERDA",
        "DERECHA",
        "ARRIBA"
    };

    // Reloj musical
    private double tiempoInicio;
    private double duracionPulso;
    private int ultimoPulsoEvaluado;

    // Patron de direccion
    private bool patronSincronizado;
    private int siguienteDireccion;
    private int aciertosConsecutivos;

    // Estado
    private bool seguimientoActivo;
    private bool directorAnterior;

    // Seguimiento de trayectoria
    private bool gestoActivo;
    private Vector2 posicionAnterior;
    private Vector2 posicionInicioGesto;
    private Vector2 desplazamientoMaximo;

    private float velocidadMaxima;
    private int contadorFrenado;
    private double tiempoPico;

    private void LateUpdate()
    {
        if (batutaController == null || mano == null)
            return;

        bool directorActivo = batutaController.modoDirectorActivo;

        if (directorActivo != directorAnterior)
        {
            directorAnterior = directorActivo;

            if (directorActivo && iniciarAlActivarDirector)
                IniciarSeguimiento();
            else if (!directorActivo)
                DetenerSeguimiento();
        }

        if (!seguimientoActivo || !directorActivo)
            return;

        double ahora = Time.realtimeSinceStartupAsDouble;
        Vector2 posicionActual = ObtenerPosicion();

        // Comprobamos pulsos musicales perdidos.
        ComprobarPulsosPerdidos(ahora);

        // Solo analizamos la batuta con el clic mantenido.
        if (!batutaController.BotonPulsado)
        {
            ReiniciarGesto();
            posicionAnterior = posicionActual;
            return;
        }

        float deltaTime = Time.unscaledDeltaTime;

        if (deltaTime <= 0f)
            return;

        // Velocidad de la mano en unidades locales/segundo.
        Vector2 velocidad =
            (posicionActual - posicionAnterior) / deltaTime;

        float rapidez = velocidad.magnitude;

        // FASE 1: detectar el inicio del gesto.
        if (!gestoActivo)
        {
            if (rapidez >= velocidadInicio)
            {
                gestoActivo = true;

                posicionInicioGesto = posicionAnterior;
                desplazamientoMaximo = Vector2.zero;

                velocidadMaxima = rapidez;
                contadorFrenado = 0;
                tiempoPico = ahora;
            }

            posicionAnterior = posicionActual;
            return;
        }

        // FASE 2: analizar toda la trayectoria.
        Vector2 desplazamientoTotal =
            posicionActual - posicionInicioGesto;

        if (rapidez > velocidadMaxima)
            velocidadMaxima = rapidez;

        // Guardamos el punto mas alejado del origen del gesto.
        if (desplazamientoTotal.sqrMagnitude >
            desplazamientoMaximo.sqrMagnitude)
        {
            desplazamientoMaximo = desplazamientoTotal;
            tiempoPico = ahora;
        }

        // Detectamos si la mano se esta frenando.
        float umbralFrenado =
            velocidadMaxima * porcentajeFrenado;

        if (rapidez < umbralFrenado)
            contadorFrenado++;
        else
            contadorFrenado = 0;

        bool seHaFrenado =
            contadorFrenado >= framesFrenado;

        // Detectamos si comienza a invertir el movimiento.
        bool haCambiadoDireccion = false;

        if (desplazamientoMaximo.magnitude >= distanciaMinima)
        {
            Vector2 direccionGesto =
                desplazamientoMaximo.normalized;

            float velocidadProyectada =
                Vector2.Dot(velocidad, direccionGesto);

            haCambiadoDireccion =
                velocidadProyectada < -velocidadInicio;
        }

        // FASE 3: evaluar cuando termina el gesto.
        if (seHaFrenado || haCambiadoDireccion)
        {
            if (desplazamientoMaximo.magnitude >= distanciaMinima)
            {
                EvaluarGesto(
                    desplazamientoMaximo,
                    tiempoPico
                );
            }

            ReiniciarGesto();
        }

        posicionAnterior = posicionActual;
    }

    private void EvaluarGesto(
        Vector2 movimiento,
        double instanteGesto)
    {
        int direccionDetectada =
            DetectarDireccion(movimiento);

        if (direccionDetectada == -1)
            return;

        // Calculamos a que pulso musical se aproxima.
        double tiempoRelativo =
            instanteGesto - tiempoInicio;

        int pulsoMasCercano = Math.Max(
            1,
            (int)Math.Round(
                tiempoRelativo / duracionPulso,
                MidpointRounding.AwayFromZero
            )
        );

        double tiempoEsperado =
            pulsoMasCercano * duracionPulso;

        float error = (float)Math.Abs(
            tiempoRelativo - tiempoEsperado
        );

        // No evaluamos dos gestos en el mismo pulso.
        if (pulsoMasCercano <= ultimoPulsoEvaluado)
            return;

        // El movimiento debe coincidir con el pulso.
        if (error > toleranciaTiempo)
        {
            Debug.Log(
                $"GESTO FUERA DE RITMO | Error: {error:F3} s"
            );

            return;
        }

        // Si habia pulsos pendientes, perdemos el patron.
        if (pulsoMasCercano > ultimoPulsoEvaluado + 1)
        {
            if (patronSincronizado)
                PerderSincronizacion("Pulso perdido");
        }

        ultimoPulsoEvaluado = pulsoMasCercano;

        // Primera entrada o reincorporacion.
        if (!patronSincronizado)
        {
            patronSincronizado = true;

            siguienteDireccion =
                (direccionDetectada + 1) % 4;

            aciertosConsecutivos = 1;
            if (musicScoreManager != null)
            {
                musicScoreManager.CorrectRhythm(aciertosConsecutivos);
            }

            Debug.Log(
                $"ENTRADA: {nombres[direccionDetectada]} | " +
                $"Error: {error:F3} s | " +
                $"Siguiente: {nombres[siguienteDireccion]}"
            );

            return;
        }

        // Evaluacion de la secuencia.
        if (direccionDetectada == siguienteDireccion)
        {
            RegistrarAcierto(direccionDetectada, error);

            siguienteDireccion =
                (siguienteDireccion + 1) % 4;
        }
        else
        {
            RegistrarFallo(
                $"Direccion incorrecta: " +
                $"{nombres[direccionDetectada]}. " +
                $"Esperada: {nombres[siguienteDireccion]}"
            );
        }
    }

    private int DetectarDireccion(Vector2 movimiento)
    {
        if (movimiento.magnitude < distanciaMinima)
            return -1;

        Vector2 direccion = movimiento.normalized;

        // Entrada libre: detectamos cualquiera de las cuatro.
        if (!patronSincronizado)
            return DetectarDireccionLibre(direccion);

        // Si ya existe un patron, priorizamos la esperada.
        Vector2 esperada = direcciones[siguienteDireccion];

        float similitud =
            Vector2.Dot(direccion, esperada);

        float desplazamientoEje =
            Vector2.Dot(movimiento, esperada);

        float minimoEje =
            distanciaMinima * minimoEjeEsperado;

        if (similitud >= toleranciaDireccionEsperada &&
            desplazamientoEje >= minimoEje)
        {
            return siguienteDireccion;
        }

        // Si no coincide, intentamos reconocer otra direccion.
        return DetectarDireccionLibre(direccion);
    }

    private int DetectarDireccionLibre(Vector2 direccion)
    {
        float mejorSimilitud = precisionDireccion;
        int detectada = -1;

        for (int i = 0; i < direcciones.Length; i++)
        {
            float similitud =
                Vector2.Dot(direccion, direcciones[i]);

            if (similitud >= mejorSimilitud)
            {
                mejorSimilitud = similitud;
                detectada = i;
            }
        }

        return detectada;
    }

    private void ComprobarPulsosPerdidos(double ahora)
    {
        double tiempoTranscurrido = ahora - tiempoInicio;

        // Margen adicional para terminar de detectar el frenado.
        double margenTotal =
            toleranciaTiempo + margenDeteccion;

        int siguientePulso = ultimoPulsoEvaluado + 1;

        while (tiempoTranscurrido >
               siguientePulso * duracionPulso + margenTotal)
        {
            if (patronSincronizado)
            {
                PerderSincronizacion("Pulso perdido");
            }

            ultimoPulsoEvaluado++;
            siguientePulso++;
        }
    }

    private void PerderSincronizacion(string motivo)
    {
        patronSincronizado = false;
        siguienteDireccion = 0;
        aciertosConsecutivos = 0;

        Debug.Log(
            $"SINCRONIZACION PERDIDA: {motivo}. " +
            "El siguiente gesto sera una nueva entrada."
        );
    }

    private void RegistrarAcierto(int direccion, float error)
    {
        aciertosConsecutivos++;

        Debug.Log(
            $"CORRECTO: {nombres[direccion]} | " +
            $"Error: {error:F3} s | " +
            $"Racha: {aciertosConsecutivos}"
        );

        if (musicScoreManager != null)
        {
            musicScoreManager.CorrectRhythm(aciertosConsecutivos);
        }

        if (aciertosConsecutivos % 4 == 0)
        {
            Debug.Log("SECUENCIA PERFECTA DE 4 MOVIMIENTOS");
        }
    }

    private void RegistrarFallo(string motivo)
    {
        aciertosConsecutivos = 0;
        Debug.Log($"FALLO: {motivo}");
    }

    private void ReiniciarGesto()
    {
        gestoActivo = false;
        desplazamientoMaximo = Vector2.zero;
        velocidadMaxima = 0f;
        contadorFrenado = 0;
    }

    public void IniciarSeguimiento()
    {
        if (mano == null)
            return;

        duracionPulso = 60.0 / bpm;
        tiempoInicio = Time.realtimeSinceStartupAsDouble;

        ultimoPulsoEvaluado = 0;

        patronSincronizado = false;
        siguienteDireccion = 0;
        aciertosConsecutivos = 0;

        posicionAnterior = ObtenerPosicion();

        ReiniciarGesto();

        seguimientoActivo = true;

        Debug.Log(
            $"SEGUIMIENTO INICIADO | {bpm} BPM | 4/4"
        );
    }

    public void DetenerSeguimiento()
    {
        seguimientoActivo = false;
        patronSincronizado = false;
        aciertosConsecutivos = 0;

        ReiniciarGesto();
    }

    private Vector2 ObtenerPosicion()
    {
        return new Vector2(
            mano.localPosition.x,
            mano.localPosition.y
        );
    }
}
