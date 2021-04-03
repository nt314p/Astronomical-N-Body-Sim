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


    [SerializeField] private Vector2Int dimensions = Vector2Int.zero;
    [SerializeField] private int numMasses = 0; // should be a multiple of 128
    [SerializeField] private bool useScreenDimensions = false;
    [SerializeField] private bool useFadeProcessing = false;
    [SerializeField] private float timeStep = 1;

    private RenderTexture renderTexture;

    private PointMass[] masses;
    private Motion[] motions;

    //[SerializeField] private Transform parentCanvas = null;
    //[SerializeField] private GameObject obj;
    //private RectTransform[] massTransforms;

    private ComputeBuffer massesBuffer;
    private ComputeBuffer motionsBuffer;
    private ComputeBuffer readout;

    private int stepSimId;
    private int compEnergyId;
    private int processTextureId;

    private void OnEnable()
    {
        stepSimId = computeShader.FindKernel("StepSimulation");
        compEnergyId = computeShader.FindKernel("ComputeTotalEnergy");
        processTextureId = computeShader.FindKernel("ProcessTexture");

        masses = new PointMass[numMasses];
        motions = new Motion[numMasses];
        //massTransforms = new RectTransform[numMasses];

        for (int i = 0; i < numMasses; i++) // Change point mass creation here
        {
            Vector3 pos = Random.insideUnitCircle * 100;
            pos.z = pos.y;
            pos.y = Random.Range(-2, 2);

            var vel = Vector3.Cross(pos, Vector3.up).normalized * pos.sqrMagnitude * 0.015f; // circular motion
            //pos.x += 1000;

            PointMass p = new PointMass
            {
                Mass = 600000000000,
                Position = pos,
            };

            motions[i].Velocity = vel;
            motions[i].Acceleration = Vector3.zero;
            masses[i] = p;
        }

        /*
        for (int i = 0; i < numMasses; i++)
        {
            massTransforms[i] = Instantiate(obj, parentCanvas).GetComponent<RectTransform>();
            massTransforms[i].localScale = Vector3.one * Mathf.Log(masses[i].mass)/100;
        }*/

        massesBuffer = new ComputeBuffer(numMasses, 16);
        massesBuffer.SetData(masses);

        motionsBuffer = new ComputeBuffer(numMasses, 24);
        motionsBuffer.SetData(motions);

        computeShader.SetFloat("numMasses", numMasses);
        computeShader.SetBuffer(stepSimId, "masses", massesBuffer);
        computeShader.SetBuffer(stepSimId, "motions", motionsBuffer);

        computeShader.SetBuffer(compEnergyId, "masses", massesBuffer);
        computeShader.SetBuffer(compEnergyId, "motions", motionsBuffer);

        readout = new ComputeBuffer(2, 4);
        readout.SetData(new float[] { 0, 0 });
        computeShader.SetBuffer(compEnergyId, "readout", readout);
    }

    private void OnDisable()
    {
        massesBuffer.Release();
        motionsBuffer.Release();
        readout.Release();
        massesBuffer = null;
        motionsBuffer = null;
        readout = null;
    }

    private void UpdateMasses(float deltaTime)
    {
        Matrix4x4 viewToScreen = Matrix4x4.Scale(new Vector3(renderTexture.width, renderTexture.height, 1));
        Matrix4x4 clipToViewportMatrix = Matrix4x4.Translate(Vector3.one * 0.5f) * Matrix4x4.Scale(Vector3.one * 0.5f);
        Matrix4x4 m = viewToScreen * clipToViewportMatrix * cam.projectionMatrix * cam.worldToCameraMatrix;
        // Matrix derived by Wokarol

        computeShader.SetMatrix("worldToScreenMatrix", m); // matrix to convert world space position to screen space
        computeShader.SetFloat("deltaTime", deltaTime);
        computeShader.SetFloat("halfDeltaTime", deltaTime*0.5f);
        computeShader.Dispatch(stepSimId, numMasses / 256, 1, 1); // compute a single step from the simulation
        if (useFadeProcessing)
        {
            computeShader.Dispatch(processTextureId, (renderTexture.width / 32) + 1, (renderTexture.height / 8) + 1, 1);
        }

        //positionsBuffer.GetData(positions); // obtain position data
    }

    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        if (renderTexture == null || !useFadeProcessing)
        {
            renderTexture = useScreenDimensions ?
                new RenderTexture(Screen.width, Screen.height, 24) :
                new RenderTexture(dimensions.x, dimensions.y, 24);

            renderTexture.enableRandomWrite = true;
            renderTexture.Create();
            computeShader.SetTexture(stepSimId, "renderTexture", renderTexture);
            computeShader.SetTexture(processTextureId, "renderTexture", renderTexture);
        }

        UpdateMasses(Time.deltaTime * timeStep) ;

        LogTotalEnergy();

        Graphics.Blit(renderTexture, destination);

        if (!useFadeProcessing)
        {
            renderTexture.Release();
        }
    }

    private void LogTotalEnergy()
    {
        computeShader.Dispatch(compEnergyId, numMasses / 128, 1, 1);
        var readoutArr = new float[2];
        readout.GetData(readoutArr);          // Kinetic             Potential
        TextLogger.Log(Time.time + "," + readoutArr[0] + "," + readoutArr[1]);
        //Debug.Log(readoutArr[0]);
    }

    /*
    private void SetTransforms()
    {
        Matrix4x4 viewToScreen = Matrix4x4.Scale(new Vector3(Screen.width, Screen.height, 1));
        Matrix4x4 clipToViewportMatrix = Matrix4x4.Translate(Vector3.one * 0.5f) * Matrix4x4.Scale(Vector3.one * 0.5f);
        Matrix4x4 m = viewToScreen * clipToViewportMatrix * cam.projectionMatrix * cam.worldToCameraMatrix;

        for (int i = 0; i < numMasses; i++)
        {
            var j = positions[i];
            var pos = m * new Vector4(j.x, j.y, j.z, 1);
            massTransforms[i].gameObject.SetActive(pos.w > 0);
            pos /= pos.w;
            if (i % 100 == 0)
            {
                Debug.Log(pos);
            }
            massTransforms[i].anchoredPosition = pos;
        }
    }*/
}
