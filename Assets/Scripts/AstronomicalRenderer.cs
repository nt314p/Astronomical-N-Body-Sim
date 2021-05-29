using UnityEngine;

public class AstronomicalRenderer
{
    private Camera camera;
    private ComputeShader computeShader;
    private RenderTexture renderTexture;

    private AstronomicalSimulator astronomicalSimulator;

    private int numMasses;
    private int processTextureId;
    private int renderMassesId;

    public AstronomicalRenderer(AstronomicalSimulator astronomicalSimulator, ComputeShader computeShader, Camera camera)
    {
        this.astronomicalSimulator = astronomicalSimulator;
        this.computeShader = computeShader;
        this.camera = camera;

        processTextureId = computeShader.FindKernel("ProcessTexture");
        renderMassesId = computeShader.FindKernel("RenderMasses");

        numMasses = astronomicalSimulator.NumMasses;
        
        computeShader.SetBuffer(renderMassesId, "masses", astronomicalSimulator.MassesBuffer);
        computeShader.SetBuffer(renderMassesId, "motions", astronomicalSimulator.MotionsBuffer);
    }

    public void SetBuffers()
    {
        computeShader.SetBuffer(renderMassesId, "masses", astronomicalSimulator.MassesBuffer);
        computeShader.SetBuffer(renderMassesId, "motions", astronomicalSimulator.MotionsBuffer);
    }

    public RenderTexture RenderMasses(Vector2Int dimensions, bool useFadeProcessing = false)
    {
        if (renderTexture == null || !useFadeProcessing)
        {
            if (renderTexture != null)
                renderTexture.Release();
            
            renderTexture = new RenderTexture(dimensions.x, dimensions.y, 24);

            renderTexture.enableRandomWrite = true;
            renderTexture.Create();
            computeShader.SetTexture(renderMassesId, "renderTexture", renderTexture);
            computeShader.SetTexture(processTextureId, "renderTexture", renderTexture);
        }

        if (useFadeProcessing)
        {
            computeShader.Dispatch(processTextureId, (renderTexture.width / 32) + 1, (renderTexture.height / 8) + 1, 1);
        }

        var viewToScreen = Matrix4x4.Scale(new Vector3(renderTexture.width, renderTexture.height, 1));
        var clipToViewportMatrix = Matrix4x4.Translate(Vector3.one * 0.5f) * Matrix4x4.Scale(Vector3.one * 0.5f);
        var worldToScreenMatrix = viewToScreen * clipToViewportMatrix * camera.projectionMatrix * camera.worldToCameraMatrix;
        // World to screen matrix derived by Wokarol

        computeShader.SetMatrix("worldToScreenMatrix", worldToScreenMatrix); 
        computeShader.SetVector("cameraPosition", camera.transform.position);

        computeShader.Dispatch(renderMassesId, numMasses / 256, 1, 1);

        return renderTexture;
    }
    
    public RenderTexture GetRenderTexture()
    {
        return renderTexture;
    }
}
