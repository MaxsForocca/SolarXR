using UnityEngine;

/// <summary>
/// Movimiento orbital simple (circular, plano XZ) alrededor del origen del padre
/// (el Sol esta en el origen de "SolarSystem") + rotacion sobre su propio eje.
/// Todo se calcula en espacio local para que funcione sobre el ImageTarget.
/// </summary>
public class PlanetOrbit : MonoBehaviour
{
    [Tooltip("Radio de la orbita en unidades locales del sistema (metros sobre el target).")]
    public float orbitRadius = 0.1f;
    [Tooltip("Segundos que tarda en dar una vuelta al Sol.")]
    public float orbitPeriod = 15f;
    [Tooltip("Angulo inicial en grados, para que no arranquen todos alineados.")]
    public float startAngle = 0f;
    [Tooltip("Grados por segundo de rotacion sobre su eje.")]
    public float spinSpeed = 30f;
    [Tooltip("Inclinacion del eje del planeta (grados).")]
    public float axialTilt = 0f;

    /// <summary>Multiplicador global de velocidad (lo modifica SolarSystemController).</summary>
    public static float TimeScale = 1f;

    float angle;
    float spin;

    void Start()
    {
        angle = startAngle * Mathf.Deg2Rad;
        Apply();
    }

    void Update()
    {
        float dt = Time.deltaTime * TimeScale;
        if (orbitPeriod > 0.01f) angle += dt * Mathf.PI * 2f / orbitPeriod;
        spin += spinSpeed * dt;
        Apply();
    }

    void Apply()
    {
        transform.localPosition = new Vector3(Mathf.Cos(angle) * orbitRadius, 0f, Mathf.Sin(angle) * orbitRadius);
        transform.localRotation = Quaternion.Euler(0f, 0f, axialTilt) * Quaternion.Euler(0f, spin, 0f);
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (!Application.isPlaying)
        {
            angle = startAngle * Mathf.Deg2Rad;
            Apply();
        }
    }
#endif
}
