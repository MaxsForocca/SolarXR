using UnityEngine;

/// <summary>
/// Fondo de estrellas para la camara que sigue a la nave. Emite particulas estaticas
/// sobre una esfera alrededor de la camara. Debe estar en una capa que la ARCamera no
/// renderiza (ver culling mask), para que no aparezca en la vista AR.
/// </summary>
[RequireComponent(typeof(ParticleSystem))]
public class Starfield : MonoBehaviour
{
    public int starCount = 600;
    public float radius = 3f;
    public Vector2 sizeRange = new Vector2(0.006f, 0.02f);
    [Tooltip("Las estrellas giran con este transform (el sistema solar) para que el fondo sea coherente al rotar la vista.")]
    public Transform orientationReference;
    public Transform followTarget;

    ParticleSystem ps;

    void Start()
    {
        ps = GetComponent<ParticleSystem>();
        var main = ps.main;
        main.loop = true; // sin emision: solo mantiene vivas las estrellas emitidas
        main.playOnAwake = false;
        main.startSpeed = 0f;
        main.startLifetime = 1e6f;
        main.maxParticles = starCount;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        var emission = ps.emission;
        emission.enabled = false;
        var shape = ps.shape;
        shape.enabled = false;

        ps.Play();
        var p = new ParticleSystem.EmitParams();
        for (int i = 0; i < starCount; i++)
        {
            p.position = Random.onUnitSphere * radius;
            p.startSize = Random.Range(sizeRange.x, sizeRange.y) * radius;
            float b = Random.Range(0.6f, 1f);
            float tint = Random.Range(-0.15f, 0.15f);
            p.startColor = new Color(b - tint * 0.5f, b, Mathf.Clamp01(b + tint), 1f);
            p.startLifetime = 1e6f;
            p.velocity = Vector3.zero;
            ps.Emit(p, 1);
        }
    }

    void LateUpdate()
    {
        if (followTarget != null) transform.position = followTarget.position;
        if (orientationReference != null) transform.rotation = orientationReference.rotation;
    }
}
