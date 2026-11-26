using UnityEngine;

/// <summary>
/// Dibuja un circulo con un LineRenderer en el plano XZ local (se usa para las orbitas
/// y para el marcador de la nave seleccionada). Usa espacio local para que el circulo
/// siga al ImageTarget cuando Vuforia lo mueve.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(LineRenderer))]
public class CircleLine : MonoBehaviour
{
    [Min(0.0001f)] public float radius = 0.1f;
    [Range(8, 256)] public int segments = 96;
    [Min(0.0001f)] public float width = 0.0007f;
    public Color color = new Color(1f, 1f, 1f, 0.35f);

    LineRenderer line;

    void OnEnable() => Rebuild();
    void OnValidate() => Rebuild();

    public void Rebuild()
    {
        if (line == null) line = GetComponent<LineRenderer>();
        if (line == null) return;

        line.useWorldSpace = false;
        line.loop = true;
        line.positionCount = segments;
        line.startWidth = line.endWidth = width;
        line.startColor = line.endColor = color;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.alignment = LineAlignment.View;

        for (int i = 0; i < segments; i++)
        {
            float a = (float)i / segments * Mathf.PI * 2f;
            line.SetPosition(i, new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius));
        }
    }
}
