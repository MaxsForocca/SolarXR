using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Contenedor de las naves. Define el "espacio" donde navegan (su propio transform,
/// hijo del sistema solar) y la lista de obstaculos (Sol y planetas) que las naves
/// deben esquivar. Las naves se registran solas en OnEnable.
///
/// Todo el calculo de steering se hace en el espacio local de este transform, asi el
/// comportamiento no depende de donde Vuforia coloque el ImageTarget en el mundo.
/// </summary>
public class ShipFleet : MonoBehaviour
{
    [Header("Volumen de navegacion (espacio local)")]
    [Tooltip("Las naves no se alejan del Sol mas de este radio.")]
    public float maxRadius = 0.30f;
    [Tooltip("Ni se acercan mas que esto al centro.")]
    public float minRadius = 0.05f;
    [Tooltip("Altura minima / maxima sobre el plano de las orbitas.")]
    public Vector2 altitudeRange = new Vector2(0.012f, 0.07f);

    [Header("Obstaculos (Sol y planetas)")]
    public List<Transform> obstacles = new List<Transform>();
    [Tooltip("Margen extra alrededor de cada obstaculo.")]
    public float obstacleMargin = 0.015f;

    readonly List<ShipSteering> ships = new List<ShipSteering>();
    public IReadOnlyList<ShipSteering> Ships => ships;

    public void Register(ShipSteering s)
    {
        if (!ships.Contains(s)) ships.Add(s);
        ships.Sort((a, b) => a.transform.GetSiblingIndex().CompareTo(b.transform.GetSiblingIndex()));
    }

    public void Unregister(ShipSteering s) => ships.Remove(s);

    /// <summary>Posicion y radio de un obstaculo en el espacio local de la flota.</summary>
    public bool GetObstacle(int i, out Vector3 localPos, out float localRadius)
    {
        var t = obstacles[i];
        if (t == null || !t.gameObject.activeInHierarchy)
        {
            localPos = Vector3.zero; localRadius = 0f;
            return false;
        }
        localPos = transform.InverseTransformPoint(t.position);
        // Esfera de Unity: radio 0.5 * escala.
        float worldRadius = 0.5f * Mathf.Max(t.lossyScale.x, t.lossyScale.y, t.lossyScale.z);
        localRadius = worldRadius / Mathf.Max(transform.lossyScale.x, 1e-5f);
        return true;
    }

    /// <summary>Punto aleatorio dentro del volumen de navegacion.</summary>
    public Vector3 RandomPoint()
    {
        float a = Random.value * Mathf.PI * 2f;
        // sqrt para distribucion uniforme en el anillo
        float r = Mathf.Sqrt(Mathf.Lerp(minRadius * minRadius, maxRadius * maxRadius, Random.value));
        float y = Random.Range(altitudeRange.x, altitudeRange.y);
        return new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r);
    }

    /// <summary>Punto cercano a un planeta al azar (para "visitar" planetas).</summary>
    public Vector3 PointNearRandomPlanet()
    {
        if (obstacles.Count == 0) return RandomPoint();
        // Indice 0 es el Sol: se excluye si hay mas obstaculos.
        int i = obstacles.Count > 1 ? Random.Range(1, obstacles.Count) : 0;
        if (!GetObstacle(i, out var p, out var r)) return RandomPoint();
        Vector3 offset = Random.onUnitSphere;
        offset.y = Mathf.Abs(offset.y) + 0.3f;
        p += offset.normalized * (r + obstacleMargin * 2f);
        p.y = Mathf.Clamp(p.y, altitudeRange.x, altitudeRange.y);
        return p;
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = new Color(0f, 1f, 1f, 0.4f);
        DrawCircle(maxRadius, altitudeRange.x);
        DrawCircle(maxRadius, altitudeRange.y);
        DrawCircle(minRadius, altitudeRange.x);
    }

    static void DrawCircle(float r, float y)
    {
        Vector3 prev = new Vector3(r, y, 0f);
        for (int i = 1; i <= 48; i++)
        {
            float a = i / 48f * Mathf.PI * 2f;
            Vector3 p = new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r);
            Gizmos.DrawLine(prev, p);
            prev = p;
        }
    }
#endif
}
