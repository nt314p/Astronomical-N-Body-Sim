using UnityEngine;

public class AstronomicalRenderer
{
    private readonly Camera camera;
    private readonly ComputeShader computeShader;
    private RenderTexture renderTexture;
    private RenderTexture[] mipmaps;

    private readonly AstronomicalSimulator astronomicalSimulator;

    private const int BloomMipDepth = 10;

    public int Passes = 10;

    private readonly int numMasses;
    private readonly int processTextureId;
    private readonly int renderMassesId;
    private readonly int clearTextureId;
    private readonly int minColorSpeedId;
    private readonly int maxColorSpeedId;
    private readonly int upsampleId;
    private readonly int downsampleId;
    private readonly int postProcessTextureId;

    public AstronomicalRenderer(AstronomicalSimulator astronomicalSimulator, ComputeShader computeShader, Camera camera)
    {
        this.astronomicalSimulator = astronomicalSimulator;
        this.computeShader = computeShader;
        this.camera = camera;

        processTextureId = computeShader.FindKernel("ProcessTexture");
        renderMassesId = computeShader.FindKernel("RenderMasses");
        clearTextureId = computeShader.FindKernel("ClearTexture");
        upsampleId = computeShader.FindKernel("Upsample");
        downsampleId = computeShader.FindKernel("Downsample");
        minColorSpeedId = Shader.PropertyToID("minColorSpeed");
        maxColorSpeedId = Shader.PropertyToID("maxColorSpeed");
        postProcessTextureId = computeShader.FindKernel("PostProcessTexture");

        numMasses = astronomicalSimulator.NumMasses;

        computeShader.SetBuffer(renderMassesId, "masses", astronomicalSimulator.MassesBuffer);
        computeShader.SetBuffer(renderMassesId, "motions", astronomicalSimulator.MotionsBuffer);
    }

    public void SetBuffers()
    {
        computeShader.SetBuffer(renderMassesId, "masses", astronomicalSimulator.MassesBuffer);
        computeShader.SetBuffer(renderMassesId, "motions", astronomicalSimulator.MotionsBuffer);
    }

    public void ReleaseBuffers()
    {

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
        }

        if (mipmaps == null)
        {
            mipmaps = new RenderTexture[BloomMipDepth];
            var width = Screen.width;
            var height = Screen.height;
            
            for (var i = 0; i < BloomMipDepth; i++)
            {
                var rt = new RenderTexture(width, height, 16);
                rt.enableRandomWrite = true;
                rt.Create();
                mipmaps[i] = rt;
                width /= 2;
                height /= 2;
            }
        }

        if (renderTexture == null || !useFadeProcessing)
        {
            computeShader.Dispatch(clearTextureId, 
                Mathf.CeilToInt(renderTexture.width / 32f), 
                Mathf.CeilToInt(renderTexture.height / 8f), 1);
        }

        if (useFadeProcessing)
        {
            computeShader.Dispatch(processTextureId, 
                Mathf.CeilToInt(renderTexture.width / 32f),
                Mathf.CeilToInt(renderTexture.height / 8f), 1);
        }

        var viewToScreen = Matrix4x4.Scale(new Vector3(renderTexture.width, renderTexture.height, 1));
        var clipToViewportMatrix = Matrix4x4.Translate(Vector3.one * 0.5f) * Matrix4x4.Scale(Vector3.one * 0.5f);
        var worldToScreenMatrix =
            viewToScreen * clipToViewportMatrix * camera.projectionMatrix * camera.worldToCameraMatrix;
        // World to screen matrix derived by Wokarol

        computeShader.SetMatrix("worldToScreenMatrix", worldToScreenMatrix);
        computeShader.Dispatch(renderMassesId, numMasses / 256, 1, 1);

        mipmaps[0] = renderTexture;
        for (var i = 1; i < Passes; i++) // Downsample
        {
            var srcTexture = mipmaps[i - 1];
            var destTexture = mipmaps[i];

            computeShader.SetTexture(downsampleId, "srcTexture", srcTexture);
            computeShader.SetTexture(downsampleId, "destTexture", destTexture);
            computeShader.SetVector("textureDimensions", GetInverseTextureDimensions(srcTexture, destTexture));

            computeShader.Dispatch(downsampleId, 
                Mathf.CeilToInt(destTexture.width / 32f),
                Mathf.CeilToInt(destTexture.height / 8f), 1);
        }

        for (var i = Passes - 1; i > 0; i--) // Upsample
        {
            var srcTexture = mipmaps[i];
            var destTexture = mipmaps[i - 1];
            
            computeShader.SetTexture(upsampleId, "srcTexture", srcTexture);
            computeShader.SetTexture(upsampleId, "destTexture", destTexture);
            computeShader.SetVector("textureDimensions",
                GetInverseTextureDimensions(srcTexture, destTexture));
        
            computeShader.Dispatch(upsampleId, 
                Mathf.CeilToInt(destTexture.width / 32f),
                Mathf.CeilToInt(destTexture.height / 8f), 1);
        }

        renderTexture = mipmaps[0];
        computeShader.SetTexture(postProcessTextureId, "renderTexture", renderTexture);
        computeShader.Dispatch(postProcessTextureId, 
            Mathf.CeilToInt(renderTexture.width / 32f), 
            Mathf.CeilToInt(renderTexture.height / 8f), 1);
        
        return mipmaps[0];
    }

    private static Vector4 GetInverseTextureDimensions(Texture rtA, Texture rtB)
    {
        return new Vector4(1.0f / (rtA.width - 1), 1.0f / (rtA.height - 1), 1.0f / (rtB.width - 1),
            1.0f / (rtB.height - 1));
    }

    public RenderTexture GetRenderTexture()
    {
        return renderTexture;
    }
}