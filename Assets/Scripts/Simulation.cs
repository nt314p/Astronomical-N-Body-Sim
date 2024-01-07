using System;
using System.IO;
using UnityEngine;
using Cursor = UnityEngine.Cursor;

public class Simulation : MonoBehaviour
{
    [SerializeField] private DisplayManager displayManager;
    [SerializeField] private Prompt prompt;
    [SerializeField] private ComputeShader computeShader;
    [SerializeField] private Camera cam;
    [SerializeField] private int Passes = 10;
    [SerializeField] private bool useScreenDimensions;
    [SerializeField] private Vector2Int textureDimensions = Vector2Int.zero;
    [SerializeField] private bool simulationPaused;
    [SerializeField] private bool useFadeProcessing;
    [SerializeField] private bool lockCamera;
    
    private bool SimulationPaused
    {
        get => simulationPaused;
        set
        {
            simulationPaused = value;
            if (FileHelper.IsReplaying)
            {
                var direction = FileHelper.ReplayStep >= 0 ? "forward" : "reverse";
                displayManager.SetMessage(simulationPaused ? "Replay paused" : $"Replay replaying ({direction})");
            }
            else if (FileHelper.IsRecording)
            {
                displayManager.SetMessage(simulationPaused ? "Recording of simulation paused" : "Recording simulation");
            }
            else
            {
                displayManager.SetMessage(simulationPaused ? "Simulation paused" : "Simulation simulating");
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
        var simulationState = new SimulationState(10240);
        SetSimulationState(simulationState, 0.01f); // TODO: match timestep
        displayManager.UpdateTimeStepText(astronomicalSimulator.TimeStep);
    }

    private void OnDisable()
    {
        Debug.Log("SimulationManager disabled, releasing buffers");
        astronomicalSimulator.ReleaseBuffers(true);
    }

    private void Update()
    {
        astronomicalRenderer.Passes = Passes;
        
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            prompt.ShowPrompt("Really quit?", "Yes", "No", QuitPromptCallback);
        }
        
        if (FileHelper.IsRecording && !SimulationPaused)
        {
            FileHelper.UpdateStateRecording();
        }

        if (prompt.IsPrompting || displayManager.IsEditingAny) return;

        if (Input.GetKeyDown(KeyCode.F))
        {
            if (FileHelper.IsReplaying && SimulationPaused && FileHelper.ReplayStep == 0)
            {
                FileHelper.ReplayStep = 1;
                displayManager.UpdateTimeStepTextMultiplier(FileHelper.ReplayStep);
            }
            SimulationPaused = !SimulationPaused;
        }

        if (Input.GetKeyDown(KeyCode.L))
        {
            lockCamera = !lockCamera;
            Cursor.lockState = lockCamera ? CursorLockMode.None : CursorLockMode.Locked;
        }

        if (Input.GetKeyDown(KeyCode.F7)) // Save simulation state
        {
            prompt.ShowPromptWithInput("Enter state file name to save:", "Save", "Cancel", SaveSimulationStateCallback);
        }

        if (Input.GetKeyDown(KeyCode.F8)) // Load simulation state
        {
            prompt.ShowPromptWithInput("Enter state file name to load:", "Load", "Cancel", LoadSimulationStateCallback);
        }

        if (Input.GetKeyDown(KeyCode.H)) displayManager.ToggleHelp();
        if (Input.GetKeyDown(KeyCode.K)) displayManager.ToggleSettingsMenu();
        if (Input.GetKeyDown(KeyCode.F2)) displayManager.ToggleUI();

        if (Input.GetKeyDown(KeyCode.F1))
        {
            SaveScreenshot();
            displayManager.SetMessage("Took screenshot");
        }

        if (Input.GetKeyDown(KeyCode.Period))
        {
            if (FileHelper.IsReplaying)
            {
                if (!SimulationPaused || FileHelper.ReplayStep == 0) FileHelper.ReplayStep += 1;
                SimulationPaused = FileHelper.ReplayStep == 0;
                displayManager.UpdateTimeStepTextMultiplier(FileHelper.ReplayStep);
            }
            else
            {
                astronomicalSimulator.TimeStep *= 2;
                displayManager.UpdateTimeStepText(astronomicalSimulator.TimeStep);
                SimulationPaused = false;
            }
        }

        if (Input.GetKeyDown(KeyCode.Comma))
        {
            SimulationPaused = false; // TODO: why is this only on comma but not period?
            if (FileHelper.IsReplaying)
            {
                if (!SimulationPaused || FileHelper.ReplayStep == 0) FileHelper.ReplayStep -= 1;
                SimulationPaused = FileHelper.ReplayStep == 0;
                displayManager.UpdateTimeStepTextMultiplier(FileHelper.ReplayStep);
            }
            else
            {
                astronomicalSimulator.TimeStep *= 0.5f;
                displayManager.UpdateTimeStepText(astronomicalSimulator.TimeStep);
                SimulationPaused = false;
            }
        }

        if (Input.GetKeyDown(KeyCode.F9)) ToggleRecording();
        if (Input.GetKeyDown(KeyCode.F10)) ToggleReplay();

        //astronomicalSimulator.GetTotalEnergy();
        //LogEnergies();
    }

    private void ToggleRecording()
    {
        if (!FileHelper.IsRecording)
        {
            try
            {
                prompt.ShowPromptWithInput("Enter recording file name:", "Record", "Cancel", StartRecordingCallback);
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

    private void ToggleReplay()
    {
        if (!FileHelper.IsReplaying && !FileHelper.IsRecording)
        {
            try
            {
                prompt.ShowPromptWithInput("Enter stream file name to replay:", "Replay", "Cancel", StartReplayCallback);
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
            simulationPaused = true;
            displayManager.SetTimeStepReadOnly(false);
        }
    }

    private static void QuitPromptCallback()
    {
        FileHelper.CloseFiles();
        Application.Quit();
    }
    
    private void SaveSimulationStateCallback(string fileName)
    {
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

    private void LoadSimulationStateCallback(string fileName)
    {
        try
        {
            FileHelper.LoadSimulationState(fileName, astronomicalSimulator);
            astronomicalRenderer.SetBuffers(astronomicalSimulator.MassesBuffer, astronomicalSimulator.MotionsBuffer);
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

    private void StartRecordingCallback(string fileName)
    {
        FileHelper.StartStateRecording(fileName, astronomicalSimulator);
        displayManager.SetMessage("Started recording");
        displayManager.SetTimeStepReadOnly(true);
    }

    private void StartReplayCallback(string fileName)
    {
        try
        {
            FileHelper.StartStateReplay(fileName, astronomicalSimulator);
            astronomicalRenderer.SetBuffers(astronomicalSimulator.MassesBuffer, astronomicalSimulator.MotionsBuffer);
            FileHelper.ReplayStep = 0;
            displayManager.UpdateTimeStepTextMultiplier(FileHelper.ReplayStep);
            simulationPaused = true;
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
        astronomicalSimulator?.ReleaseBuffers(true);
        astronomicalSimulator = new AstronomicalSimulator(computeShader, simulationState);
        astronomicalRenderer = new AstronomicalRenderer(astronomicalSimulator.NumMasses, computeShader, cam,
            useScreenDimensions ? new Vector2Int(Screen.width, Screen.height) : textureDimensions,
            astronomicalSimulator.MassesBuffer, astronomicalSimulator.MotionsBuffer);
        astronomicalSimulator.TimeStep = timeStep;
    }

    public void SetTimeStep(float timeStep)
    {
        astronomicalSimulator.TimeStep = timeStep;
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
        if (!SimulationPaused && !FileHelper.IsReplaying)
        {
            astronomicalSimulator.UpdateMasses();
        }
        
        if (FileHelper.IsReplaying && !SimulationPaused)
        {
            try
            {
                FileHelper.UpdateStateReplay();
                astronomicalRenderer.SetBuffers(astronomicalSimulator.MassesBuffer, astronomicalSimulator.MotionsBuffer);
            }
            catch (ArgumentOutOfRangeException)
            {
                displayManager.SetMessage("Reached beginning of replay file");
            }
            catch (InvalidOperationException)
            {
                displayManager.SetMessage("Reached end of replay file");
            }
        }

        var rt = astronomicalRenderer.RenderMasses(useFadeProcessing);

        Graphics.Blit(rt, destination);
    }

    private void SaveScreenshot()
    {
        var rt = astronomicalRenderer.RenderTexture;
        var texture = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
        RenderTexture.active = rt;
        texture.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        RenderTexture.active = null;

        FileHelper.SaveScreenshot(texture);
    }
}
