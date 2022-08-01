using UnityEngine;

public class AstronomicalRenderer
{
    private readonly Camera camera;
    private readonly ComputeShader computeShader;
    private RenderTexture renderTexture;

    private readonly AstronomicalSimulator astronomicalSimulator;

    public ComputeBuffer ScreenPositionsComputeBuffer;

    private readonly int numMasses;
    private readonly int processTextureId;
    private readonly int renderMassesId;
    private readonly int clearTextureId;
    private readonly int computePositionsId;
    private readonly int renderStarsId;
    private readonly int minColorSpeedId;
    private readonly int maxColorSpeedId;

    public AstronomicalRenderer(AstronomicalSimulator astronomicalSimulator, ComputeShader computeShader, Camera camera)
    {
        this.astronomicalSimulator = astronomicalSimulator;
        this.computeShader = computeShader;
        this.camera = camera;

        processTextureId = computeShader.FindKernel("ProcessTexture");
        renderMassesId = computeShader.FindKernel("RenderMasses");
        clearTextureId = computeShader.FindKernel("ClearTexture");
        computePositionsId = computeShader.FindKernel("ComputePositions");
        renderStarsId = computeShader.FindKernel("RenderStars");
        minColorSpeedId = Shader.PropertyToID("minColorSpeed");
        maxColorSpeedId = Shader.PropertyToID("maxColorSpeed");


        numMasses = astronomicalSimulator.NumMasses;
        
        computeShader.SetBuffer(renderMassesId, "masses", astronomicalSimulator.MassesBuffer);
        computeShader.SetBuffer(renderMassesId, "motions", astronomicalSimulator.MotionsBuffer);

        computeShader.SetBuffer(computePositionsId, "masses", astronomicalSimulator.MassesBuffer);

        ScreenPositionsComputeBuffer = new ComputeBuffer(numMasses, 8);
        ScreenPositionsComputeBuffer.SetData(new Vector2[numMasses]);
        computeShader.SetBuffer(computePositionsId, "screenPositions", ScreenPositionsComputeBuffer);
        computeShader.SetBuffer(renderStarsId, "screenPositions", ScreenPositionsComputeBuffer);
    }

    public void SetBuffers()
    {
        computeShader.SetBuffer(renderMassesId, "masses", astronomicalSimulator.MassesBuffer);
        computeShader.SetBuffer(renderMassesId, "motions", astronomicalSimulator.MotionsBuffer);
    }

    public void ReleaseBuffers()
    {
        ScreenPositionsComputeBuffer?.Release();
        ScreenPositionsComputeBuffer = null;
    }

    public void SetMinColorSpeed(float minSpeed)
    {
        computeShader.SetFloat(minColorSpeedId, minSpeed);
    }
    
    public void SetMaxColorSpeed(float maxSpeed)
    {
        computeShader.SetFloat(maxColorSpeedId, maxSpeed);
    }

    public RenderTexture RenderMasses(Vector2Int dimensions, bool useFadeProcessing = false)
    {
        if (renderTexture == null)
        {
            renderTexture = new RenderTexture(dimensions.x, dimensions.y, 16);
            renderTexture.enableRandomWrite = true;
            renderTexture.Create();
            computeShader.SetTexture(clearTextureId, "renderTexture", renderTexture);
            computeShader.SetTexture(renderMassesId, "renderTexture", renderTexture);
            computeShader.SetTexture(processTextureId, "renderTexture", renderTexture);
            computeShader.SetTexture(renderStarsId, "renderTexture", renderTexture);
        }
        
        if (renderTexture == null || !useFadeProcessing)
        {
            computeShader.Dispatch(clearTextureId, (renderTexture.width / 32) + 1, (renderTexture.height / 8) + 1, 1);
            //GL.Clear(true, true, Color.black);
            // if (renderTexture != null)
            //     renderTexture.Release();
            
            //renderTexture = new RenderTexture(dimensions.x, dimensions.y, 0);

            // renderTexture.enableRandomWrite = true;
            // renderTexture.Create();
            // computeShader.SetTexture(renderMassesId, "renderTexture", renderTexture);
            // computeShader.SetTexture(processTextureId, "renderTexture", renderTexture);
        }

        if (useFadeProcessing)
        {
            computeShader.Dispatch(processTextureId, renderTexture.width / 32 + 1, renderTexture.height / 8 + 1, 1);
        }

        var viewToScreen = Matrix4x4.Scale(new Vector3(renderTexture.width, renderTexture.height, 1));
        var clipToViewportMatrix = Matrix4x4.Translate(Vector3.one * 0.5f) * Matrix4x4.Scale(Vector3.one * 0.5f);
        var worldToScreenMatrix = viewToScreen * clipToViewportMatrix * camera.projectionMatrix * camera.worldToCameraMatrix;
        // World to screen matrix derived by Wokarol

        computeShader.SetMatrix("worldToScreenMatrix", worldToScreenMatrix); 
        computeShader.SetVector("cameraPosition", camera.transform.position);

        computeShader.Dispatch(computePositionsId, numMasses / 256, 1, 1);

        //computeShader.Dispatch(renderMassesId, numMasses / 256, 1, 1);
        computeShader.Dispatch(renderStarsId, renderTexture.width / 32 + 1, renderTexture.height / 32 + 1, 1);

        return renderTexture;
    }
    
    public RenderTexture GetRenderTexture()
    {
        return renderTexture;
    }
}
