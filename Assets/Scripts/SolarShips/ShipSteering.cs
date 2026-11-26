using UnityEngine;

/// <summary>
/// Navegacion autonoma de una nave usando Steering Behaviours (Craig Reynolds):
///
///   fuerza = Seek/Arrive(waypoint) * wSeek
///          + Wander                * wWander
///          + ObstacleAvoidance     * wAvoid      (Sol y planetas, que se mueven)
///          + Separation            * wSeparation (otras naves)
///          + Containment           * wContain    (no salir del volumen del sistema)
///
///   aceleracion = Truncate(fuerza, maxForce)
///   velocidad   = Truncate(velocidad + aceleracion*dt, maxSpeed)
///   posicion   += velocidad*dt
///
/// Cuando la nave llega a su waypoint elige otro: un punto al azar del sistema o un
/// punto cerca de un planeta (asi "visita" planetas). La orientacion sigue a la
/// velocidad y se inclina (banking) segun el giro, como un avion.
/// Todo se calcula en espacio local del ShipFleet.
/// </summary>
public class ShipSteering : MonoBehaviour
{
    [Header("Movimiento")]
    public float maxSpeed = 0.045f;
    public float minSpeed = 0.015f;
    public float maxForce = 0.06f;
    [Tooltip("Distancia a la que empieza a frenar al llegar al waypoint.")]
    public float slowingRadius = 0.03f;
    [Tooltip("Distancia a la que se considera alcanzado el waypoint.")]
    public float arriveRadius = 0.015f;
    [Range(0f, 1f)] public float visitPlanetChance = 0.5f;

    [Header("Pesos de los comportamientos")]
    public float wSeek = 1.0f;
    public float wWander = 0.35f;
    public float wAvoid = 2.5f;
    public float wSeparation = 1.2f;
    public float wContain = 2.0f;

    [Header("Wander")]
    public float wanderDistance = 0.03f;
    public float wanderRadius = 0.015f;
    public float wanderJitter = 0.6f;

    [Header("Evasion")]
    [Tooltip("Segundos hacia adelante que mira para anticipar choques.")]
    public float lookAheadTime = 0.8f;
    public float separationRadius = 0.035f;

    [Header("Orientacion")]
    public float turnSmoothing = 6f;
    public float maxBank = 35f;
    [Tooltip("Corrige la orientacion del modelo si su 'frente' no es +Z.")]
    public Vector3 modelRotationOffset = Vector3.zero;

    public Vector3 Velocity => velocity;
    public Vector3 Waypoint => waypoint;

    ShipFleet fleet;
    Vector3 velocity;
    Vector3 waypoint;
    Vector3 wanderTarget;
    float bank;

    void Awake()
    {
        fleet = GetComponentInParent<ShipFleet>();
    }

    void OnEnable()
    {
        if (fleet == null) fleet = GetComponentInParent<ShipFleet>();
        if (fleet != null) fleet.Register(this);
    }

    void OnDisable()
    {
        if (fleet != null) fleet.Unregister(this);
    }

    void Start()
    {
        if (fleet == null)
        {
            Debug.LogError($"{name}: ShipSteering necesita un ShipFleet en algun padre.");
            enabled = false;
            return;
        }
        if (transform.localPosition.sqrMagnitude < 1e-6f)
            transform.localPosition = fleet.RandomPoint();

        velocity = Random.onUnitSphere;
        velocity.y *= 0.2f;
        velocity = velocity.normalized * Random.Range(minSpeed, maxSpeed);
        wanderTarget = Random.onUnitSphere * wanderRadius;
        PickWaypoint();
    }

    void Update()
    {
        float dt = Mathf.Min(Time.deltaTime, 0.05f);
        if (dt <= 0f) return;

        Vector3 pos = transform.localPosition;

        if ((waypoint - pos).sqrMagnitude < arriveRadius * arriveRadius)
            PickWaypoint();

        Vector3 force = Vector3.zero;
        force += Arrive(pos, waypoint) * wSeek;
        force += Wander(dt) * wWander;
        force += AvoidObstacles(pos) * wAvoid;
        force += Separation(pos) * wSeparation;
        force += Containment(pos) * wContain;
        force = Vector3.ClampMagnitude(force, maxForce);

        velocity += force * dt;
        float speed = velocity.magnitude;
        if (speed > maxSpeed) velocity = velocity / speed * maxSpeed;
        else if (speed < minSpeed) velocity = (speed > 1e-6f ? velocity / speed : transform.localRotation * Vector3.forward) * minSpeed;

        transform.localPosition = pos + velocity * dt;
        Orient(force, dt);
    }

    void PickWaypoint()
    {
        waypoint = Random.value < visitPlanetChance ? fleet.PointNearRandomPlanet() : fleet.RandomPoint();
    }

    // --- Comportamientos -------------------------------------------------------

    Vector3 Arrive(Vector3 pos, Vector3 target)
    {
        Vector3 toTarget = target - pos;
        float dist = toTarget.magnitude;
        if (dist < 1e-5f) return Vector3.zero;
        float speed = dist < slowingRadius ? Mathf.Max(minSpeed, maxSpeed * dist / slowingRadius) : maxSpeed;
        Vector3 desired = toTarget / dist * speed;
        return desired - velocity;
    }

    Vector3 Wander(float dt)
    {
        // Un punto que se mueve aleatoriamente sobre una esfera delante de la nave.
        wanderTarget += Random.insideUnitSphere * wanderJitter * wanderRadius * dt * 10f;
        wanderTarget = wanderTarget.normalized * wanderRadius;
        Vector3 forward = velocity.sqrMagnitude > 1e-8f ? velocity.normalized : Vector3.forward;
        Vector3 desired = forward * wanderDistance + wanderTarget;
        desired.y *= 0.3f; // preferir giros horizontales
        return desired.normalized * maxSpeed - velocity;
    }

    Vector3 AvoidObstacles(Vector3 pos)
    {
        Vector3 steer = Vector3.zero;
        float speed = velocity.magnitude;
        Vector3 dir = speed > 1e-6f ? velocity / speed : Vector3.forward;
        float lookAhead = speed * lookAheadTime;

        for (int i = 0; i < fleet.obstacles.Count; i++)
        {
            if (!fleet.GetObstacle(i, out Vector3 c, out float r)) continue;
            float safe = r + fleet.obstacleMargin;

            // 1) Repulsion si ya esta dentro de la zona de seguridad.
            Vector3 away = pos - c;
            float d = away.magnitude;
            if (d < safe * 1.5f && d > 1e-5f)
            {
                float k = 1f - Mathf.Clamp01((d - r) / (safe * 1.5f - r));
                steer += away / d * maxSpeed * k * 2f;
            }

            // 2) Anticipacion: proyecta el centro del obstaculo sobre la trayectoria.
            Vector3 toC = c - pos;
            float t = Vector3.Dot(toC, dir);
            if (t > 0f && t < lookAhead + safe)
            {
                Vector3 closest = pos + dir * t;
                Vector3 lateral = closest - c;
                float lat = lateral.magnitude;
                if (lat < safe)
                {
                    if (lat < 1e-5f) lateral = Vector3.Cross(dir, Vector3.up); // de frente: girar a un lado
                    float urgency = 1f - t / (lookAhead + safe);
                    steer += lateral.normalized * maxSpeed * (0.5f + urgency);
                }
            }
        }
        return steer;
    }

    Vector3 Separation(Vector3 pos)
    {
        Vector3 steer = Vector3.zero;
        var ships = fleet.Ships;
        for (int i = 0; i < ships.Count; i++)
        {
            var other = ships[i];
            if (other == this) continue;
            Vector3 away = pos - other.transform.localPosition;
            float d = away.magnitude;
            if (d < separationRadius && d > 1e-5f)
                steer += away / d * (1f - d / separationRadius) * maxSpeed;
        }
        return steer;
    }

    Vector3 Containment(Vector3 pos)
    {
        Vector3 steer = Vector3.zero;
        Vector3 flat = new Vector3(pos.x, 0f, pos.z);
        float r = flat.magnitude;
        if (r > fleet.maxRadius)
            steer -= flat / r * (r - fleet.maxRadius) / 0.02f * maxSpeed;
        if (r < fleet.minRadius && r > 1e-5f)
            steer += flat / r * (fleet.minRadius - r) / 0.02f * maxSpeed;

        if (pos.y < fleet.altitudeRange.x) steer.y += (fleet.altitudeRange.x - pos.y) / 0.01f * maxSpeed;
        if (pos.y > fleet.altitudeRange.y) steer.y -= (pos.y - fleet.altitudeRange.y) / 0.01f * maxSpeed;
        return steer;
    }

    // --- Orientacion -----------------------------------------------------------

    void Orient(Vector3 force, float dt)
    {
        if (velocity.sqrMagnitude < 1e-10f) return;
        Vector3 fwd = velocity.normalized;

        // Banking: inclinar segun la fuerza lateral (giro).
        Vector3 right = Vector3.Cross(Vector3.up, fwd).normalized;
        float lateral = Vector3.Dot(force, right) / Mathf.Max(maxForce, 1e-5f);
        bank = Mathf.Lerp(bank, -lateral * maxBank, 1f - Mathf.Exp(-3f * dt));

        Quaternion target = Quaternion.LookRotation(fwd, Vector3.up)
                          * Quaternion.Euler(0f, 0f, bank)
                          * Quaternion.Euler(modelRotationOffset);
        transform.localRotation = Quaternion.Slerp(transform.localRotation, target, 1f - Mathf.Exp(-turnSmoothing * dt));
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (fleet == null) return;
        Gizmos.matrix = fleet.transform.localToWorldMatrix;
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(transform.localPosition, waypoint);
        Gizmos.DrawWireSphere(waypoint, arriveRadius);
        Gizmos.color = Color.green;
        Gizmos.DrawLine(transform.localPosition, transform.localPosition + velocity);
    }
#endif
}
