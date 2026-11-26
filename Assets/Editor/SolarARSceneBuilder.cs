using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Construye/actualiza las escenas del proyecto de forma reproducible.
/// Menu: Solar AR > ...  (se puede volver a ejecutar: borra lo generado y lo recrea).
/// </summary>
public static class SolarARSceneBuilder
{
    const string ScenesDir = "Assets/Scenes/";
    const string MenuScenePath = ScenesDir + "MainMenu.unity";
    const string PlanetsScenePath = ScenesDir + "SampleScene.unity";
    const string ShipsScenePath = ScenesDir + "SampleScene-Ships.unity";
    const string PlanetPrefabs = "Assets/Solar System/Prefabs/";
    const string ShipModels = "Assets/Space Ships/Prefabs/";
    const string MaterialsDir = "Assets/Solar System/Materials/";
    const string ShipViewLayer = "ShipView";

    static readonly Color ButtonBg = new Color(0.07f, 0.09f, 0.18f, 0.88f);
    static readonly Color AccentBg = new Color(0.13f, 0.42f, 0.95f, 0.95f);
    static readonly Color TextColor = Color.white;

    // nombre, escala (diametro), radio orbita, periodo (s), giro (grados/s), inclinacion eje
    static readonly (string name, float size, float orbit, float period, float spin, float tilt)[] Planets =
    {
        ("Mercury", 0.008f, 0.055f,  6f,  20f,  0f),
        ("Venus",   0.012f, 0.075f, 10f, -10f,  3f),
        ("Earth",   0.013f, 0.095f, 14f,  60f, 23f),
        ("Mars",    0.010f, 0.115f, 20f,  55f, 25f),
        ("Jupiter", 0.030f, 0.155f, 34f, 120f,  3f),
        ("Saturn",  0.022f, 0.200f, 46f, 110f, 27f),
        ("Uranus",  0.018f, 0.240f, 60f,  80f, 82f),
        ("Neptune", 0.017f, 0.275f, 75f,  80f, 28f),
    };

    static readonly string[] FleetModels = { "StarSparrow01", "StarSparrow03", "StarSparrow06", "StarSparrow09", "StarSparrow12", "StarSparrow15" };
    const float ShipScale = 0.0016f;
    const float SunSize = 0.055f;
    // Escala global del sistema sobre el target (0.2 m de ancho): 0.65 => ~36 cm de diametro.
    const float SystemScale = 0.65f;

    // ------------------------------------------------------------------------

    [MenuItem("Solar AR/Construir todo")]
    public static void BuildAll()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        BuildShipsScene();
        AddMenuButtonToPlanetsScene();
        BuildMainMenu();
        ConfigureBuildSettings();
        EditorSceneManager.OpenScene(MenuScenePath);
        Debug.Log("Solar AR: escenas construidas.");
    }

    [MenuItem("Solar AR/Configurar Build Settings")]
    public static void ConfigureBuildSettings()
    {
        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene(MenuScenePath, true),
            new EditorBuildSettingsScene(PlanetsScenePath, true),
            new EditorBuildSettingsScene(ShipsScenePath, true),
        };
    }

    // ------------------------------------------------------------------------
    // Escena 2: Sistema solar + naves
    // ------------------------------------------------------------------------

    [MenuItem("Solar AR/Construir escena Sistema Solar + Naves")]
    public static void BuildShipsScene()
    {
        var scene = EditorSceneManager.OpenScene(ShipsScenePath, OpenSceneMode.Single);
        int layer = EnsureLayer(ShipViewLayer);

        var imageTarget = FindRoot(scene, "ImageTarget");
        var arCamera = FindRoot(scene, "ARCamera").GetComponent<Camera>();
        var canvas = FindRoot(scene, "Canvas");

        // 1) Limpiar el contenido anterior (las 15 naves del carrusel y su script).
        var oldSwitcher = imageTarget.GetComponent<MultiTargetVuforia>();
        if (oldSwitcher) Object.DestroyImmediate(oldSwitcher);
        foreach (Transform child in imageTarget.transform.Cast<Transform>().ToList())
            if (child.name.StartsWith("StarSparrow") || child.name == "SolarSystem")
                Object.DestroyImmediate(child.gameObject);
        DestroyRoot(scene, "ShipViewRig");
        DestroyRoot(scene, "SceneLoader");

        // 2) Sistema solar
        var system = NewChild("SolarSystem", imageTarget.transform);
        system.transform.localScale = Vector3.one * SystemScale;
        var sunMat = GetSunEmissiveMaterial();
        var orbitMat = GetOrbitMaterial();

        var sun = (GameObject)PrefabUtility.InstantiatePrefab(Load<GameObject>(PlanetPrefabs + "Sun.prefab"), system.transform);
        sun.name = "Sun";
        sun.transform.localPosition = Vector3.zero;
        sun.transform.localScale = Vector3.one * SunSize;
        sun.GetComponent<MeshRenderer>().sharedMaterial = sunMat;
        var sunSpin = sun.AddComponent<PlanetOrbit>();
        sunSpin.orbitRadius = 0f; sunSpin.orbitPeriod = 0f; sunSpin.spinSpeed = 8f;

        var orbits = NewChild("Orbits", system.transform);
        var planets = NewChild("Planets", system.transform);
        var obstacles = new List<Transform> { sun.transform };

        for (int i = 0; i < Planets.Length; i++)
        {
            var p = Planets[i];
            var go = (GameObject)PrefabUtility.InstantiatePrefab(Load<GameObject>(PlanetPrefabs + p.name + ".prefab"), planets.transform);
            go.name = p.name;
            go.transform.localScale = Vector3.one * p.size;
            var orbit = go.AddComponent<PlanetOrbit>();
            orbit.orbitRadius = p.orbit;
            orbit.orbitPeriod = p.period;
            orbit.spinSpeed = p.spin;
            orbit.axialTilt = p.tilt;
            orbit.startAngle = i * 137.5f; // angulo aureo: reparte los planetas
            obstacles.Add(go.transform);

            var line = NewChild("Orbit_" + p.name, orbits.transform);
            var lr = line.AddComponent<LineRenderer>();
            lr.sharedMaterial = orbitMat;
            var circle = line.AddComponent<CircleLine>();
            circle.radius = p.orbit;
            circle.width = 0.0007f;
            circle.color = new Color(0.75f, 0.85f, 1f, 0.45f);
            circle.Rebuild();
        }

        // 3) Flota de naves con steering
        var fleetGo = NewChild("Fleet", system.transform);
        var fleet = fleetGo.AddComponent<ShipFleet>();
        fleet.obstacles = obstacles;

        for (int i = 0; i < FleetModels.Length; i++)
        {
            var model = Load<GameObject>(ShipModels + FleetModels[i] + ".fbx");
            var ship = (GameObject)PrefabUtility.InstantiatePrefab(model, fleetGo.transform);
            ship.name = FleetModels[i];
            ship.transform.localScale = Vector3.one * ShipScale;
            float a = (i / (float)FleetModels.Length) * Mathf.PI * 2f;
            float r = 0.13f + 0.05f * (i % 3);
            ship.transform.localPosition = new Vector3(Mathf.Cos(a) * r, 0.03f + 0.01f * (i % 2), Mathf.Sin(a) * r);
            ship.transform.localRotation = Quaternion.LookRotation(new Vector3(-Mathf.Sin(a), 0f, Mathf.Cos(a)));
            var steering = ship.AddComponent<ShipSteering>();
            steering.maxSpeed = 0.04f + 0.004f * i;
        }

        var marker = NewChild("SelectionMarker", fleetGo.transform);
        var markerLr = marker.AddComponent<LineRenderer>();
        markerLr.sharedMaterial = orbitMat;
        var markerCircle = marker.AddComponent<CircleLine>();
        markerCircle.radius = 0.016f;
        markerCircle.segments = 40;
        markerCircle.width = 0.001f;
        markerCircle.color = new Color(0.2f, 1f, 0.9f, 0.9f);
        markerCircle.Rebuild();

        // 4) Camara de la escena que sigue a la nave (+ fondo de estrellas)
        var rig = new GameObject("ShipViewRig");
        SceneManager.MoveGameObjectToScene(rig, scene);
        var camGo = NewChild("FollowCamera", rig.transform);
        var cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.01f, 0.01f, 0.035f, 1f);
        cam.nearClipPlane = 0.001f;
        cam.farClipPlane = 20f;
        cam.fieldOfView = 55f;
        cam.depth = arCamera.depth + 1;
        cam.enabled = false;
        var camData = camGo.AddComponent<UniversalAdditionalCameraData>();
        camData.renderPostProcessing = false;

        var stars = NewChild("Starfield", rig.transform);
        stars.layer = layer;
        var ps = stars.AddComponent<ParticleSystem>();
        var main = ps.main; main.playOnAwake = false; main.loop = true;
        var em = ps.emission; em.enabled = false;
        var sh = ps.shape; sh.enabled = false;
        var psr = stars.GetComponent<ParticleSystemRenderer>();
        psr.sharedMaterial = GetParticleMaterial();
        psr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        psr.receiveShadows = false;
        var starfield = stars.AddComponent<Starfield>();
        starfield.followTarget = camGo.transform;
        starfield.orientationReference = system.transform;

        // La ARCamera no debe ver las estrellas.
        arCamera.cullingMask &= ~(1 << layer);
        EditorUtility.SetDirty(arCamera);

        var view = rig.AddComponent<ShipViewController>();
        view.fleet = fleet;
        view.followCamera = cam;
        view.contentRoot = system.transform;
        view.observerHandler = imageTarget.GetComponent<DefaultObserverEventHandler>();
        view.selectionMarker = marker.transform;

        // 5) UI
        var loader = new GameObject("SceneLoader").AddComponent<SceneLoader>();
        SceneManager.MoveGameObjectToScene(loader.gameObject, scene);
        SetupCanvasScaler(canvas);
        foreach (var n in new[] { "ButtonMenu", "ButtonSeguir", "ShipLabel", "FollowHint", "ScanHint" })
        {
            var old = canvas.transform.Find(n);
            if (old) Object.DestroyImmediate(old.gameObject);
        }

        var prev = canvas.transform.Find("ButtonPrevio").GetComponent<Button>();
        var next = canvas.transform.Find("ButtonSiguiente").GetComponent<Button>();
        StyleButton(prev, "< Nave", new Vector2(0.5f, 0f), new Vector2(-340f, 70f), new Vector2(280f, 140f), ButtonBg);
        StyleButton(next, "Nave >", new Vector2(0.5f, 0f), new Vector2(340f, 70f), new Vector2(280f, 140f), ButtonBg);
        SetListener(prev, view.PreviousShip);
        SetListener(next, view.NextShip);

        var follow = CreateButton(canvas.transform, "ButtonSeguir", "Seguir nave", new Vector2(0.5f, 0f), new Vector2(0f, 70f), new Vector2(340f, 140f), AccentBg);
        SetListener(follow, view.ToggleFollow);

        var menu = CreateButton(canvas.transform, "ButtonMenu", "Menú", new Vector2(0f, 1f), new Vector2(40f, -40f), new Vector2(220f, 100f), ButtonBg);
        UnityEventTools.AddStringPersistentListener(menu.onClick, loader.LoadScene, SceneLoader.MenuScene);

        var label = CreateLabel(canvas.transform, "ShipLabel", "Nave", new Vector2(0.5f, 1f), new Vector2(0f, -160f), new Vector2(760f, 90f), 40f, true);
        var followHint = CreateLabel(canvas.transform, "FollowHint", "Arrastra para girar · Pellizca para acercar", new Vector2(0.5f, 0f), new Vector2(0f, 240f), new Vector2(900f, 80f), 34f, true);
        var scanHint = CreateLabel(canvas.transform, "ScanHint", "Apunta la cámara a la imagen del Sistema Solar", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 130f), 40f, true);

        view.shipLabel = label.GetComponentInChildren<TMP_Text>();
        view.followButtonLabel = follow.GetComponentInChildren<TMP_Text>();
        view.followHint = followHint;
        view.scanHint = scanHint;

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("Solar AR: escena SampleScene-Ships construida.");
    }

    // ------------------------------------------------------------------------
    // Escena 1: agregar boton para volver al menu
    // ------------------------------------------------------------------------

    [MenuItem("Solar AR/Agregar botón Menú a SampleScene")]
    public static void AddMenuButtonToPlanetsScene()
    {
        var scene = EditorSceneManager.OpenScene(PlanetsScenePath, OpenSceneMode.Single);
        var canvas = FindRoot(scene, "Canvas");
        var old = canvas.transform.Find("ButtonMenu");
        if (old) Object.DestroyImmediate(old.gameObject);
        DestroyRoot(scene, "SceneLoader");

        var loader = new GameObject("SceneLoader").AddComponent<SceneLoader>();
        SceneManager.MoveGameObjectToScene(loader.gameObject, scene);

        // Se clona el estilo de los botones existentes (Previo/Siguiente).
        var template = canvas.transform.Find("ButtonPrevio").gameObject;
        var go = Object.Instantiate(template, canvas.transform);
        go.name = "ButtonMenu";
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(30f, -30f);
        rt.sizeDelta = new Vector2(160f, 80f);
        go.GetComponentInChildren<TMP_Text>().text = "Menú";
        var btn = go.GetComponent<Button>();
        ClearListeners(btn);
        UnityEventTools.AddStringPersistentListener(btn.onClick, loader.LoadScene, SceneLoader.MenuScene);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("Solar AR: boton Menu agregado a SampleScene.");
    }

    // ------------------------------------------------------------------------
    // Menu principal
    // ------------------------------------------------------------------------

    [MenuItem("Solar AR/Crear escena MainMenu")]
    public static void BuildMainMenu()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var camGo = new GameObject("Main Camera");
        camGo.tag = "MainCamera";
        var cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.01f, 0.015f, 0.05f, 1f);
        cam.nearClipPlane = 0.01f;
        cam.farClipPlane = 20f;
        camGo.AddComponent<UniversalAdditionalCameraData>();
        camGo.AddComponent<AudioListener>();

        var stars = new GameObject("Starfield");
        stars.transform.SetParent(camGo.transform, false);
        var ps = stars.AddComponent<ParticleSystem>();
        var main = ps.main; main.playOnAwake = false; main.loop = true;
        var em = ps.emission; em.enabled = false;
        var sh = ps.shape; sh.enabled = false;
        stars.GetComponent<ParticleSystemRenderer>().sharedMaterial = GetParticleMaterial();
        stars.AddComponent<Starfield>();

        var loader = new GameObject("SceneLoader").AddComponent<SceneLoader>();

        var es = new GameObject("EventSystem");
        es.AddComponent<EventSystem>();
        es.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();

        var canvasGo = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.layer = 5;
        canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        SetupCanvasScaler(canvasGo);
        var c = canvasGo.transform;

        CreateText(c, "Title", "Sistema Solar AR", new Vector2(0.5f, 0.5f), new Vector2(0f, 470f), new Vector2(1000f, 140f), 92f, FontStyles.Bold);
        CreateText(c, "Subtitle", "Elige una experiencia", new Vector2(0.5f, 0.5f), new Vector2(0f, 360f), new Vector2(1000f, 80f), 44f, FontStyles.Normal);

        var planets = CreateButton(c, "ButtonPlanetas", "Planetas", new Vector2(0.5f, 0.5f), new Vector2(0f, 160f), new Vector2(720f, 170f), ButtonBg);
        CreateText(planets.transform, "Detail", "Un planeta a la vez sobre la imagen", new Vector2(0.5f, 0f), new Vector2(0f, 18f), new Vector2(700f, 40f), 28f, FontStyles.Italic);
        UnityEventTools.AddStringPersistentListener(planets.onClick, loader.LoadScene, SceneLoader.PlanetsScene);

        var ships = CreateButton(c, "ButtonSistemaSolar", "Sistema Solar + Naves", new Vector2(0.5f, 0.5f), new Vector2(0f, -40f), new Vector2(720f, 170f), AccentBg);
        CreateText(ships.transform, "Detail", "Órbitas, naves y cámara de seguimiento", new Vector2(0.5f, 0f), new Vector2(0f, 18f), new Vector2(700f, 40f), 28f, FontStyles.Italic);
        UnityEventTools.AddStringPersistentListener(ships.onClick, loader.LoadScene, SceneLoader.SolarShipsScene);

        var quit = CreateButton(c, "ButtonSalir", "Salir", new Vector2(0.5f, 0.5f), new Vector2(0f, -230f), new Vector2(360f, 120f), new Color(0.25f, 0.06f, 0.08f, 0.85f));
        UnityEventTools.AddVoidPersistentListener(quit.onClick, loader.Quit);

        CreateText(c, "Footer", "Usa la imagen \"SolarSystem\" como marcador", new Vector2(0.5f, 0f), new Vector2(0f, 60f), new Vector2(1000f, 60f), 30f, FontStyles.Normal)
            .color = new Color(1f, 1f, 1f, 0.6f);

        // Ajusta el texto principal de los botones hacia arriba para dejar lugar al detalle.
        foreach (var b in new[] { planets, ships })
        {
            var t = (RectTransform)b.transform.Find("Text (TMP)");
            t.offsetMin = new Vector2(0f, 40f);
        }

        EditorSceneManager.SaveScene(scene, MenuScenePath);
        Debug.Log("Solar AR: escena MainMenu creada.");
    }

    // ------------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------------

    static T Load<T>(string path) where T : Object
    {
        var a = AssetDatabase.LoadAssetAtPath<T>(path);
        if (a == null) throw new System.Exception("No se encontro el asset: " + path);
        return a;
    }

    static GameObject FindRoot(Scene scene, string name)
    {
        var go = scene.GetRootGameObjects().FirstOrDefault(g => g.name == name);
        if (go == null) throw new System.Exception($"No se encontro '{name}' en {scene.name}");
        return go;
    }

    static void DestroyRoot(Scene scene, string name)
    {
        foreach (var g in scene.GetRootGameObjects().Where(g => g.name == name).ToList())
            Object.DestroyImmediate(g);
    }

    static GameObject NewChild(string name, Transform parent)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.layer = parent.gameObject.layer;
        return go;
    }

    static int EnsureLayer(string name)
    {
        int existing = LayerMask.NameToLayer(name);
        if (existing >= 0) return existing;
        var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        var layers = tagManager.FindProperty("layers");
        for (int i = 8; i < layers.arraySize; i++)
        {
            var sp = layers.GetArrayElementAtIndex(i);
            if (string.IsNullOrEmpty(sp.stringValue))
            {
                sp.stringValue = name;
                tagManager.ApplyModifiedProperties();
                return i;
            }
        }
        throw new System.Exception("No hay capas libres para " + name);
    }

    static Material GetSunEmissiveMaterial()
    {
        string path = MaterialsDir + "Sun_Emissive.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(Load<Material>(MaterialsDir + "Sun.mat"));
            AssetDatabase.CreateAsset(mat, path);
        }
        var tex = mat.GetTexture("_BaseMap");
        mat.EnableKeyword("_EMISSION");
        mat.SetTexture("_EmissionMap", tex);
        mat.SetColor("_EmissionColor", new Color(1f, 0.85f, 0.6f) * 1.2f);
        mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static Material GetOrbitMaterial()
    {
        string path = MaterialsDir + "OrbitLine.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Sprites/Default"));
            AssetDatabase.CreateAsset(mat, path);
        }
        return mat;
    }

    static Material GetParticleMaterial()
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>("Packages/com.unity.render-pipelines.universal/Runtime/Materials/ParticlesUnlit.mat");
        return mat != null ? mat : GetOrbitMaterial();
    }

    static void SetupCanvasScaler(GameObject canvas)
    {
        var scaler = canvas.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        EditorUtility.SetDirty(scaler);
    }

    static Sprite UISprite() => AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

    static Button CreateButton(Transform parent, string name, string label, Vector2 anchor, Vector2 pos, Vector2 size, Color bg)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.layer = 5;
        go.transform.SetParent(parent, false);
        var btn = go.GetComponent<Button>();
        var text = CreateText(go.transform, "Text (TMP)", label, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, 44f, FontStyles.Bold);
        var trt = text.rectTransform;
        trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.offsetMin = trt.offsetMax = Vector2.zero;
        StyleButton(btn, label, anchor, pos, size, bg);
        return btn;
    }

    static void StyleButton(Button btn, string label, Vector2 anchor, Vector2 pos, Vector2 size, Color bg)
    {
        var rt = (RectTransform)btn.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        var img = btn.GetComponent<Image>();
        img.sprite = UISprite();
        img.type = Image.Type.Sliced;
        img.color = bg;
        var colors = btn.colors;
        colors.highlightedColor = new Color(0.85f, 0.9f, 1f, 1f);
        colors.pressedColor = new Color(0.6f, 0.7f, 0.9f, 1f);
        btn.colors = colors;
        var tmp = btn.GetComponentInChildren<TMP_Text>();
        tmp.text = label;
        tmp.color = TextColor;
        tmp.fontSize = Mathf.Min(44f, size.y * 0.36f);
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        EditorUtility.SetDirty(btn.gameObject);
    }

    static TextMeshProUGUI CreateText(Transform parent, string name, string text, Vector2 anchor, Vector2 pos, Vector2 size, float fontSize, FontStyles style)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = 5;
        go.transform.SetParent(parent, false);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        if (TMP_Settings.defaultFontAsset != null) tmp.font = TMP_Settings.defaultFontAsset;
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.fontStyle = style;
        tmp.color = TextColor;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        tmp.raycastTarget = false;
        var rt = tmp.rectTransform;
        rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        return tmp;
    }

    /// <summary>Etiqueta con fondo semitransparente (no bloquea toques).</summary>
    static GameObject CreateLabel(Transform parent, string name, string text, Vector2 anchor, Vector2 pos, Vector2 size, float fontSize, bool background)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.layer = 5;
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        var img = go.GetComponent<Image>();
        img.sprite = UISprite();
        img.type = Image.Type.Sliced;
        img.color = background ? new Color(0f, 0f, 0f, 0.5f) : Color.clear;
        img.raycastTarget = false;
        var tmp = CreateText(go.transform, "Text (TMP)", text, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, fontSize, FontStyles.Normal);
        var trt = tmp.rectTransform;
        trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
        trt.offsetMin = new Vector2(16f, 6f); trt.offsetMax = new Vector2(-16f, -6f);
        return go;
    }

    static void ClearListeners(Button btn)
    {
        for (int i = btn.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
            UnityEventTools.RemovePersistentListener(btn.onClick, i);
    }

    static void SetListener(Button btn, UnityEngine.Events.UnityAction action)
    {
        ClearListeners(btn);
        UnityEventTools.AddPersistentListener(btn.onClick, action);
    }
}
