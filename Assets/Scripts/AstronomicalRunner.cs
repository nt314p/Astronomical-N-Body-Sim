using UnityEngine;

public struct PointMass
{
    public float Mass;
    public Vector3 Position;
};

public struct Motion
{
    public Vector3 Velocity;
    public Vector3 Acceleration;
};

public class AstronomicalRunner : MonoBehaviour
{
    [SerializeField] private ComputeShader computeShader = null;
    [SerializeField] private Camera cam = null;

    [SerializeField] private int numMasses = 0; // should be a multiple of 256
    [SerializeField] private bool useFadeProcessing = false;

    private RenderTexture renderTexture;

    private PointMass[] masses;
    private Motion[] motions;

    private ComputeBuffer massesBuffer;
    private ComputeBuffer motionsBuffer;
    private ComputeBuffer readoutBuffer;

    private int stepSimId;
    private int compEnergyId;
    private int processTextureId;
    private int renderMassesId;

    public void Initialize()
    {
        stepSimId = computeShader.FindKernel("StepSimulation");
        compEnergyId = computeShader.FindKernel("ComputeTotalEnergy");
        processTextureId = computeShader.FindKernel("ProcessTexture");
        renderMassesId = computeShader.FindKernel("RenderMasses");

        masses = new PointMass[numMasses];
        motions = new Motion[numMasses];

        for (int i = 0; i < numMasses; i++) // Change point mass creation here
        {
            Vector3 pos = Random.insideUnitCircle * 200;
            pos.z = pos.y;
            pos.y = Random.Range(-10, 10);

            var vel = Vector3.Cross(pos, Vector3.up).normalized * pos.sqrMagnitude * 0.0035f; // circular motion
            //pos.x += 1000;

            PointMass p = new PointMass
            {
                Mass = 1000000000000,
                Position = pos,
            };

            motions[i].Velocity = vel;
            motions[i].Acceleration = Vector3.zero;
            masses[i] = p;
        }

        massesBuffer = new ComputeBuffer(numMasses, 16);
        massesBuffer.SetData(masses);

        motionsBuffer = new ComputeBuffer(numMasses, 24);
        motionsBuffer.SetData(motions);

        computeShader.SetInt("numMasses", numMasses);
        computeShader.SetBuffer(stepSimId, "masses", massesBuffer);
        computeShader.SetBuffer(stepSimId, "motions", motionsBuffer);

        computeShader.SetBuffer(compEnergyId, "masses", massesBuffer);
        computeShader.SetBuffer(compEnergyId, "motions", motionsBuffer);

        computeShader.SetBuffer(renderMassesId, "masses", massesBuffer);
        computeShader.SetBuffer(renderMassesId, "motions", motionsBuffer);

        readoutBuffer = new ComputeBuffer(numMasses, 8);
        readoutBuffer.SetData(new Vector2[numMasses]);
        computeShader.SetBuffer(compEnergyId, "readout", readoutBuffer);
    }

    public void ReleaseBuffers()
    {
        massesBuffer.Release();
        motionsBuffer.Release();
        readoutBuffer.Release();
        massesBuffer = null;
        motionsBuffer = null;
        readoutBuffer = null;
    }

    public void UpdateMasses(float deltaTime) // compute a single step from the simulation
    {
        computeShader.SetFloat("deltaTime", deltaTime);
        computeShader.SetFloat("halfDeltaTime", deltaTime * 0.5f);
        computeShader.Dispatch(stepSimId, numMasses / 256, 1, 1); 
    }

    public RenderTexture RenderMasses(Vector2Int dimensions)
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
        var worldToScreenMatrix = viewToScreen * clipToViewportMatrix * cam.projectionMatrix * cam.worldToCameraMatrix;
        // World to screen matrix derived by Wokarol

        computeShader.SetMatrix("worldToScreenMatrix", worldToScreenMatrix); 
        computeShader.SetVector("cameraPosition", cam.transform.position);

        computeShader.Dispatch(renderMassesId, numMasses / 128, 1, 1);

        return renderTexture;
    }
    
    public RenderTexture GetRenderTexture()
    {
        return renderTexture;
    }

    public Vector3 GetTotalEnergy()
    {
        computeShader.Dispatch(compEnergyId, numMasses / 128, 1, 1);
        var readoutArr = new Vector2[numMasses];
        readoutBuffer.GetData(readoutArr);

        var total = Vector2.zero;

        for (var i = 0; i < numMasses; i++)
        {
            total += readoutArr[i];
        }

        return new Vector3(total.x, total.y, total.x + total.y); // Kinetic, Potential, Total
    }
}
