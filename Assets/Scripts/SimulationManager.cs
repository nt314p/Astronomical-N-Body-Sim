using System.IO;
using UnityEngine;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.UnityConverters;
using Newtonsoft.Json.UnityConverters.Math;

public class SimulationManager : MonoBehaviour
{
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
    private JsonSerializerSettings jsonSettings;

    private void OnEnable()
    {
        jsonSettings = new JsonSerializerSettings {
            Converters = new JsonConverter[] {
                new Vector3Converter(),
                new StringEnumConverter(),
            },
            ContractResolver = new UnityTypeContractResolver(),
        };
        
        var simulationState = new SimulationState(12800);
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
        }

        if (Input.GetKeyDown(KeyCode.I))
        {
            LoadSimulationState();
        }

        if (Input.GetKeyDown(KeyCode.O))
        {
            SaveSimulationState();
        }
        
        if (Input.GetKeyDown((KeyCode.F1)))
        {
            SaveScreenshot();
        }

        if (!lockCamera)
        {
            firstPersonCamera.ProcessCamera();
        }

        var data = astronomicalSimulator.GetTotalEnergy();
        Debug.Log(data.z);
    }
    
    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        if (!freezeSimulation)
        {
            astronomicalSimulator.UpdateMasses(Time.deltaTime * timeStep);
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
        var simulationState = astronomicalSimulator.GetSimulationState();
        var json = JsonConvert.SerializeObject(simulationState.StateMasses, jsonSettings);
        var path = "Assets/Resources/saved_state.json";
        var writer = new StreamWriter(path, false);
        writer.WriteLine(json);
        writer.Close();
        Debug.Log("Saved simulation state");
    }

    private void LoadSimulationState()
    {
        var path = "Assets/Resources/saved_state.json";
        var reader = new StreamReader(path);
        var pointMassStates = JsonConvert.DeserializeObject<PointMassState[]>(reader.ReadToEnd());
        astronomicalSimulator.SetSimulationState(new SimulationState(pointMassStates));
        astronomicalRenderer.SetBuffers();
        Debug.Log("Loaded simulation state");
    }

    private void SaveScreenshot()
    {
        var rt = astronomicalRenderer.GetRenderTexture();
        var texture = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
        RenderTexture.active = rt;
        texture.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        RenderTexture.active = null;

        var bytes = texture.EncodeToPNG();
        var maxScreenshotNum = -1;
        
        var files = Directory.GetFiles("Screenshots/", "screenshot*.png");

        foreach (var file in files)
        {
            var numStr = file.Substring(22, file.Length - 26);
            if (int.TryParse(numStr, out var screenshotNum))
            {
                if (screenshotNum > maxScreenshotNum)
                    maxScreenshotNum = screenshotNum;
            }
        }

        maxScreenshotNum++;
        
        var path = "Screenshots/screenshot" + maxScreenshotNum + ".png";
        File.WriteAllBytes(path, bytes);
        Debug.Log("Took screenshot!");
    }
}
