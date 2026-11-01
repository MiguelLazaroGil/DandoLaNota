
using System;
using UnityEngine;
using UnityEngine.Audio;

public class BatonGhostHit : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private BatutaController batutaController;
    [SerializeField] private Transform manoBatuta;
    [SerializeField] private Transform puntaBatuta;
    [SerializeField] private FantasmasIA fantasmasIA;

    [Header("Deteccion de golpe")]
    [SerializeField, Min(0.001f)]
    private float radioGolpe = 0.05f;

    [SerializeField] private LayerMask capasImpacto;

    [SerializeField, Min(0.001f)]
    private float velocidadMinima = 0.04f;

    [SerializeField, Min(0.0001f)]
    private float desplazamientoMinimo = 0.002f;

    [SerializeField, Min(0f)]
    private float cooldownGolpe = 0.4f;

    [SerializeField] private bool corregirDirectamente = true;
    [SerializeField] private AudioClip sonidoGolpe;

    public event Action<GhostCorrectionTarget> GolpeDetectado;

    private Vector2 manoAnterior;
    private Vector2 inicioGesto;
    private Vector3 puntaAnterior;
    private bool dirigiendoAnterior;
    private float siguienteGolpe;

    private void Awake()
    {
        if (fantasmasIA == null)
            fantasmasIA = FindFirstObjectByType<FantasmasIA>();
    }

    private void OnEnable()
    {
        ReiniciarDeteccion();
    }

    private void ReiniciarDeteccion()
    {
        if (manoBatuta != null)
        {
            manoAnterior = new Vector2(
                manoBatuta.localPosition.x,
                manoBatuta.localPosition.y
            );

            inicioGesto = manoAnterior;
        }

        if (puntaBatuta != null)
            puntaAnterior = puntaBatuta.position;

        dirigiendoAnterior = false;
    }

    private void LateUpdate()
    {
        if (batutaController == null ||
            manoBatuta == null ||
            puntaBatuta == null)
            return;

        if (Time.timeScale == 0f)
        {
            ReiniciarDeteccion();
            return;
        }

        Vector2 manoActual = new Vector2(
            manoBatuta.localPosition.x,
            manoBatuta.localPosition.y
        );

        Vector3 puntaActual = puntaBatuta.position;

        bool dirigiendo =
            batutaController.modoDirectorActivo &&
            batutaController.BotonPulsado;

        if (!dirigiendo || !dirigiendoAnterior)
        {
            dirigiendoAnterior = dirigiendo;
            manoAnterior = manoActual;
            inicioGesto = manoActual;
            puntaAnterior = puntaActual;
            return;
        }

        float dt = Time.unscaledDeltaTime;
        if (dt <= 0f) return;

        // El movimiento de la mano determina si hay golpe.
        float velocidad =
            (manoActual - manoAnterior).magnitude / dt;

        float desplazamiento =
            Vector2.Distance(manoActual, inicioGesto);

        // El movimiento real de la punta determina qué tocamos.
        Vector3 origenPunta = puntaAnterior;

        manoAnterior = manoActual;
        puntaAnterior = puntaActual;

        if (velocidad < velocidadMinima ||
            desplazamiento < desplazamientoMinimo ||
            Time.time < siguienteGolpe)
            return;

        GhostCorrectionTarget objetivo =
    BuscarContacto(origenPunta, puntaActual, out AudioSource audioGolpe);

        if (objetivo == null)
            return;

        inicioGesto = manoActual;
        siguienteGolpe = Time.time + cooldownGolpe;

        // Reproducir el sonido del músico golpeado.
        if (audioGolpe != null && sonidoGolpe != null)
            audioGolpe.PlayOneShot(sonidoGolpe);

        GolpeDetectado?.Invoke(objetivo);

        if (corregirDirectamente && fantasmasIA != null)
            fantasmasIA.CorregirMusico(objetivo);
    }

    private GhostCorrectionTarget BuscarContacto(
    Vector3 origen,
    Vector3 destino,
    out AudioSource audioGolpe)
    {
        audioGolpe = null;

        Vector3 movimiento = destino - origen;
        float distancia = movimiento.magnitude;

        if (distancia > 0.00001f)
        {
            RaycastHit[] impactos = Physics.SphereCastAll(
                origen,
                radioGolpe,
                movimiento.normalized,
                distancia,
                capasImpacto,
                QueryTriggerInteraction.Collide
            );

            foreach (RaycastHit hit in impactos)
            {
                if (!hit.collider.CompareTag("MusicoGolpeable"))
                    continue;

                GhostCorrectionTarget objetivo =
                    hit.collider.GetComponentInParent<GhostCorrectionTarget>();

                if (objetivo != null && objetivo.TieneFantasma)
                {
                    audioGolpe = hit.collider.GetComponentInParent<AudioSource>();
                    return objetivo;
                }
            }
        }

        Collider[] contactos = Physics.OverlapSphere(
            destino,
            radioGolpe,
            capasImpacto,
            QueryTriggerInteraction.Collide
        );

        foreach (Collider contacto in contactos)
        {
            if (!contacto.CompareTag("MusicoGolpeable"))
                continue;

            GhostCorrectionTarget objetivo =
                contacto.GetComponentInParent<GhostCorrectionTarget>();

            if (objetivo != null && objetivo.TieneFantasma)
            {
                audioGolpe = contacto.GetComponentInParent<AudioSource>();
                return objetivo;
            }
        }

        return null;
    }
    private void OnDrawGizmosSelected()
    {
        if (puntaBatuta == null) return;

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(
            puntaBatuta.position,
            radioGolpe
        );
    }
}
