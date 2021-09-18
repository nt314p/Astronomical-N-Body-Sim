using System;
using System.IO;
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
    
    private bool FreezeSimulation
    {
        get => freezeSimulation;
        set
        {
            freezeSimulation = value;
            if (FileHelper.IsReplaying)
            {
                string direction = FileHelper.ReplayStep >= 0 ? "forward" : "reverse";
                displayManager.SetMessage(freezeSimulation ? "Replay paused" : $"Replay replaying ({direction})");
            }
            else if (FileHelper.IsRecording)
            {
                displayManager.SetMessage(freezeSimulation ? "Recording of simulation paused" : "Recording simulation");
            }
            else
            {
                displayManager.SetMessage(freezeSimulation ? "Simulation paused" : "Simulation simulating");
            }
        }
    }

    private AstronomicalSimulator astronomicalSimulator;
    private AstronomicalRenderer astronomicalRenderer;

    private float previousEnergy;
    private float[] energies = new float[20];
    private int energiesIndex;

    private void Awake()
    {
        Application.targetFrameRate = 60;
        FileHelper.InitializeDirectories();
        var simulationState = new SimulationState(2048);
        astronomicalSimulator = new AstronomicalSimulator(computeShader, simulationState);
        astronomicalRenderer = new AstronomicalRenderer(astronomicalSimulator, computeShader, cam);
        displayManager.UpdateTimeStepText(astronomicalSimulator.TimeStep);
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
        
        if (FileHelper.IsRecording && !FreezeSimulation)
        {
            FileHelper.UpdateStateRecording();
        }
        
        if (promptManager.IsPrompting || displayManager.IsEditingAny)
        {
            return;
        }
        
        if (!lockCamera)
        {
            firstPersonCamera.ProcessCamera();
        }
        
        if (Input.GetKeyDown(KeyCode.F))
        {
            if (FileHelper.IsReplaying && FreezeSimulation && FileHelper.ReplayStep == 0)
            {
                FileHelper.ReplayStep = 1;
                displayManager.UpdateTimeStepTextMultiplier(FileHelper.ReplayStep);
            }
            FreezeSimulation = !FreezeSimulation;
        }

        if (Input.GetKeyDown(KeyCode.L))
        {
            lockCamera = !lockCamera;
            Cursor.lockState = lockCamera ? CursorLockMode.None : CursorLockMode.Locked;
        }

        if (Input.GetKeyDown(KeyCode.F7)) // Save simulation state
        {
            promptManager.ShowPrompt("Enter state file name to save:", "Cancel", "Save", SaveSimulationStateCallback);
        }

        if (Input.GetKeyDown(KeyCode.F8)) // Load simulation state
        {
            promptManager.ShowPrompt("Enter state file name to load:", "Cancel", "Load", LoadSimulationStateCallback);
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
            displayManager.SetMessage("Took screenshot");
        }

        if (Input.GetKeyDown(KeyCode.F2))
        {
            displayManager.ToggleUI();
        }

        if (Input.GetKeyDown(KeyCode.Period))
        {
            if (FileHelper.IsReplaying)
            {
                if (!FreezeSimulation || FileHelper.ReplayStep == 0) FileHelper.ReplayStep += 1;
                FreezeSimulation = FileHelper.ReplayStep == 0;
                displayManager.UpdateTimeStepTextMultiplier(FileHelper.ReplayStep);
            }
            else
            {
                astronomicalSimulator.TimeStep *= 2;
                displayManager.UpdateTimeStepText(astronomicalSimulator.TimeStep);
                FreezeSimulation = false;
            }
        }

        if (Input.GetKeyDown(KeyCode.Comma))
        {
            FreezeSimulation = false;
            if (FileHelper.IsReplaying)
            {
                if (!FreezeSimulation || FileHelper.ReplayStep == 0) FileHelper.ReplayStep -= 1;
                FreezeSimulation = FileHelper.ReplayStep == 0;
                displayManager.UpdateTimeStepTextMultiplier(FileHelper.ReplayStep);
            }
            else
            {
                astronomicalSimulator.TimeStep *= 0.5f;
                displayManager.UpdateTimeStepText(astronomicalSimulator.TimeStep);
                FreezeSimulation = false;
            }
        }

        if (Input.GetKeyDown(KeyCode.F9))
        {
            if (!FileHelper.IsRecording)
            {
                try
                {
                    promptManager.ShowPrompt("Enter recording file name:", "Cancel", "Record", StartRecordingCallback);
                }
                catch (Exception e)
                {
                    displayManager.SetMessage(e.Message);
                }
            }
            else
            {
                FileHelper.EndStateRecording();
                displayManager.SetMessage("Ended recording");
                displayManager.SetTimeStepReadOnly(false);
            }
        }

        if (Input.GetKeyDown(KeyCode.F10))
        {
            if (!FileHelper.IsReplaying && !FileHelper.IsRecording)
            {
                try
                {
                    promptManager.ShowPrompt("Enter stream file name to replay:", "Cancel", "Replay", StartReplayCallback);
                }
                catch (Exception e)
                {
                    displayManager.SetMessage(e.Message);
                }
            }
            else
            {
                FileHelper.EndStateReplay();
                displayManager.SetMessage("Ended replay");
                displayManager.UpdateTimeStepText(astronomicalSimulator.TimeStep);
                freezeSimulation = true;
                displayManager.SetTimeStepReadOnly(false);
            }
        }

        // astronomicalSimulator.GetTotalEnergy();
        //LogEnergies();
        //TextLogger.Log($"{Time.time},{data.z}");
    }

    private void QuitPromptCallback(bool rightButtonClicked, string inputResult)
    {
        if (!rightButtonClicked) return;
        FileHelper.CloseFiles();
        Application.Quit();
    }
    
    private void SaveSimulationStateCallback(bool right, string fileName)
    {
        if (!right) return;
        try
        {
            FileHelper.SaveSimulationState(fileName, astronomicalSimulator);
            displayManager.SetMessage("Saved simulation state");
        }
        catch (Exception)
        {
            displayManager.SetMessage("Unable to save file");
        }
    }

    private void LoadSimulationStateCallback(bool right, string fileName)
    {
        if (!right) return;
        try
        {
            FileHelper.LoadSimulationState(fileName, astronomicalSimulator);
            astronomicalRenderer.SetBuffers();
            displayManager.SetMessage("Loaded simulation state");
        }
        catch (FileNotFoundException)
        {
            displayManager.SetMessage("Unable to find file");
        }
        catch (Exception)
        {
            displayManager.SetMessage("Unable to load file");
        }
    }

    private void StartRecordingCallback(bool right, string fileName)
    {
        if (!right) return;
        FileHelper.StartStateRecording(fileName, astronomicalSimulator);
        displayManager.SetMessage("Started recording");
        displayManager.SetTimeStepReadOnly(true);
    }

    private void StartReplayCallback(bool right, string fileName)
    {
        if (!right) return;
        try
        {
            FileHelper.StartStateReplay(fileName, astronomicalSimulator);
            astronomicalRenderer.SetBuffers();
            FileHelper.ReplayStep = 0;
            displayManager.UpdateTimeStepTextMultiplier(FileHelper.ReplayStep);
            freezeSimulation = true;
            displayManager.SetMessage("Started replay");
            displayManager.SetTimeStepReadOnly(true);
        }
        catch (FileNotFoundException)
        {
            displayManager.SetMessage("Unable to find file");
        }
        catch (Exception)
        {
            displayManager.SetMessage("Unable to replay file");
        }
    }

    public void SetSimulationState(SimulationState simulationState, float timeStep)
    {
        if (FileHelper.IsReplaying)
        {
            FileHelper.EndStateReplay();
        }

        if (FileHelper.IsRecording)
        {
            FileHelper.EndStateRecording();
        }
        astronomicalSimulator.ReleaseBuffers(true);
        astronomicalSimulator = new AstronomicalSimulator(computeShader, simulationState);
        astronomicalRenderer = new AstronomicalRenderer(astronomicalSimulator, computeShader, cam);
        astronomicalSimulator.TimeStep = timeStep;
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
        if (!FreezeSimulation && !FileHelper.IsReplaying)
        {
            astronomicalSimulator.UpdateMasses();
        }
        
        if (FileHelper.IsReplaying && !FreezeSimulation)
        {
            try
            {
                FileHelper.UpdateStateReplay();
                astronomicalRenderer.SetBuffers();
            }
            catch (ArgumentOutOfRangeException)
            {
                displayManager.SetMessage("Reached beginning of replay file");
            }
            catch (InvalidOperationException)
            {
                displayManager.SetMessage("Reached end of replay file");
                Debug.Log("ended");
            }
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

    private void SaveScreenshot()
    {
        var rt = astronomicalRenderer.GetRenderTexture();
        var texture = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
        RenderTexture.active = rt;
        texture.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        RenderTexture.active = null;

        FileHelper.SaveScreenshot(texture);
        displayManager.SetMessage("Took screenshot");
    }
}
