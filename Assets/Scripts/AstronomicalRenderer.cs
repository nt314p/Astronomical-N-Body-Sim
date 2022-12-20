using System;
using UnityEngine;

public struct GridCellData
{
    public ushort Offset;
    public ushort Length;
}

public class AstronomicalRenderer
{
    private readonly Camera camera;
    private readonly ComputeShader computeShader;
    private RenderTexture renderTexture;
    private RenderTexture[] mipmaps;

    private readonly AstronomicalSimulator astronomicalSimulator;

    private ComputeBuffer screenPositionsComputeBuffer;
    private Vector2[] screenPositionsBuffer;
    private Vector2[] screenPositionsTempBuffer;

    private GridCellData[,] cellData;
    private uint[] cellDataTextureBuffer;
    private Texture2D cellDataTexture;
    private Vector2Int cellDataDimensions;

    private readonly int numMasses;
    private readonly int processTextureId;
    private readonly int renderMassesId;
    private readonly int clearTextureId;
    private readonly int computePositionsId;
    private readonly int renderStarsId;
    private readonly int minColorSpeedId;
    private readonly int maxColorSpeedId;
    private readonly int upsampleId;
    private readonly int downsampleId;

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
        upsampleId = computeShader.FindKernel("Upsample");
        downsampleId = computeShader.FindKernel("Downsample");
        minColorSpeedId = Shader.PropertyToID("minColorSpeed");
        maxColorSpeedId = Shader.PropertyToID("maxColorSpeed");

        numMasses = astronomicalSimulator.NumMasses;

        computeShader.SetBuffer(renderMassesId, "masses", astronomicalSimulator.MassesBuffer);
        computeShader.SetBuffer(renderMassesId, "motions", astronomicalSimulator.MotionsBuffer);

        computeShader.SetBuffer(computePositionsId, "masses", astronomicalSimulator.MassesBuffer);

        screenPositionsComputeBuffer = new ComputeBuffer(numMasses, 8);
        screenPositionsBuffer = new Vector2[numMasses];
        screenPositionsTempBuffer = new Vector2[numMasses];
        screenPositionsComputeBuffer.SetData(screenPositionsBuffer);
        computeShader.SetBuffer(computePositionsId, "screenPositions", screenPositionsComputeBuffer);
        computeShader.SetBuffer(renderStarsId, "screenPositions", screenPositionsComputeBuffer);
    }

    public void SetBuffers()
    {
        computeShader.SetBuffer(renderMassesId, "masses", astronomicalSimulator.MassesBuffer);
        computeShader.SetBuffer(renderMassesId, "motions", astronomicalSimulator.MotionsBuffer);
    }

    public void ReleaseBuffers()
    {
        screenPositionsComputeBuffer?.Release();
        screenPositionsComputeBuffer = null;
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

            cellDataDimensions.x = renderTexture.width / 32 + 1;
            cellDataDimensions.y = renderTexture.height / 32 + 1;

            cellData = new GridCellData[cellDataDimensions.x, cellDataDimensions.y];
            cellDataTextureBuffer = new uint[cellData.Length];

            cellDataTexture = new Texture2D(cellDataDimensions.x, cellDataDimensions.y, TextureFormat.RFloat, false);

            computeShader.SetTexture(clearTextureId, "renderTexture", renderTexture);
            computeShader.SetTexture(renderMassesId, "renderTexture", renderTexture);
            computeShader.SetTexture(processTextureId, "renderTexture", renderTexture);
            computeShader.SetTexture(renderStarsId, "renderTexture", renderTexture);
            computeShader.SetTexture(renderStarsId, "cellData", cellDataTexture);
        }

        if (mipmaps == null)
        {
            mipmaps = new RenderTexture[7];
            var width = Screen.width;
            var height = Screen.height;
            for (var i = 0; i < mipmaps.Length; i++)
            {
                var rt = new RenderTexture(width, height, 16);
                rt.enableRandomWrite = true;
                rt.Create();
                mipmaps[i] = rt;
                width /= 2;
                height /= 2;

                //computeShader.set
                //computeShader.SetTexture(downsampleId, "mipmaps", rt, i);
            }
        }

        if (renderTexture == null || !useFadeProcessing)
        {
            computeShader.Dispatch(clearTextureId, renderTexture.width / 32 + 1, renderTexture.height / 8 + 1, 1);
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
        var worldToScreenMatrix =
            viewToScreen * clipToViewportMatrix * camera.projectionMatrix * camera.worldToCameraMatrix;
        // World to screen matrix derived by Wokarol

        computeShader.SetMatrix("worldToScreenMatrix", worldToScreenMatrix);

        //computeShader.Dispatch(computePositionsId, numMasses / 256, 1, 1);
        //SortScreenPositions();
        //screenPositionsComputeBuffer.SetData(screenPositionsBuffer);
        //computeShader.Dispatch(renderStarsId, cellDataDimensions.x, cellDataDimensions.y, 1);

        computeShader.Dispatch(renderMassesId, numMasses / 256, 1, 1);

        mipmaps[0] = renderTexture;
        for (var i = 1; i < mipmaps.Length; i++)
        {
            var destTexture = mipmaps[i];

            computeShader.SetTexture(downsampleId, "srcTexture", mipmaps[i - 1]);
            computeShader.SetTexture(downsampleId, "destTexture", destTexture);

            computeShader.Dispatch(downsampleId, destTexture.width / 32 + 1, destTexture.height / 8 + 1, 1);
        }

        for (var i = mipmaps.Length - 1; i > 0; i--)
        {
            var destTexture = mipmaps[i - 1];

            computeShader.SetTexture(upsampleId, "srcTexture", mipmaps[i]);
            computeShader.SetTexture(upsampleId, "destTexture", destTexture);

            computeShader.Dispatch(upsampleId, destTexture.width / 32 + 1, destTexture.height / 8 + 1, 1);
        }

        var sel = (int)Time.time / 2;
        sel %= 6;
        return mipmaps[0];
    }

    private void SortScreenPositions()
    {
        Array.Clear(cellData, 0, cellData.Length);
        Array.Clear(cellDataTextureBuffer, 0, cellDataTextureBuffer.Length);
        screenPositionsComputeBuffer.GetData(screenPositionsBuffer);

        foreach (var position in screenPositionsBuffer)
        {
            var x = (int)position.x / 32;
            var y = (int)position.y / 32;
            if (x < 0 || y < 0) continue;
            if (x >= cellDataDimensions.x || y >= cellDataDimensions.y) continue;
            cellData[x, y].Length++;
        }

        ushort offset = 0;

        for (var y = 0; y < cellData.GetLength(1); y++)
        {
            for (var x = 0; x < cellData.GetLength(0); x++)
            {
                cellData[x, y].Offset = offset;
                offset += cellData[x, y].Length;
            }
        }

        foreach (var position in screenPositionsBuffer)
        {
            var x = (int)position.x / 32;
            var y = (int)position.y / 32;
            if (x < 0 || y < 0) continue;
            if (x >= cellDataDimensions.x || y >= cellDataDimensions.y) continue;
            var index = cellData[x, y].Offset;
            cellData[x, y].Offset = (ushort)(index + 1);

            screenPositionsTempBuffer[index] = position;
        }

        var temp = screenPositionsBuffer;
        screenPositionsBuffer = screenPositionsTempBuffer;
        screenPositionsTempBuffer = temp;

        for (var y = 0; y < cellData.GetLength(1); y++)
        {
            for (var x = 0; x < cellData.GetLength(0); x++)
            {
                var cell = cellData[x, y];
                var val = (uint)(((cell.Offset - cell.Length) << 16) | cell.Length);
                cellDataTextureBuffer[x + y * cellData.GetLength(0)] = val;
            }
        }

        cellDataTexture.SetPixelData(cellDataTextureBuffer, 0);
        cellDataTexture.Apply(false);
    }

    public RenderTexture GetRenderTexture()
    {
        return renderTexture;
    }
}