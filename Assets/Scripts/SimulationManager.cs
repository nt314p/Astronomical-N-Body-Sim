using System.IO;
using UnityEngine;

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

    private void OnEnable()
    {
        var simulationState = new SimulationState(25600);
        astronomicalSimulator = new AstronomicalSimulator(computeShader, simulationState);
        astronomicalRenderer = new AstronomicalRenderer(astronomicalSimulator, computeShader, cam);
    }

    private void OnDisable()
    {
        astronomicalSimulator.ReleaseBuffers();
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
        
        if (Input.GetKeyDown((KeyCode.F1)))
        {
            SaveScreenshot();
        }

        if (!lockCamera)
        {
            firstPersonCamera.ProcessCamera();
        }
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
