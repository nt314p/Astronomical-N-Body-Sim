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
    private readonly int fadeTextureIndex;
    private readonly int renderMassesIndex;
    private readonly int clearTextureIndex;
    private readonly int upsampleIndex;
    private readonly int downsampleIndex;
    private readonly int postProcessTextureIndex;
    private readonly int renderTextureId;

    public AstronomicalRenderer(int numMasses, ComputeShader computeShader, Camera camera, Vector2Int textureDimensions, ComputeBuffer massesBuffer, ComputeBuffer motionsBuffer)
    {
        this.computeShader = computeShader;
        this.camera = camera;

        fadeTextureIndex = computeShader.FindKernel("FadeTexture");
        renderMassesIndex = computeShader.FindKernel("RenderMasses");
        clearTextureIndex = computeShader.FindKernel("ClearTexture");
        upsampleIndex = computeShader.FindKernel("Upsample");
        downsampleIndex = computeShader.FindKernel("Downsample");
        postProcessTextureIndex = computeShader.FindKernel("PostProcessTexture");
        
        renderTextureId = Shader.PropertyToID("renderTexture");

        this.numMasses = numMasses;
        
        SetBuffers(massesBuffer, motionsBuffer);
        
        GenerateMipMaps(textureDimensions);

        RenderTexture = mipmaps[0];
            
        computeShader.SetTexture(clearTextureIndex, renderTextureId, RenderTexture);
        computeShader.SetTexture(renderMassesIndex, renderTextureId, RenderTexture);
        computeShader.SetTexture(fadeTextureIndex, renderTextureId, RenderTexture);
    }

    // TODO: can we reduce calling this? perhaps give FileHelper the astroRenderer as well as the astroSim?
    public void SetBuffers(ComputeBuffer massesBuffer, ComputeBuffer motionsBuffer)
    {
        computeShader.SetBuffer(renderMassesIndex, "masses", massesBuffer);
        computeShader.SetBuffer(renderMassesIndex, "motions", motionsBuffer);
    }

    public RenderTexture RenderMasses(bool useFadeProcessing = false)
    {
        DispatchKernelOnRenderTexture(useFadeProcessing ? fadeTextureIndex : clearTextureIndex);

        var viewToScreen = Matrix4x4.Scale(new Vector3(RenderTexture.width, RenderTexture.height, 1));
        var clipToViewportMatrix = Matrix4x4.Translate(Vector3.one * 0.5f) * Matrix4x4.Scale(Vector3.one * 0.5f);
        var worldToScreenMatrix =
            viewToScreen * clipToViewportMatrix * camera.projectionMatrix * camera.worldToCameraMatrix;
        // World to screen matrix derived by Wokarol

        computeShader.SetMatrix("worldToScreenMatrix", worldToScreenMatrix);
        computeShader.Dispatch(renderMassesIndex, numMasses / 256, 1, 1);

        mipmaps[0] = RenderTexture;
        for (var i = 1; i < Passes; i++) // Downsample
        {
            var srcTexture = mipmaps[i - 1];
            var destTexture = mipmaps[i];

            computeShader.SetTexture(downsampleIndex, "srcTexture", srcTexture);
            computeShader.SetTexture(downsampleIndex, "destTexture", destTexture);
            computeShader.SetVector("textureDimensions", 
                GetInverseTextureDimensions(srcTexture, destTexture));

            computeShader.Dispatch(downsampleIndex, 
                Mathf.CeilToInt(destTexture.width / 32f),
                Mathf.CeilToInt(destTexture.height / 8f), 1);
        }

        for (var i = Passes - 1; i > 0; i--) // Upsample
        {
            var srcTexture = mipmaps[i];
            var destTexture = mipmaps[i - 1];
            
            computeShader.SetTexture(upsampleIndex, "srcTexture", srcTexture);
            computeShader.SetTexture(upsampleIndex, "destTexture", destTexture);
            computeShader.SetVector("textureDimensions",
                GetInverseTextureDimensions(srcTexture, destTexture));
        
            computeShader.Dispatch(upsampleIndex, 
                Mathf.CeilToInt(destTexture.width / 32f),
                Mathf.CeilToInt(destTexture.height / 8f), 1);
        }

        RenderTexture = mipmaps[0];
        computeShader.SetTexture(postProcessTextureIndex, renderTextureId, RenderTexture);
        DispatchKernelOnRenderTexture(postProcessTextureIndex);
        
        RenderTexture = mipmaps[0];
        return mipmaps[0];
    }

    // Dispatches the kernel on the current render texture
    // Kernel must use (32, 8, 1) numthreads
    private void DispatchKernelOnRenderTexture(int kernelIndex)
    {
        computeShader.Dispatch(kernelIndex,
            Mathf.CeilToInt(RenderTexture.width / 32f), 
            Mathf.CeilToInt(RenderTexture.height / 8f), 1);
    }

    private void GenerateMipMaps(Vector2Int dimensions)
    {
        mipmaps = new RenderTexture[BloomMipDepth];
        var width = dimensions.x;
        var height = dimensions.y;
        
        for (var i = 0; i < BloomMipDepth; i++)
        {
            var renderTexture = new RenderTexture(width, height, 0);
            renderTexture.enableRandomWrite = true;
            renderTexture.Create();
            mipmaps[i] = renderTexture;
            width /= 2;
            height /= 2;
        }
    }

    private static Vector4 GetInverseTextureDimensions(Texture rtA, Texture rtB)
    {
        return new Vector4(1.0f / (rtA.width - 1), 1.0f / (rtA.height - 1), 1.0f / (rtB.width - 1),
            1.0f / (rtB.height - 1));
    }
}