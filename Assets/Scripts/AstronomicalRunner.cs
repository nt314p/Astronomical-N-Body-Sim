using UnityEngine;

public struct PointMass
{
    public float mass;
    public Vector3 position;
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
    private Vector3[] velocities;

    //[SerializeField] private Transform parentCanvas = null;
    //[SerializeField] private GameObject obj;
    //private RectTransform[] massTransforms;

    private ComputeBuffer massesBuffer;
    private ComputeBuffer velocitiesBuffer;
    private ComputeBuffer readout;

    private int StepSimId;
    private int CompEnergyId;
    private int ProcessTextureId;

    private void OnEnable()
    {
        StepSimId = computeShader.FindKernel("StepSimulation");
        CompEnergyId = computeShader.FindKernel("ComputeTotalEnergy");
        ProcessTextureId = computeShader.FindKernel("ProcessTexture");

        masses = new PointMass[numMasses];
        velocities = new Vector3[numMasses];
        //massTransforms = new RectTransform[numMasses];

        for (int i = 0; i < numMasses; i++) // Change point mass creation here
        {
            Vector3 pos = Random.insideUnitCircle * 100;
            pos.z = pos.y;
            pos.y = Random.Range(-2, 2);

            var vel = Vector3.Cross(pos, Vector3.up).normalized * pos.sqrMagnitude * 0.00035f; // circular motion
            //pos.x += 1000;

            PointMass p = new PointMass
            {
                mass = 600000000000,
                position = pos,
            };

            velocities[i] = vel;
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

        velocitiesBuffer = new ComputeBuffer(numMasses, 12);
        velocitiesBuffer.SetData(velocities);

        computeShader.SetFloat("numMasses", numMasses);
        computeShader.SetBuffer(StepSimId, "masses", massesBuffer);
        computeShader.SetBuffer(StepSimId, "velocities", velocitiesBuffer);

        computeShader.SetBuffer(CompEnergyId, "masses", massesBuffer);
        computeShader.SetBuffer(CompEnergyId, "velocities", velocitiesBuffer);

        readout = new ComputeBuffer(1, 4);
        readout.SetData(new float[] { 0 });
        computeShader.SetBuffer(CompEnergyId, "readout", readout);
    }

    private void OnDisable()
    {
        massesBuffer.Release();
        velocitiesBuffer.Release();
        readout.Release();
        massesBuffer = null;
        velocitiesBuffer = null;
        readout = null;
    }

    private void UpdateMasses(float deltaTime)
    {
        Matrix4x4 viewToScreen = Matrix4x4.Scale(new Vector3(renderTexture.width, renderTexture.height, 1));
        Matrix4x4 clipToViewportMatrix = Matrix4x4.Translate(Vector3.one * 0.5f) * Matrix4x4.Scale(Vector3.one * 0.5f);
        Matrix4x4 m = viewToScreen * clipToViewportMatrix * cam.projectionMatrix * cam.worldToCameraMatrix;
        // Matrix derived by Wokarol

        computeShader.SetMatrix("m", m); // matrix to convert world space position to screen space
        computeShader.SetFloat("deltaTime", deltaTime);
        computeShader.Dispatch(StepSimId, numMasses / 256, 1, 1); // compute a single step from the simulation
        if (useFadeProcessing)
        {
            computeShader.Dispatch(ProcessTextureId, (renderTexture.width / 32) + 1, (renderTexture.height / 8) + 1, 1);
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
            computeShader.SetTexture(StepSimId, "renderTexture", renderTexture);
            computeShader.SetTexture(ProcessTextureId, "renderTexture", renderTexture);


        }

        UpdateMasses(Time.deltaTime * timeStep);

        //LogTotalEnergy();

        Graphics.Blit(renderTexture, destination);

        if (!useFadeProcessing)
        {
            renderTexture.Release();
        }
    }

    private void LogTotalEnergy()
    {
        computeShader.Dispatch(CompEnergyId, numMasses / 128, 1, 1);
        var readoutArr = new float[1];
        readout.GetData(readoutArr);
        Debug.Log(readoutArr[0]);
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
