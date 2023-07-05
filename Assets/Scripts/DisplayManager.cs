using System;
using System.Collections;
using System.Globalization;
using TMPro;
using UnityEngine;

public class DisplayManager : MonoBehaviour
{
    [SerializeField] private Simulation simulation;
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
    [SerializeField] private TextMeshProUGUI statusText;
    private const int BodyInterval = 512; // TODO: synchronize these values
    private const int MaxBodies = 65536;
    
    public bool IsEditingAny { get; private set; }

    private void Start()
    {
        StartCoroutine(UpdateFPSText());
        OnMinColorSpeedUpdate();
        OnMaxColorSpeedUpdate();
    }

    private void Update()
    {
        var editing = false;
        editing |= timeStepInput.isFocused;
        editing |= bodiesInput.isFocused;
        editing |= massInput.isFocused;
        editing |= initialVelocityInput.isFocused;
        editing |= galaxyRadiusInput.isFocused;
        editing |= minColorSpeedInput.isFocused;
        editing |= maxColorSpeedInput.isFocused;
        IsEditingAny = editing;
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

    public void UpdateTimeStepTextMultiplier(int timeStep)
    {
        timeStepInput.text = $"{timeStep}x";
    }

    public void OnTimeStepEndEdit()
    {
        if (float.TryParse(timeStepInput.text, out var val))
        {
            simulation.SetTimeStep(val);
        }
    }

    public void SetMessage(string message)
    {
        statusText.text = message;
    }
    
    public void OnBodiesEndEdit()
    {
        if (!int.TryParse(bodiesInput.text, out var bodies)) return;
        bodies = Mathf.RoundToInt(bodies / ((float)BodyInterval)) * BodyInterval;
        bodies = Mathf.Max(bodies, BodyInterval);
        bodies = Mathf.Min(bodies, MaxBodies);
        bodiesInput.text = bodies.ToString();
    }

    public void OnMinColorSpeedUpdate()
    {
        if(float.TryParse(minColorSpeedInput.text, out var val))
        {
            simulation.SetMinColorSpeed(val);
        }
    }
    
    public void OnMaxColorSpeedUpdate()
    {
        if (float.TryParse(maxColorSpeedInput.text, out var val))
        {
            simulation.SetMaxColorSpeed(val);
        }
    }

    public void OnRegenerateGalaxy()
    {
        try
        {
            var numMasses = int.Parse(bodiesInput.text);
            var mass = float.Parse(massInput.text) * 1000f;
            var initialVelocity = float.Parse(initialVelocityInput.text);
            var galaxyRadius = float.Parse(galaxyRadiusInput.text);
            var distributionRelation = (RadiusRelation) distributionDropdown.value;
            var velocityRelation = (RadiusRelation) velocityDropdown.value;
            var simulationState = new SimulationState(numMasses, mass, initialVelocity, galaxyRadius, distributionRelation,
                velocityRelation);
            simulation.SetSimulationState(simulationState, float.Parse(timeStepInput.text));
            SetMessage("Regenerated galaxy");
        }
        catch (Exception)
        {
            SetMessage("Invalid galaxy parameters");
        }
    }

    public void SetTimeStepReadOnly(bool readOnly)
    {
        timeStepInput.readOnly = readOnly;
    }
}
