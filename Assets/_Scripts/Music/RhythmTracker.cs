
using UnityEngine;

public class RhythmTracker : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private BatutaController batutaController;
    [SerializeField] private Transform mano;
    [SerializeField] private MusicClockFMOD musicClockFMOD;

    // Los BPM se obtienen del OrchestraAudioManager de esta escena.
    private float bpm = 100f;

    [SerializeField, Range(0.05f, 0.3f)]
    private float toleranciaTiempo = 0.18f;
    [Header("Puntuacion musical")]
    [SerializeField] private MusicScoreManager musicScoreManager;

    [Tooltip("Desviacion relativa respecto al BPM ideal a partir de la cual la calidad sera 0. Por ejemplo, 0.5 significa un 50%.")]
    [SerializeField, Range(0.05f, 2f)]
    private float desviacionRelativaCalidadCero = 0.5f;

    [Tooltip("Fraccion del intervalo entre pulsos a la que la calidad de sincronizacion sera 0. 0.5 equivale a estar justo a mitad entre dos pulsos.")]
    [SerializeField, Range(0.05f, 0.5f)]
    private float desfaseRelativoCalidadCero = 0.5f;

    [Tooltip("Margen en segundos respecto al pulso de FMOD con calidad de sincronizacion perfecta. 0.04 son 40 ms.")]
    [SerializeField, Range(0f, 0.1f)]
    private float margenPrecisionPerfecta = 0.04f;

    [Header("Correccion por racha")]
    [Tooltip("Parte del cambio hacia el ritmo medido que se aplica sin racha (0 a 1).")]
    [SerializeField, Range(0.05f, 1f)]
    private float factorCorreccionBase = 0.35f;

    [Tooltip("Aumento de la correccion por cada movimiento acertado consecutivamente.")]
    [SerializeField, Range(0f, 0.2f)]
    private float bonusCorreccionRacha = 0.06f;

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

    [Tooltip("Segundos entre el comienzo del evento FMOD y su primer pulso. Normalmente 0.")]
    [SerializeField] private float desfaseInicialMetronomo = 0f;

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
    private double duracionPulso;

    // Medicion de BPM: cuatro movimientos por actualizacion.
    private double ultimoInstanteGesto = -1d;
    private int movimientosEnBloque;
    private int intervalosEnBloque;
    private double sumaIntervalos;
    private float sumaCalidadFase;
    private int muestrasFase;
    private bool avisoRelojMostrado;

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
        {
           
            return;
        }
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

        // Comprobamos si lleva demasiado tiempo sin marcar.
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

        // Evitamos contar varias veces una misma marca por oscilaciones pequenas.
        if (ultimoInstanteGesto >= 0d &&
            instanteGesto - ultimoInstanteGesto < duracionPulso * 0.25d)
            return;

        // Medimos el desfase respecto al reloj REAL de FMOD, no respecto
        // al momento en el que se activo el modo Director.
        bool hayTiempoMusical = ObtenerCalidadFase(
            instanteGesto,
            out float errorFase,
            out float calidadFase
        );

        // Primera entrada o reincorporacion: cualquier direccion es valida.
        if (!patronSincronizado)
        {
            patronSincronizado = true;

            siguienteDireccion =
                (direccionDetectada + 1) % 4;

            aciertosConsecutivos = hayTiempoMusical &&
                errorFase <= toleranciaTiempo ? 1 : 0;

            Debug.Log(
                $"ENTRADA: {nombres[direccionDetectada]} | " +
                $"Desfase: {(hayTiempoMusical ? errorFase.ToString("F3") : "sin reloj")} s | " +
                $"Siguiente: {nombres[siguienteDireccion]}"
            );
        }
        else
        {
            // Mantenemos el patron de cuatro direcciones.
            if (direccionDetectada != siguienteDireccion)
            {
                RegistrarFallo(
                    $"Direccion incorrecta: " +
                    $"{nombres[direccionDetectada]}. " +
                    $"Esperada: {nombres[siguienteDireccion]}"
                );
                return;
            }

            siguienteDireccion =
                (siguienteDireccion + 1) % 4;

            // Para mantener una racha hacen falta TANTO el intervalo correcto
            // como coincidir con el pulso del metronomo de FMOD.
            if (ultimoInstanteGesto >= 0d)
            {
                float intervalo =
                    (float)(instanteGesto - ultimoInstanteGesto);

                float errorIntervalo =
                    Mathf.Abs(intervalo - (float)duracionPulso);

                if (errorIntervalo <= toleranciaTiempo &&
                    hayTiempoMusical && errorFase <= toleranciaTiempo)
                {
                    RegistrarAcierto(direccionDetectada, errorIntervalo);
                }
                else
                {
                    aciertosConsecutivos = 0;

                    Debug.Log(
                        $"GESTO FUERA DE RITMO: {nombres[direccionDetectada]} | " +
                        $"Error intervalo: {errorIntervalo:F3} s | " +
                        $"Desfase metronomo: {(hayTiempoMusical ? errorFase.ToString("F3") : "sin reloj")} s"
                    );
                }
            }
            else
            {
                aciertosConsecutivos = hayTiempoMusical &&
                    errorFase <= toleranciaTiempo ? 1 : 0;
            }
        }

        // Aunque un gesto este adelantado o retrasado, medimos su tempo real.
        RegistrarMovimiento(instanteGesto, hayTiempoMusical, calidadFase);
    }

    private bool ObtenerCalidadFase(
        double instanteGesto,
        out float errorFase,
        out float calidadFase)
    {
        errorFase = 0f;
        calidadFase = 0f;

        if (musicClockFMOD == null)
            musicClockFMOD = MusicClockFMOD.Instance;

        if (musicClockFMOD == null ||
            !musicClockFMOD.TryObtenerTiempo(out double tiempoMusical))
        {
            if (!avisoRelojMostrado)
            {
                Debug.LogWarning(
                    "RhythmTracker: no hay tiempo musical disponible en FMOD. " +
                    "No se enviara calidad hasta que el reloj este reproduciendo."
                );
                avisoRelojMostrado = true;
            }

            return false;
        }

        avisoRelojMostrado = false;

        // La deteccion sucede al frenar, pero tiempoPico representa el instante
        // del gesto: retrocedemos el reloj musical ese pequeno retraso.
        double retrasoDeteccion =
            Time.realtimeSinceStartupAsDouble - instanteGesto;

        double tiempoEnElGesto = tiempoMusical - retrasoDeteccion;

        // Distancia temporal al pulso musical MAS CERCANO. La fase se obtiene
        // directamente de la linea de tiempo del evento FMOD.
        double fase =
            (tiempoEnElGesto - desfaseInicialMetronomo) % duracionPulso;

        if (fase < 0d)
            fase += duracionPulso;

        double diferencia = MathMin(fase, duracionPulso - fase);
        errorFase = (float)diferencia;

        // Un desfase de hasta 40 ms no penaliza. Fuera de ese margen,
        // la calidad desciende gradualmente hasta 0 en el limite configurado.
        float limiteCalidadCero = (float)duracionPulso * desfaseRelativoCalidadCero;
        float margenPerfectoAplicado = Mathf.Min(margenPrecisionPerfecta, limiteCalidadCero - 0.0001f);

        calidadFase = Mathf.Clamp01(
            1f - Mathf.Max(0f, errorFase - margenPerfectoAplicado) /
            (limiteCalidadCero - margenPerfectoAplicado)
        );

        return true;
    }

    private static double MathMin(double a, double b)
    {
        return a < b ? a : b;
    }

    private void RegistrarMovimiento(
        double instanteGesto,
        bool hayTiempoMusical,
        float calidadFase)
    {
        // El primer bloque contiene 3 intervalos (4 marcas).
        // Los siguientes contienen 4, aprovechando el ultimo gesto anterior.
        if (ultimoInstanteGesto >= 0d)
        {
            sumaIntervalos += instanteGesto - ultimoInstanteGesto;
            intervalosEnBloque++;
        }

        ultimoInstanteGesto = instanteGesto;
        movimientosEnBloque++;

        if (hayTiempoMusical)
        {
            sumaCalidadFase += calidadFase;
            muestrasFase++;
        }

        if (movimientosEnBloque < 4)
            return;

        // No premiamos un ritmo sin haber podido comprobar su sincronizacion.
        if (intervalosEnBloque > 0 && sumaIntervalos > 0d &&
            muestrasFase == 4)
        {
            float bpmJugador =
                (float)(60d * intervalosEnBloque / sumaIntervalos);

            float calidadFaseMedia = sumaCalidadFase / muestrasFase;

            EnviarRitmoAlScore(bpmJugador, calidadFaseMedia);
        }

        // Comenzamos un bloque nuevo, manteniendo el ultimo gesto como referencia.
        movimientosEnBloque = 0;
        intervalosEnBloque = 0;
        sumaIntervalos = 0d;
        sumaCalidadFase = 0f;
        muestrasFase = 0;
    }

    private void EnviarRitmoAlScore(float bpmJugador, float calidadFaseMedia)
    {
        // El tempo ideal siempre equivale a 100, independientemente del BPM musical.
        float ritmoNormalizado =
            Mathf.Clamp(100f * bpmJugador / bpm, 0f, 200f);

        // Calidad de tempo: diferencia entre los BPM tocados y los de la cancion.
        float errorRelativo =
            Mathf.Abs(bpmJugador - bpm) / bpm;

        float calidadTempo = Mathf.Clamp01(
            1f - errorRelativo / desviacionRelativaCalidadCero
        );

        // Calidad final: penaliza tanto la velocidad como el desfase con FMOD.
        // Si el jugador toca a BPM perfectos pero a contratiempo, se penaliza.
        float calidadNormalizada =
            Mathf.Clamp01(calidadTempo * calidadFaseMedia);

        if (musicScoreManager != null)
        {
            // Una racha alta se acerca mas al nuevo ritmo medido en cada envio.
            // El Lerp suave definitivo sigue haciendose en MusicScoreManager.
            float factorCorreccion = Mathf.Clamp01(
                factorCorreccionBase +
                Mathf.Max(0, aciertosConsecutivos - 1) * bonusCorreccionRacha
            );

            float ritmoEnviado = Mathf.Lerp(
                musicScoreManager.CurrentRhythm,
                ritmoNormalizado,
                factorCorreccion
            );

            musicScoreManager.ReceiveDirectorInput(
                ritmoEnviado,
                calidadNormalizada
            );

            Debug.Log(
                $"BPM MEDIDOS: {bpmJugador:F1} / {bpm:F1} | " +
                $"Ritmo normalizado: {ritmoNormalizado:F1} | " +
                $"Ritmo enviado: {ritmoEnviado:F1} | " +
                $"Calidad BPM: {calidadTempo:F2} | " +
                $"Calidad metronomo: {calidadFaseMedia:F2} | " +
                $"Calidad final: {calidadNormalizada:F2} | " +
                $"Racha: {aciertosConsecutivos}"
            );
        }
        else
        {
            Debug.LogWarning(
                $"BPM MEDIDOS: {bpmJugador:F1} | " +
                "Falta asignar MusicScoreManager en RhythmTracker."
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
        if (!patronSincronizado || ultimoInstanteGesto < 0d)
            return;

        // No cortamos la medicion por ir mas lento que el tempo ideal.
        // Solo perdemos el patron tras una pausa prolongada (4 pulsos + margen).
        double maxTiempoSinMarcas =
            duracionPulso * 4d + margenDeteccion;

        if (ahora - ultimoInstanteGesto > maxTiempoSinMarcas)
            PerderSincronizacion("Demasiado tiempo sin marcar");
    }

    private void PerderSincronizacion(string motivo)
    {
        patronSincronizado = false;
        siguienteDireccion = 0;
        aciertosConsecutivos = 0;
        ReiniciarMedicion();

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

        if (aciertosConsecutivos % 4 == 0)
        {
            Debug.Log("SECUENCIA PERFECTA DE 4 MOVIMIENTOS");
        }
    }

    private void RegistrarFallo(string motivo)
    {
        // Un gesto en otra direccion corta la secuencia actual.
        patronSincronizado = false;
        siguienteDireccion = 0;
        aciertosConsecutivos = 0;
        ReiniciarMedicion();

        Debug.Log($"FALLO: {motivo}. El siguiente gesto sera una nueva entrada.");
    }

    private void ReiniciarMedicion()
    {
        ultimoInstanteGesto = -1d;
        movimientosEnBloque = 0;
        intervalosEnBloque = 0;
        sumaIntervalos = 0d;
        sumaCalidadFase = 0f;
        muestrasFase = 0;
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

        // Cada nivel configura sus BPM en el prefab OrchestraAudioManager.
        OrchestraAudioManager orchestra = OrchestraAudioManager.Instance;
        if (orchestra == null)
        {
            Debug.LogError("RhythmTracker: no se encontro OrchestraAudioManager para obtener los BPM de la cancion.");
            return;
        }

        bpm = orchestra.SongBpm;
        duracionPulso = 60.0 / bpm;

        if (musicClockFMOD == null)
            musicClockFMOD = MusicClockFMOD.Instance;

        avisoRelojMostrado = false;

        patronSincronizado = false;
        siguienteDireccion = 0;
        aciertosConsecutivos = 0;
        ReiniciarMedicion();

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
        ReiniciarMedicion();

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
