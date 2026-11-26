# Sistema Solar AR (Unity + Vuforia)

Aplicación de Realidad Aumentada para Android hecha en **Unity 6 (6000.6.0f1)** con **Vuforia Engine 11.4.4**. Al apuntar la cámara del dispositivo a la imagen **SolarSystem**, se muestra contenido 3D sobre ella. Tiene dos experiencias, y se elige una desde un menú:

| Escena | Qué muestra |
|---|---|
| `MainMenu` | Menú principal para elegir la experiencia. |
| `SampleScene`: **Planetas** | Un planeta a la vez sobre la imagen. Se cambia con los botones *Previo* y *Siguiente*. |
| `SampleScene-Ships`: **Sistema Solar + Naves** | El sistema solar completo con órbitas, 6 naves que navegan solas (*steering behaviours*) y una cámara virtual que sigue a la nave elegida. La vista se gira con el dedo. |

```
MainMenu ──"Planetas"──────────────► SampleScene ──"Menú"──┐
   ▲      └"Sistema Solar + Naves"─► SampleScene-Ships ─"Menú"─┤
   └───────────────────────────────────────────────────────────┘
```

---

## 1. Requisitos y configuración del proyecto

### Software
- **Unity 6000.6.0f1** con el módulo *Android Build Support* (OpenJDK, SDK y NDK).
- **Git LFS**. El paquete de Vuforia (`Packages/com.ptc.vuforia.engine-11.4.4.tgz`, unos 138 MB), los `.fbx` y las texturas se guardan con LFS. Ver `.gitattributes`.

### Paquetes principales (`Packages/manifest.json`)
| Paquete | Uso |
|---|---|
| `com.ptc.vuforia.engine` 11.4.4 (archivo local `.tgz`) | Tracking de la imagen (Image Target) y video de la cámara. |
| `com.unity.render-pipelines.universal` 17.6 | URP. Los materiales usan `URP/Lit` y `URP/Simple Lit`. |
| `com.unity.inputsystem` 1.20 | Input del proyecto. *Active Input Handling* está en **Input System Package (New)**. Se usa para el touch y para la UI (`InputSystemUIInputModule`). |
| `com.unity.ugui` + TextMeshPro | Interfaz (Canvas, botones y textos TMP). |

### Licencia de Vuforia (importante)
La licencia está en `Assets/Resources/VuforiaConfiguration.asset`. **Ese archivo está en `.gitignore`** para que la clave no se publique en el repositorio remoto. Al clonar el proyecto en otra máquina:
1. Abrir el proyecto en Unity.
2. Ir a **Window ▸ Vuforia Configuration** (o seleccionar `ARCamera ▸ Open Vuforia Engine configuration`).
3. Pegar la **App License Key** del [Vuforia Developer Portal](https://developer.vuforia.com/) en *App License Key*.

> Si se prefiere versionar la licencia (por ejemplo, en un repositorio privado), basta con quitar las dos líneas de `VuforiaConfiguration.asset` del `.gitignore`.

### Image Target
- Tipo **Image Target ▸ From Image** (sin base de datos). Usa la textura `Assets/Solar System/ImageTargets/SolarSystem.jpg`.
- Nombre del target: `SolarSystem`. **Ancho físico: 0.20 m** (alto 0.143 m). Para que la escala del contenido sea correcta, la imagen impresa o mostrada en pantalla debe medir unos 20 cm de ancho.
- `DefaultObserverEventHandler` con *Status Filter* = **Tracked / Extended Tracked**. Muestra el contenido cuando la imagen se detecta y lo oculta (desactiva los renderers) cuando se pierde.

### Build Settings
`File ▸ Build Profiles ▸ Scene List`. La configuración la hace automáticamente el menú **Solar AR ▸ Configurar Build Settings**:

| Índice | Escena |
|---|---|
| 0 | `Assets/Scenes/MainMenu.unity` (escena inicial) |
| 1 | `Assets/Scenes/SampleScene.unity` |
| 2 | `Assets/Scenes/SampleScene-Ships.unity` |

### Compilar para Android
1. `File ▸ Build Profiles ▸ Android ▸ Switch Platform`.
2. *Player Settings*: el proyecto ya usa orientación automática (portrait y landscape permitidos). Vuforia requiere **ARM64** y una *Minimum API Level* compatible (Android 8.0 o superior recomendado).
3. Conectar el dispositivo y usar **Build And Run**.

---

## 2. Estructura del proyecto

```
Assets/
├─ Scenes/
│  ├─ MainMenu.unity            ← menú (nuevo)
│  ├─ SampleScene.unity         ← escena 1: planetas
│  └─ SampleScene-Ships.unity   ← escena 2: sistema solar + naves
├─ Scripts/
│  ├─ MultiTargetsVuforia.cs    ← (escena 1) cambia el planeta visible
│  ├─ SceneLoader.cs            ← carga escenas desde botones
│  └─ SolarShips/               ← (escena 2)
│     ├─ PlanetOrbit.cs         ← órbita circular + rotación del planeta
│     ├─ CircleLine.cs          ← dibuja órbitas y el marcador de selección
│     ├─ ShipFleet.cs           ← volumen de navegación y obstáculos
│     ├─ ShipSteering.cs        ← IA de las naves (steering behaviours)
│     ├─ ShipViewController.cs  ← cámara que sigue a la nave + touch + botones
│     └─ Starfield.cs           ← fondo de estrellas de la cámara virtual
├─ Editor/
│  └─ SolarARSceneBuilder.cs    ← menú "Solar AR" que arma las escenas
├─ Solar System/                ← prefabs, materiales y texturas de los planetas
│  └─ Scripts/PlanetRing.cs     ← genera el anillo de Saturno
├─ Space Ships/                 ← modelos StarSparrow01…40 (.fbx) y materiales
└─ Resources/VuforiaConfiguration.asset (no versionado: licencia)
```

---

## 3. Escena 1: `SampleScene` (Planetas)

### Configuración
| Objeto | Componentes | Función |
|---|---|---|
| **ARCamera** | `Camera`, `VuforiaBehaviour`, `DefaultInitializationErrorHandler`, `UniversalAdditionalCameraData` | Cámara de AR. Vuforia dibuja el video del dispositivo de fondo y mueve esta cámara según el tracking (*World Center Mode* = DEVICE). |
| **ImageTarget** | `ImageTargetBehaviour` (target `SolarSystem`, 0.2 m), `DefaultObserverEventHandler`, `MultiTargetVuforia` | Ancla del contenido. Todo lo que es hijo aparece sobre la imagen. |
| ImageTarget ▸ *Sun, Mercury, Venus, Earth, Mars, Jupiter, Saturn, Uranus, Neptune* | Prefabs de `Assets/Solar System/Prefabs` (esfera + material con textura; Saturno incluye `PlanetRing`) | Los 9 modelos. Solo **Earth** empieza activo (`startModel`). |
| **Canvas** (Screen Space Overlay, *Constant Pixel Size*) | `ButtonPrevio`, `ButtonSiguiente`, `ButtonMenu` | Navegación. |
| **EventSystem** | `InputSystemUIInputModule` | Input de la UI con el nuevo Input System. |
| **SceneLoader** | `SceneLoader` | Lo usa el botón *Menú*. |
| Directional Light, Global Volume | | Iluminación y post-proceso de URP. |

### Proceso (cómo se construyó)
1. Se importó Vuforia 11.4.4 como paquete local y se configuró la licencia en *Vuforia Configuration*.
2. Se reemplazó la *Main Camera* por la **ARCamera** de Vuforia (`GameObject ▸ Vuforia Engine ▸ AR Camera`).
3. Se agregó un **Image Target ▸ From Image** con la textura `SolarSystem.jpg` y un ancho de 0.2 m.
4. Se colocaron los 9 prefabs de planetas como hijos del ImageTarget, en la misma posición. Todos están desactivados menos Earth.
5. Se agregó el script `MultiTargetVuforia` al ImageTarget y dos botones de UI. Su `OnClick` llama a `ChangeARModel(-1)` (*Previo*) y `ChangeARModel(1)` (*Siguiente*).
6. *(Mejora)* Se agregó el botón **Menú** (esquina superior izquierda), que llama a `SceneLoader.LoadScene("MainMenu")`.

### Funcionamiento
- Al detectar la imagen, `DefaultObserverEventHandler` activa los renderers de los hijos y aparece el planeta activo. Al perderla, los oculta.
- `MultiTargetVuforia` guarda el índice del hijo visible. `ChangeARModel(delta)` desactiva el modelo actual y activa el siguiente o el anterior. Al llegar a un extremo vuelve al otro (`0 ↔ childCount-1`).
- **Menú** vuelve a `MainMenu`.

---

## 4. Escena 2: `SampleScene-Ships` (Sistema Solar + Naves)

Esta escena partió de una copia de la escena 1 que mostraba las naves `StarSparrow` una por una. Se rediseñó para mostrar un **panorama completo del sistema solar** con naves en movimiento y una **cámara de escena que sigue a una nave**.

### 4.1 Jerarquía

```
ARCamera                     (Vuforia; su culling mask excluye la capa ShipView)
ImageTarget                  (SolarSystem, 0.2 m, DefaultObserverEventHandler)
└─ SolarSystem               (escala 0.65 → ~36 cm de diámetro)
   ├─ Sun                    (prefab Sun, material Sun_Emissive, PlanetOrbit solo para girar)
   ├─ Orbits
   │  └─ Orbit_Mercury … Orbit_Neptune   (LineRenderer + CircleLine)
   ├─ Planets
   │  └─ Mercury … Neptune               (prefabs + PlanetOrbit)
   └─ Fleet                  (ShipFleet)
      ├─ StarSparrow01, 03, 06, 09, 12, 15  (modelos .fbx + ShipSteering)
      └─ SelectionMarker     (anillo cian sobre la nave seleccionada)
ShipViewRig                  (ShipViewController)
├─ FollowCamera              (Camera virtual, desactivada en modo AR)
└─ Starfield                 (ParticleSystem en la capa ShipView)
Canvas                       (Scale With Screen Size 1080×1920, match 0.5)
├─ ButtonMenu  ├─ ShipLabel  ├─ ScanHint  ├─ FollowHint
└─ ButtonPrevio "< Nave"  ButtonSeguir "Seguir nave"  ButtonSiguiente "Nave >"
SceneLoader, EventSystem, Directional Light, Global Volume
```

### 4.2 Sistema solar con órbitas (`PlanetOrbit`, `CircleLine`)
Se **reutilizan los prefabs** de `Assets/Solar System/Prefabs`. Las medidas están en metros sobre el target, antes de aplicar la escala 0.65 del nodo `SolarSystem`:

| Planeta | Diámetro | Radio de órbita | Periodo (s) | Inclinación del eje |
|---|---|---|---|---|
| Sun | 0.055 | — | — | — |
| Mercury | 0.008 | 0.055 | 6 | 0° |
| Venus | 0.012 | 0.075 | 10 | 3° |
| Earth | 0.013 | 0.095 | 14 | 23° |
| Mars | 0.010 | 0.115 | 20 | 25° |
| Jupiter | 0.030 | 0.155 | 34 | 3° |
| Saturn | 0.022 (+ anillo) | 0.200 | 46 | 27° |
| Uranus | 0.018 | 0.240 | 60 | 82° |
| Neptune | 0.017 | 0.275 | 75 | 28° |

- **PlanetOrbit**: en cada frame calcula `localPosition = (cos θ, 0, sin θ) · radio` con `θ += 2π·dt/periodo`. Además gira el planeta sobre su eje inclinado. El ángulo inicial usa el ángulo áureo (137.5°·i) para que los planetas no salgan alineados. Todo se calcula en **espacio local**, así el sistema sigue a la imagen aunque Vuforia la mueva.
- **CircleLine**: genera un `LineRenderer` circular (`useWorldSpace = false`, `loop = true`) con material `OrbitLine.mat` (shader *Sprites/Default*, semitransparente).
- El Sol usa `Sun_Emissive.mat`, una copia de `Sun.mat` con emisión, para que se vea como una estrella.
- Las escalas y distancias no son reales: están comprimidas para que todo entre en la vista de AR.

### 4.3 Naves navegando: Steering Behaviours (`ShipSteering`, `ShipFleet`)
Cada nave es un agente autónomo basado en los *Steering Behaviours* de Craig Reynolds. En cada frame suma varias fuerzas con peso, limita el resultado a `maxForce` e integra la velocidad:

```
fuerza = Arrive(waypoint)·1.0 + Wander·0.35 + ObstacleAvoidance·2.5
       + Separation·1.2 + Containment·2.0
velocidad = clamp(velocidad + truncar(fuerza, maxForce)·dt, minSpeed, maxSpeed)
posición += velocidad·dt
```

| Comportamiento | Qué hace |
|---|---|
| **Arrive** (Seek con frenado) | Va hacia su *waypoint* y frena dentro de `slowingRadius`. Al llegar elige otro: con un 50 % de probabilidad un punto al azar del sistema y si no, un punto **cerca de un planeta** (la nave "visita" planetas). |
| **Wander** | Un punto que se mueve al azar sobre una esfera delante de la nave. Da trayectorias orgánicas y no en línea recta. |
| **Obstacle Avoidance** | Esquiva el Sol y los planetas, que se mueven. (1) Si la nave está dentro de la zona de seguridad (radio + margen), la empuja hacia afuera. (2) Proyecta el centro del obstáculo sobre la trayectoria (`lookAheadTime`) y, si hay riesgo de choque, desvía la nave hacia un lado. |
| **Separation** | Evita que las naves se encimen entre sí (`separationRadius`). |
| **Containment** | Mantiene las naves dentro de un anillo (`minRadius`–`maxRadius`) y de una franja de altura sobre el plano de las órbitas (`altitudeRange`). |

- **Orientación**: la nave mira hacia su velocidad (`LookRotation`) y se **inclina** (*banking*) según la fuerza lateral, como un avión al girar. La rotación se suaviza con `Slerp`.
- **ShipFleet** define el espacio de navegación. Todo se calcula en el espacio local de `Fleet`, así que el comportamiento no depende de la pose del ImageTarget ni de la escala del sistema. También guarda la lista de obstáculos (Sol y planetas) y registra las naves.
- Cada nave tiene una `maxSpeed` un poco distinta para que no se muevan todas igual.
- Todos los pesos y radios se pueden ajustar en el Inspector. Con el objeto seleccionado se ven *gizmos* del waypoint, la velocidad y el volumen de navegación.

### 4.4 Cámara de la escena que sigue a la nave (`ShipViewController`)
Hay **dos cámaras**:
- **ARCamera** (Vuforia): muestra lo que ve el dispositivo con el sistema solar encima.
- **FollowCamera**: una cámara virtual de la escena con `depth` mayor. En modo *Seguir* se activa y cubre la pantalla con una vista en 3.ª persona de la nave seleccionada, sobre un fondo de estrellas.

**Botones**

| Botón | Acción |
|---|---|
| `< Nave` / `Nave >` | Selecciona la nave anterior o la siguiente (`PreviousShip()` / `NextShip()`). En modo AR, un **anillo cian** marca la nave seleccionada. En modo Seguir, la cámara pasa a la nueva nave. |
| `Seguir nave` / `Vista AR` | `ToggleFollow()`: alterna entre la vista AR y la cámara de seguimiento. |
| `Menú` | Vuelve a `MainMenu`. |

`ShipLabel` muestra "Nave *n*/6: *modelo*". `ScanHint` aparece cuando la imagen no se detecta y `FollowHint` muestra cómo usar los gestos.

**Gestos (solo en modo Seguir)**

| Gesto | Efecto |
|---|---|
| Arrastrar con 1 dedo (o mouse en el editor) | Gira la cámara alrededor de la nave (yaw y pitch; el pitch va de -70° a 80°). Así se puede ver el modelo desde cualquier ángulo. |
| Pellizcar con 2 dedos (o rueda del mouse) | Acerca o aleja la cámara (`minDistance`–`maxDistance`). |

**Funcionamiento interno**
- La pose de la cámara se calcula como una órbita alrededor de la nave: `rotación = rot(SolarSystem) · rumbo_nave · Euler(pitch, yaw)` y `posición = nave − rotación·forward·distancia`. Con `alignWithShipHeading` la cámara se queda **detrás de la nave** cuando gira, como una *chase cam*. El arrastre del usuario se suma como desfase. Posición y rotación se suavizan con un *lerp* exponencial.
- Las distancias están en unidades del sistema y se multiplican por su escala. Si se cambia la escala de `SolarSystem`, el encuadre se mantiene.
- El touch usa **EnhancedTouch** del Input System. Antes de empezar un arrastre se hace un *raycast* contra la UI (`EventSystem.RaycastAll`), para que tocar un botón no gire la cámara.
- **Pérdida del tracking**: Vuforia apaga los renderers cuando deja de ver la imagen. En modo Seguir, `ShipViewController` los mantiene encendidos, así que la nave se sigue viendo aunque el usuario deje de apuntar a la imagen. La simulación (órbitas y naves) sigue corriendo. Al volver a *Vista AR* se respeta otra vez el estado del tracking.
- **Starfield** emite 600 partículas estáticas sobre una esfera alrededor de la FollowCamera, orientadas con el sistema solar. Están en la capa **ShipView**, que la ARCamera excluye de su *culling mask*. Por eso las estrellas no aparecen en la vista AR.

### 4.5 Proceso (cómo se construyó)
1. Se partió de la copia `SampleScene-Ships` (ARCamera, ImageTarget, Canvas y EventSystem ya configurados).
2. Se quitaron las 15 naves del carrusel y el script `MultiTargetVuforia` del ImageTarget.
3. Se creó `SolarSystem` con el Sol, los 8 planetas (prefabs + `PlanetOrbit`) y las líneas de órbita (`CircleLine`).
4. Se creó `Fleet` (`ShipFleet`) con 6 modelos `StarSparrow` + `ShipSteering`, y el marcador de selección.
5. Se creó `ShipViewRig` con la `FollowCamera`, el `Starfield` (capa `ShipView`) y el `ShipViewController`.
6. Se reutilizaron los botones *Previo* y *Siguiente* como `< Nave` / `Nave >`, y se agregaron `Seguir nave`, `Menú` y las etiquetas. El `CanvasScaler` pasó a *Scale With Screen Size* para que la UI se adapte a cualquier pantalla.

Todo esto lo hace el script de editor **`Assets/Editor/SolarARSceneBuilder.cs`**, desde el menú **Solar AR**:

| Menú | Acción |
|---|---|
| `Solar AR ▸ Construir todo` | Ejecuta todo lo siguiente. |
| `Solar AR ▸ Construir escena Sistema Solar + Naves` | Regenera la escena 2. Es idempotente: borra lo que generó antes y lo vuelve a crear. |
| `Solar AR ▸ Agregar botón Menú a SampleScene` | Agrega el botón Menú a la escena 1. |
| `Solar AR ▸ Crear escena MainMenu` | Crea el menú. |
| `Solar AR ▸ Configurar Build Settings` | Registra las 3 escenas en orden. |

> ⚠️ Al volver a ejecutar el builder se pierden los ajustes manuales hechos en el Inspector a los objetos generados. Para cambios permanentes conviene editar las constantes del builder (tabla de planetas, `FleetModels`, `ShipScale`, `SystemScale`, etc.).

### 4.6 Prueba en el editor
- Con una webcam, Vuforia funciona en Play Mode: se muestra la imagen `SolarSystem.jpg` a la cámara.
- Sin webcam ni imagen, se puede marcar **`followOnStart`** en `ShipViewRig ▸ ShipViewController`. La escena arranca en modo Seguir y se puede probar la navegación de las naves y los gestos con el mouse.

---

## 5. Menú principal (`MainMenu`)
- Cámara con fondo oscuro y `Starfield`, Canvas *Scale With Screen Size* y `EventSystem` con `InputSystemUIInputModule`.
- Botones: **Planetas** → `SceneLoader.LoadScene("SampleScene")`, **Sistema Solar + Naves** → `LoadScene("SampleScene-Ships")` y **Salir** → `SceneLoader.Quit()`.
- `SceneLoader` usa `SceneManager.LoadScene(nombre)`. Al cambiar de escena, el `VuforiaBehaviour` de cada escena AR inicia o detiene Vuforia.

---

## 6. Control de versiones
- Repositorio Git con **LFS** (`.gitattributes`) para el `.tgz` de Vuforia, `.fbx`, imágenes y fuentes. `.gitignore` excluye `Library/`, `Temp/`, `Logs/`, `UserSettings/`, los builds, los archivos del IDE y la licencia de Vuforia.
- Para subirlo a un remoto:
  ```bash
  git remote add origin <url-del-repo>
  git push -u origin main
  ```
  GitHub limita los archivos a 100 MB. El paquete de Vuforia pasa ese límite y por eso **debe ir por LFS** (ya configurado). Hay que verificar que el remoto tenga LFS habilitado.
