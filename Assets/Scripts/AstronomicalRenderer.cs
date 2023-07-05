using UnityEngine;

public class AstronomicalRenderer
{
    private readonly Camera camera;
    private readonly ComputeShader computeShader;
    public RenderTexture RenderTexture { get; private set; }
    private RenderTexture[] mipmaps;

    private readonly AstronomicalSimulator astronomicalSimulator;

    private const int BloomMipDepth = 10;
    public int Passes = 10;

    private readonly int numMasses;
    private readonly int fadeTextureId;
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

        fadeTextureId = computeShader.FindKernel("FadeTexture");
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
        if (RenderTexture == null)
        {
            RenderTexture = new RenderTexture(dimensions.x, dimensions.y, 16);
            RenderTexture.useMipMap = true;
            RenderTexture.enableRandomWrite = true;
            RenderTexture.Create();
            
            computeShader.SetTexture(clearTextureId, "renderTexture", RenderTexture);
            computeShader.SetTexture(renderMassesId, "renderTexture", RenderTexture);
            computeShader.SetTexture(fadeTextureId, "renderTexture", RenderTexture);
        }

        if (mipmaps == null)
        {
            mipmaps = new RenderTexture[BloomMipDepth]; // TODO: unify creation of render texture
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

        if (RenderTexture == null || !useFadeProcessing)
        {
            computeShader.Dispatch(clearTextureId, 
                Mathf.CeilToInt(RenderTexture.width / 32f), 
                Mathf.CeilToInt(RenderTexture.height / 8f), 1);
        }

        if (useFadeProcessing)
        {
            computeShader.Dispatch(fadeTextureId, 
                Mathf.CeilToInt(RenderTexture.width / 32f),
                Mathf.CeilToInt(RenderTexture.height / 8f), 1);
        }

        var viewToScreen = Matrix4x4.Scale(new Vector3(RenderTexture.width, RenderTexture.height, 1));
        var clipToViewportMatrix = Matrix4x4.Translate(Vector3.one * 0.5f) * Matrix4x4.Scale(Vector3.one * 0.5f);
        var worldToScreenMatrix =
            viewToScreen * clipToViewportMatrix * camera.projectionMatrix * camera.worldToCameraMatrix;
        // World to screen matrix derived by Wokarol

        computeShader.SetMatrix("worldToScreenMatrix", worldToScreenMatrix);
        computeShader.Dispatch(renderMassesId, numMasses / 256, 1, 1);

        mipmaps[0] = RenderTexture;
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

        RenderTexture = mipmaps[0];
        computeShader.SetTexture(postProcessTextureId, "renderTexture", RenderTexture);
        computeShader.Dispatch(postProcessTextureId, 
            Mathf.CeilToInt(RenderTexture.width / 32f), 
            Mathf.CeilToInt(RenderTexture.height / 8f), 1);
        RenderTexture = mipmaps[0];
        return mipmaps[0];
    }

    private static Vector4 GetInverseTextureDimensions(Texture rtA, Texture rtB)
    {
        return new Vector4(1.0f / (rtA.width - 1), 1.0f / (rtA.height - 1), 1.0f / (rtB.width - 1),
            1.0f / (rtB.height - 1));
    }
}