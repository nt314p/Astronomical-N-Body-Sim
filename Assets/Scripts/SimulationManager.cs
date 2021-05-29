using UnityEngine;

public class SimulationManager : MonoBehaviour
{
    [SerializeField] private DisplayManager displayManager;
    [SerializeField] private ComputeShader computeShader;
    [SerializeField] private Camera cam;
    [SerializeField] private FirstPersonCam firstPersonCamera;
    [SerializeField] private bool useScreenDimensions;
    [SerializeField] private Vector2Int textureDimensions = Vector2Int.zero;
    [SerializeField] private float timeStep = 1;
    [SerializeField] private bool freezeSimulation;
    [SerializeField] private bool useFadeProcessing;
    [SerializeField] private bool renderMasses = true;
    [SerializeField] private bool lockCamera;
    
    private AstronomicalSimulator astronomicalSimulator;
    private AstronomicalRenderer astronomicalRenderer;

    private float previousEnergy = 0;
    private float[] energies = new float[20];
    private int energiesIndex = 0;

    private void OnEnable()
    {
        Application.targetFrameRate = 60;

        var simulationState = new SimulationState(10240);
        astronomicalSimulator = new AstronomicalSimulator(computeShader, simulationState);
        astronomicalRenderer = new AstronomicalRenderer(astronomicalSimulator, computeShader, cam);
    }

    private void OnDisable()
    {
        astronomicalSimulator.ReleaseBuffers(true);
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Application.Quit();
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
            timeStep *= 2;
        }

        if (Input.GetKeyDown(KeyCode.Comma))
        {
            timeStep *= 0.5f;
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

        astronomicalSimulator.GetTotalEnergy();

        //LogEnergies();

        // LoadSimulationState();
        // SaveSimulationState();
        //TextLogger.Log($"{Time.time},{data.z}");
    }

    private void LogEnergies()
    {
        var data = astronomicalSimulator.GetTotalEnergy();
        Debug.Log("Per: " + (data.z - previousEnergy) * 100 / timeStep / data.z);
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
            astronomicalSimulator.UpdateMasses(Time.fixedDeltaTime * timeStep);
        }
        
        if (FileHelper.IsReplaying && !freezeSimulation)
        {
            FileHelper.UpdateStateReplay(1);
            astronomicalRenderer.SetBuffers();
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

    private void SaveSimulationState()
    {
        FileHelper.SaveSimulationState(astronomicalSimulator);
    }

    private void LoadSimulationState()
    {
        FileHelper.LoadSimulationState(astronomicalSimulator);
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
