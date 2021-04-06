using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEngine.Serialization;

public class SimulationManager : MonoBehaviour
{
    [SerializeField] private AstronomicalRunner astroRunner;
    [SerializeField] private bool useScreenDimensions = false;
    [SerializeField] private Vector2Int textureDimensions = Vector2Int.zero;
    [SerializeField] private float timeStep = 1;

    [SerializeField] private bool freezeSimulation = false;
    [SerializeField] private bool renderMasses = true;

    private void OnEnable()
    {
        astroRunner.Initialize();
    }

    private void OnDisable()
    {
        astroRunner.ReleaseBuffers();
    }

    // Update is called once per frame
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
        if (Input.GetKeyDown((KeyCode.F1)))
        {
            SaveScreenshot();
        }
    }
    
    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        if (!freezeSimulation)
        {
            astroRunner.UpdateMasses(Time.deltaTime * timeStep);
        }

        if (renderMasses)
        {
            var rt = astroRunner.RenderMasses(useScreenDimensions
                ? new Vector2Int(Screen.width, Screen.height)
                : textureDimensions);

            Graphics.Blit(rt, destination);
        }
    }

    private void SaveScreenshot()
    {
        var rt = astroRunner.GetRenderTexture();
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
