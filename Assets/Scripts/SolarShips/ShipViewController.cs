using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

/// <summary>
/// Controla la "camara de la escena" (una Camera virtual, distinta de la ARCamera de
/// Vuforia) que puede seguir a una nave:
///
///  - Modo AR: se ve la camara del dispositivo con el sistema solar sobre la imagen.
///    Un anillo (selectionMarker) indica la nave seleccionada.
///  - Modo Seguir: la followCamera (depth mayor) cubre la pantalla y orbita alrededor
///    de la nave seleccionada. Arrastrar con un dedo (o mouse) gira la vista,
///    pellizcar (o rueda del mouse) acerca/aleja.
///
/// Los botones de la UI llaman a PreviousShip / NextShip / ToggleFollow.
/// </summary>
public class ShipViewController : MonoBehaviour
{
    [Header("Referencias")]
    public ShipFleet fleet;
    public Camera followCamera;
    [Tooltip("Raiz del contenido aumentado (SolarSystem, hijo del ImageTarget).")]
    public Transform contentRoot;
    public DefaultObserverEventHandler observerHandler;
    public Transform selectionMarker;

    [Header("UI")]
    public TMP_Text shipLabel;
    public TMP_Text followButtonLabel;
    public GameObject followHint;
    public GameObject scanHint;
    public string followText = "Seguir nave";
    public string arText = "Vista AR";

    [Header("Orbita de la camara (en unidades locales del sistema; se escalan con el)")]
    public float distance = 0.07f;
    public float minDistance = 0.03f;
    public float maxDistance = 0.3f;
    public float yaw = 0f;
    public float pitch = 15f;
    public Vector2 pitchLimits = new Vector2(-70f, 80f);
    [Tooltip("Grados que gira al arrastrar toda la altura de la pantalla.")]
    public float dragSensitivity = 240f;
    public float followSmoothing = 10f;
    [Tooltip("Si es true la camara se mantiene detras de la nave cuando esta gira.")]
    public bool alignWithShipHeading = true;
    [Tooltip("Arranca directamente en modo Seguir (util para probar en el editor sin imagen).")]
    public bool followOnStart = false;

    public bool IsFollowing { get; private set; }
    public ShipSteering CurrentShip =>
        fleet != null && fleet.Ships.Count > 0 ? fleet.Ships[Mathf.Clamp(index, 0, fleet.Ships.Count - 1)] : null;

    int index;
    bool targetVisible;
    bool dragging;
    Renderer[] trackedRenderers;
    float smoothedHeading;
    readonly List<RaycastResult> uiHits = new List<RaycastResult>();

    void OnEnable() => EnhancedTouchSupport.Enable();
    void OnDisable() => EnhancedTouchSupport.Disable();

    void Start()
    {
        if (followCamera != null) followCamera.enabled = false;
        if (contentRoot != null) trackedRenderers = contentRoot.GetComponentsInChildren<Renderer>(true);
        if (observerHandler != null)
        {
            observerHandler.OnTargetFound.AddListener(() => { targetVisible = true; RefreshUI(); });
            observerHandler.OnTargetLost.AddListener(() => { targetVisible = false; RefreshUI(); });
        }
        RefreshUI();
        if (followOnStart) SetFollow(true);
    }

    // --- API para los botones -------------------------------------------------

    public void NextShip() => Select(index + 1);
    public void PreviousShip() => Select(index - 1);
    public void ToggleFollow() => SetFollow(!IsFollowing);

    public void Select(int i)
    {
        int n = fleet != null ? fleet.Ships.Count : 0;
        if (n == 0) return;
        index = ((i % n) + n) % n;
        RefreshUI();
    }

    public void SetFollow(bool follow)
    {
        IsFollowing = follow && CurrentShip != null;
        if (followCamera != null) followCamera.enabled = IsFollowing;
        if (IsFollowing)
        {
            var ship = CurrentShip;
            smoothedHeading = Heading(ship);
            SnapCamera();
        }
        else
        {
            // Volver a lo que Vuforia decida segun el estado del tracking.
            SetTrackedRenderers(targetVisible);
        }
        RefreshUI();
    }

    // --- Update ---------------------------------------------------------------

    void Update()
    {
        if (!IsFollowing) return;
        HandleInput();
    }

    void LateUpdate()
    {
        var ship = CurrentShip;

        if (selectionMarker != null)
        {
            selectionMarker.gameObject.SetActive(ship != null && !IsFollowing);
            if (ship != null) selectionMarker.localPosition = ship.transform.localPosition;
        }

        if (!IsFollowing || ship == null) return;

        // Si se pierde la imagen, Vuforia apaga los renderers: en modo seguir los
        // mantenemos encendidos para poder seguir viendo la nave.
        SetTrackedRenderers(true);

        float k = 1f - Mathf.Exp(-followSmoothing * Time.deltaTime);
        smoothedHeading = Mathf.LerpAngle(smoothedHeading, Heading(ship), k);
        Pose p = ComputePose(ship);
        var t = followCamera.transform;
        t.SetPositionAndRotation(Vector3.Lerp(t.position, p.position, k), Quaternion.Slerp(t.rotation, p.rotation, k));
    }

    void SnapCamera()
    {
        var ship = CurrentShip;
        if (ship == null || followCamera == null) return;
        Pose p = ComputePose(ship);
        followCamera.transform.SetPositionAndRotation(p.position, p.rotation);
    }

    Pose ComputePose(ShipSteering ship)
    {
        Transform space = fleet.transform;
        Vector3 up = space.up;
        Quaternion frame = space.rotation * Quaternion.Euler(0f, alignWithShipHeading ? smoothedHeading : 0f, 0f);
        Quaternion orbit = frame * Quaternion.Euler(pitch, yaw, 0f);
        Vector3 target = ship.transform.position;
        float worldDistance = distance * Mathf.Max(space.lossyScale.x, 1e-4f);
        Vector3 pos = target - orbit * Vector3.forward * worldDistance;
        return new Pose(pos, Quaternion.LookRotation(target - pos, up));
    }

    float Heading(ShipSteering ship)
    {
        Vector3 v = ship.Velocity;
        if (v.x * v.x + v.z * v.z < 1e-10f) return smoothedHeading;
        return Mathf.Atan2(v.x, v.z) * Mathf.Rad2Deg;
    }

    // --- Input (touch + mouse para el editor) ---------------------------------

    void HandleInput()
    {
        var touches = Touch.activeTouches;
        if (touches.Count == 1)
        {
            var t = touches[0];
            if (t.phase == TouchPhase.Began) dragging = !IsOverUI(t.screenPosition);
            if (dragging && t.phase == TouchPhase.Moved) Rotate(t.delta);
            if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled) dragging = false;
        }
        else if (touches.Count >= 2)
        {
            dragging = false;
            var a = touches[0];
            var b = touches[1];
            float cur = Vector2.Distance(a.screenPosition, b.screenPosition);
            float prev = Vector2.Distance(a.screenPosition - a.delta, b.screenPosition - b.delta);
            if (cur > 1f && prev > 1f) Zoom(prev / cur);
        }
        else if (Mouse.current != null)
        {
            var m = Mouse.current;
            if (m.leftButton.wasPressedThisFrame) dragging = !IsOverUI(m.position.ReadValue());
            if (m.leftButton.wasReleasedThisFrame) dragging = false;
            if (dragging && m.leftButton.isPressed) Rotate(m.delta.ReadValue());
            float scroll = m.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.01f) Zoom(scroll > 0 ? 0.9f : 1.1f);
        }
    }

    void Rotate(Vector2 deltaPixels)
    {
        float scale = dragSensitivity / Mathf.Max(Screen.height, 1);
        yaw += deltaPixels.x * scale;
        pitch = Mathf.Clamp(pitch - deltaPixels.y * scale, pitchLimits.x, pitchLimits.y);
    }

    void Zoom(float factor)
    {
        distance = Mathf.Clamp(distance * factor, minDistance, maxDistance);
    }

    bool IsOverUI(Vector2 screenPos)
    {
        if (EventSystem.current == null) return false;
        var data = new PointerEventData(EventSystem.current) { position = screenPos };
        uiHits.Clear();
        EventSystem.current.RaycastAll(data, uiHits);
        return uiHits.Count > 0;
    }

    // --- Helpers --------------------------------------------------------------

    void SetTrackedRenderers(bool on)
    {
        if (trackedRenderers == null) return;
        foreach (var r in trackedRenderers)
            if (r != null && r.enabled != on) r.enabled = on;
    }

    void RefreshUI()
    {
        var ship = CurrentShip;
        int n = fleet != null ? fleet.Ships.Count : 0;
        if (shipLabel != null)
            shipLabel.text = ship != null ? $"Nave {index + 1}/{n}: {ship.name}" : "Sin naves";
        if (followButtonLabel != null) followButtonLabel.text = IsFollowing ? arText : followText;
        if (followHint != null) followHint.SetActive(IsFollowing);
        if (scanHint != null) scanHint.SetActive(!IsFollowing && !targetVisible);
    }
}
