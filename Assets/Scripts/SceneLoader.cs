using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Carga escenas desde botones de UI (OnClick -> SceneLoader.LoadScene("Nombre")).
/// Las escenas deben estar agregadas en File > Build Profiles > Scene List.
/// </summary>
public class SceneLoader : MonoBehaviour
{
    public const string MenuScene = "MainMenu";
    public const string PlanetsScene = "SampleScene";
    public const string SolarShipsScene = "SampleScene-Ships";

    public void LoadScene(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogWarning("SceneLoader: nombre de escena vacio.");
            return;
        }
        SceneManager.LoadScene(sceneName);
    }

    public void LoadMenu() => LoadScene(MenuScene);
    public void LoadPlanets() => LoadScene(PlanetsScene);
    public void LoadSolarShips() => LoadScene(SolarShipsScene);

    public void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
