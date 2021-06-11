using UnityEngine;

public class SimulationManager : MonoBehaviour
{
    [SerializeField] private DisplayManager displayManager;
    [SerializeField] private PromptManager promptManager;
    [SerializeField] private ComputeShader computeShader;
    [SerializeField] private Camera cam;
    [SerializeField] private FirstPersonCam firstPersonCamera;
    [SerializeField] private bool useScreenDimensions;
    [SerializeField] private Vector2Int textureDimensions = Vector2Int.zero;
    [SerializeField] private bool freezeSimulation;
    [SerializeField] private bool useFadeProcessing;
    [SerializeField] private bool renderMasses = true;
    [SerializeField] private bool lockCamera;
    
    private AstronomicalSimulator astronomicalSimulator;
    private AstronomicalRenderer astronomicalRenderer;

    private float previousEnergy = 0;
    private float[] energies = new float[20];
    private int energiesIndex = 0;

    private void Awake()
    {
        Application.targetFrameRate = 60;
        FileHelper.InitializeDirectories();
        var simulationState = new SimulationState(2048);
        astronomicalSimulator = new AstronomicalSimulator(computeShader, simulationState);
        astronomicalRenderer = new AstronomicalRenderer(astronomicalSimulator, computeShader, cam);
        displayManager.UpdateTimeStepText(astronomicalSimulator.TimeStep);
        
        promptManager.ShowPrompt("Enter your name:", "No thanks", "Submit", PromptCallback, true);
    }

    private void OnDisable()
    {
        Debug.Log("SimulationManager disabled, releasing buffers");
        astronomicalSimulator.ReleaseBuffers(true);
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            promptManager.ShowPrompt("Really quit?", "No", "Yes", QuitPromptCallback, false);
        }
        
        if (Input.GetKeyDown(KeyCode.F))
        {
            freezeSimulation = !freezeSimulation;
        }

        if (Input.GetKeyDown(KeyCode.L))
        {
            lockCamera = !lockCamera;
            Cursor.lockState = lockCamera ? CursorLockMode.None : CursorLockMode.Locked;
        }

        if (Input.GetKeyDown(KeyCode.F7))
        {
            SaveSimulationState();
            Debug.Log("Saved simulation state");
        }

        if (Input.GetKeyDown(KeyCode.F8))
        {
            LoadSimulationState();
            Debug.Log("Loaded simulation state");
        }

        if (Input.GetKeyDown(KeyCode.H))
        {
            displayManager.ToggleHelp();
        }

        if (Input.GetKeyDown(KeyCode.K))
        {
            displayManager.ToggleSettingsMenu();
        }
        
        if (Input.GetKeyDown(KeyCode.F1))
        {
            SaveScreenshot();
        }

        if (Input.GetKeyDown(KeyCode.F2))
        {
            displayManager.ToggleUI();
        }

        if (Input.GetKeyDown(KeyCode.Period))
        {
            astronomicalSimulator.TimeStep *= 2;
            displayManager.UpdateTimeStepText(astronomicalSimulator.TimeStep);
        }

        if (Input.GetKeyDown(KeyCode.Comma))
        {
            astronomicalSimulator.TimeStep *= 0.5f;
            displayManager.UpdateTimeStepText(astronomicalSimulator.TimeStep);
        }

        if (!lockCamera)
        {
            firstPersonCamera.ProcessCamera();
        }

        if (Input.GetKeyDown(KeyCode.F9))
        {
            if (!FileHelper.IsRecording)
            {
                FileHelper.StartStateRecording(astronomicalSimulator);
                Debug.Log("Started recording");
            }
            else
            {
                FileHelper.EndStateRecording();
                Debug.Log("Ended recording");
            }
        }

        if (Input.GetKeyDown(KeyCode.F10))
        {
            if (!FileHelper.IsReplaying)
            {
                FileHelper.StartStateReplay(astronomicalSimulator);
                Debug.Log("Started replay");
                astronomicalRenderer.SetBuffers();
            }
            else
            {
                FileHelper.EndStateReplay();
                Debug.Log("Ended replay");
            }
        }

        if (FileHelper.IsRecording && !freezeSimulation)
        {
            FileHelper.UpdateStateRecording();
        }

        // astronomicalSimulator.GetTotalEnergy();

        //LogEnergies();

        // LoadSimulationState();
        // SaveSimulationState();
        //TextLogger.Log($"{Time.time},{data.z}");
    }

    public void PromptCallback(bool rightButtonClicked, string inputResult)
    {
        if (rightButtonClicked)
        {
            Debug.Log("Right prompt button clicked");
        }
        else
        {
            Debug.Log("Left prompt button clicked");
        }

        if (inputResult == null)
        {
            Debug.Log("The input was null");
            return;
        } 
        
        Debug.Log($"The input is {inputResult}");
    }

    public void QuitPromptCallback(bool rightButtonClicked, string inputResult)
    {
        if (!rightButtonClicked) return;
        FileHelper.CloseFiles();
        Application.Quit();
    }

    public void SetSimulationState(SimulationState simulationState, float timeStep)
    {
        astronomicalSimulator.ReleaseBuffers(true);
        astronomicalSimulator = new AstronomicalSimulator(computeShader, simulationState);
        astronomicalRenderer = new AstronomicalRenderer(astronomicalSimulator, computeShader, cam);
        astronomicalSimulator.TimeStep = timeStep;
    }

    public float GetTimeStep()
    {
        return astronomicalSimulator.TimeStep;
    }

    public void SetTimeStep(float timeStep)
    {
        astronomicalSimulator.TimeStep = timeStep;
    }

    public void SetMinColorSpeed(float minSpeed)
    {
        astronomicalRenderer.SetMinColorSpeed(minSpeed);
    }
    
    public void SetMaxColorSpeed(float maxSpeed)
    {
        astronomicalRenderer.SetMaxColorSpeed(maxSpeed);
    }

    private void LogEnergies()
    {
        var data = astronomicalSimulator.GetTotalEnergy();
        Debug.Log("Per: " + (data.z - previousEnergy) * 100 / astronomicalSimulator.TimeStep / data.z);
        Debug.Log("Tot: " + data.z);

        float averageEnergy = 0;
        for (var index = 0; index < energies.Length; index++)
        {
            averageEnergy += energies[index];
        }

        averageEnergy /= energies.Length;
        Debug.Log("Avg: " + averageEnergy);
        
        previousEnergy = data.z;
        energies[energiesIndex] = previousEnergy;
        energiesIndex++;

        if (energiesIndex == energies.Length)
        {
            energiesIndex = 0;
        }
    }
    
    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        if (!freezeSimulation && !FileHelper.IsReplaying)
        {
            astronomicalSimulator.UpdateMasses();
        }
        
        if (FileHelper.IsReplaying && !freezeSimulation)
        {
            FileHelper.UpdateStateReplay(1);
            astronomicalRenderer.SetBuffers();
            Debug.Log("Replay update!");
        } 

        if (renderMasses)
        {
            var rt = astronomicalRenderer.RenderMasses(useScreenDimensions
                ? new Vector2Int(Screen.width, Screen.height)
                : textureDimensions, useFadeProcessing);

            Graphics.Blit(rt, destination);
        }
        else
        {
            Graphics.Blit(source, destination);
        }
    }

    private void SaveSimulationStateCallback(bool right, string fileName)
    {
        if (!right) return;
        FileHelper.SaveSimulationState(fileName, astronomicalSimulator);
    }

    private void LoadSimulationStateCallback(bool right, string fileName)
    {
        if (!right) return;
        FileHelper.LoadSimulationState(fileName, astronomicalSimulator);
        astronomicalRenderer.SetBuffers();
    }

    private void SaveScreenshot()
    {
        var rt = astronomicalRenderer.GetRenderTexture();
        var texture = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
        RenderTexture.active = rt;
        texture.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        RenderTexture.active = null;

        FileHelper.SaveScreenshot(texture);
    }
}
