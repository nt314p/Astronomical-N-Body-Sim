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

public class AstronomicalSimulator
{
    private ComputeShader computeShader;
    private int numMasses; // should be a multiple of 256

    public int NumMasses => numMasses;
    public ComputeBuffer MassesBuffer => massesBuffer;
    public ComputeBuffer MotionsBuffer => motionsBuffer;

    private ComputeBuffer massesBuffer;
    private ComputeBuffer motionsBuffer;
    private ComputeBuffer readoutBuffer;

    private int stepSimId;
    private int compEnergyId;

    public AstronomicalSimulator(ComputeShader computeShader, SimulationState simulationState)
    {
        this.computeShader = computeShader;

        numMasses = simulationState.NumMasses();

        stepSimId = computeShader.FindKernel("StepSimulation");
        compEnergyId = computeShader.FindKernel("ComputeTotalEnergy");
        
        var masses = new PointMass[numMasses];
        var motions = new Motion[numMasses];
        var stateMasses = simulationState.StateMasses;

        for (var i = 0; i < numMasses; i++) // Change point mass creation here
        {
            var stateMass = stateMasses[i];
            masses[i] = new PointMass{Mass= stateMass.Mass, Position = stateMass.Position};
            motions[i] = new Motion {Velocity = stateMass.Velocity, Acceleration = Vector3.zero};
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
