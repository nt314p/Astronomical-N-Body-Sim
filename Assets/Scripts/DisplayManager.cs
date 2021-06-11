using System.Collections;
using System.Globalization;
using TMPro;
using UnityEngine;

public class DisplayManager : MonoBehaviour
{
    [SerializeField] private SimulationManager simulationManager;
    [SerializeField] private GameObject helpScreen;
    [SerializeField] private GameObject settingsMenu;
    [SerializeField] private GameObject mainCanvas;
    [SerializeField] private TextMeshProUGUI fpsText;
    [SerializeField] private TMP_InputField timeStepInput;
    [SerializeField] private TMP_InputField bodiesInput;
    [SerializeField] private TMP_InputField massInput;
    [SerializeField] private TMP_InputField initialVelocityInput;
    [SerializeField] private TMP_InputField galaxyRadiusInput;
    [SerializeField] private TMP_Dropdown distributionDropdown;
    [SerializeField] private TMP_Dropdown velocityDropdown;
    [SerializeField] private TMP_InputField minColorSpeedInput;
    [SerializeField] private TMP_InputField maxColorSpeedInput;


    public void Start()
    {
        StartCoroutine(UpdateFPSText());
        OnMinColorSpeedUpdate();
        OnMaxColorSpeedUpdate();
    }

    private IEnumerator UpdateFPSText()
    {
        while (true)
        {
            fpsText.text = $"FPS: {(int) (1.0f / Time.deltaTime)}";
            yield return new WaitForSeconds(.5f);
        }
    }

    public void ToggleHelp()
    {
        if (!helpScreen.activeInHierarchy)
        {
            mainCanvas.SetActive(true); // ensure that UI is showing for help menu
            helpScreen.SetActive(true);
            return;
        }
        helpScreen.SetActive(!helpScreen.activeInHierarchy);
    }

    public void ToggleSettingsMenu()
    {
        settingsMenu.SetActive(!settingsMenu.activeInHierarchy);
    }

    public void ToggleUI()
    {
        mainCanvas.SetActive(!mainCanvas.activeInHierarchy);
    }

    public void UpdateTimeStepText(float timeStep)
    {
        timeStepInput.text = timeStep.ToString(CultureInfo.InvariantCulture);
    }

    public void OnTimeStepEndEdit()
    {
        simulationManager.SetTimeStep(float.Parse(timeStepInput.text));
    }
    
    public void OnBodiesEndEdit()
    {
        var bodies = Mathf.RoundToInt(int.Parse(bodiesInput.text) / 512.0f) * 512;
        bodies = Mathf.Max(bodies, 512);
        bodies = Mathf.Min(bodies, 65536);
        bodiesInput.text = bodies.ToString();
    }

    public void OnMinColorSpeedUpdate()
    {
        simulationManager.SetMinColorSpeed(float.Parse(minColorSpeedInput.text));
    }
    
    public void OnMaxColorSpeedUpdate()
    {
        simulationManager.SetMaxColorSpeed(float.Parse(maxColorSpeedInput.text));
    }

    public void OnRegenerateGalaxy()
    {
        var numMasses = int.Parse(bodiesInput.text);
        var mass = float.Parse(massInput.text) * 1000000f;
        var initialVelocity = float.Parse(initialVelocityInput.text);
        var galaxyRadius = float.Parse(galaxyRadiusInput.text);
        var distributionRelation = (RadiusRelation) distributionDropdown.value;
        var velocityRelation = (RadiusRelation) velocityDropdown.value;
        var simulationState = new SimulationState(numMasses, mass, initialVelocity, galaxyRadius, distributionRelation,
            velocityRelation);
        simulationManager.SetSimulationState(simulationState, float.Parse(timeStepInput.text));
    }
}
