using System;
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
    private const int SizeOfPointMass = 16;
    private const int SizeOfMotion = 24;
    private const int ComputeThreads = 512;
    private ComputeShader computeShader;
    private int numMasses; // should be a multiple of 256
    
    public int NumMasses => numMasses;
    public ComputeBuffer MassesBuffer => massesBuffer;
    public ComputeBuffer MotionsBuffer => motionsBuffer;

    private byte[] massesByteBuffer = new byte[65536 * SizeOfPointMass];
    private byte[] motionsByteBuffer = new byte[65536 * SizeOfMotion];

    private ComputeBuffer massesBuffer;
    private ComputeBuffer motionsBuffer;
    private ComputeBuffer readoutBuffer;

    private readonly int stepSimId;
    private readonly int compEnergyId;

    public AstronomicalSimulator(ComputeShader computeShader, SimulationState simulationState)
    {
        this.computeShader = computeShader;
        
        stepSimId = computeShader.FindKernel("StepSimulation");
        compEnergyId = computeShader.FindKernel("ComputeTotalEnergy");

        numMasses = simulationState.NumMasses;
        readoutBuffer = new ComputeBuffer(numMasses, 8);
        readoutBuffer.SetData(new Vector2[numMasses]);
        computeShader.SetBuffer(compEnergyId, "readout", readoutBuffer);

        SetSimulationState(simulationState);
    }

    public void ReleaseBuffers(bool releaseReadout = false)
    {
        massesBuffer?.Release();
        motionsBuffer?.Release();
        massesBuffer = null;
        motionsBuffer = null;
        
        if (releaseReadout)
        {
            readoutBuffer?.Release();
            readoutBuffer = null;
        }
    }

    public void UpdateMasses(float deltaTime) // compute a single step from the simulation
    {
        computeShader.SetFloat("deltaTime", deltaTime);
        computeShader.SetFloat("halfDeltaTime", deltaTime * 0.5f);
        computeShader.Dispatch(stepSimId, numMasses / ComputeThreads, 1, 1); 
    }

    public SimulationState GetSimulationState()
    {
        var pointMasses = new PointMassState[numMasses];
        GetSimulationStateNonAlloc(pointMasses);
        return new SimulationState(pointMasses);
    }

    private void GetSimulationStateNonAlloc(PointMassState[] pointMasses)
    {
        if (pointMasses.Length < numMasses)
        {
            throw new InvalidOperationException("Buffer length too small");
        }
        
        var massesArr = new PointMass[numMasses];
        var motionsArr = new Motion[numMasses];
        massesBuffer.GetData(massesArr);
        motionsBuffer.GetData(motionsArr);

        for (var index = 0; index < numMasses; index++)
        {
            pointMasses[index] = new PointMassState
            {
                Mass = massesArr[index].Mass,
                Position = massesArr[index].Position,
                Velocity = motionsArr[index].Velocity,
                Acceleration = motionsArr[index].Acceleration
            };
        }
    }

    public void GetSimulationStateNonAllocBytes(byte[] pointMassesBuffer)
    {
        if (pointMassesBuffer.Length < numMasses)
        {
            throw new InvalidOperationException("Buffer length too small");
        }
        
        massesBuffer.GetData(massesByteBuffer, 0, 0, numMasses * SizeOfPointMass);
        motionsBuffer.GetData(motionsByteBuffer, 0, 0, numMasses * SizeOfMotion);
        
        for (var index = 0; index < numMasses; index++)
        {
            var offset = index * (SizeOfPointMass + SizeOfMotion);
            var massOffset = index * SizeOfPointMass;
            var motionOffset = index * SizeOfMotion;
            for (var massIndex = 0; massIndex < SizeOfPointMass; massIndex++)
            {
                pointMassesBuffer[offset + massIndex] = massesByteBuffer[massOffset + massIndex];
            }

            offset += SizeOfPointMass;
            
            for (var motionIndex = 0; motionIndex < SizeOfMotion; motionIndex++)
            {
                pointMassesBuffer[offset + motionIndex] = motionsByteBuffer[motionOffset + motionIndex];
            }
        }
    }

    public void SetSimulationState(SimulationState simulationState)
    {
        ReleaseBuffers();
        numMasses = simulationState.NumMasses;

        var masses = new PointMass[numMasses];
        var motions = new Motion[numMasses];
        var stateMasses = simulationState.StateMasses;

        for (var i = 0; i < numMasses; i++) // Change point mass creation here
        {
            var stateMass = stateMasses[i];
            masses[i] = new PointMass{Mass= stateMass.Mass, Position = stateMass.Position};
            motions[i] = new Motion {Velocity = stateMass.Velocity, Acceleration = stateMass.Acceleration};
        }

        massesBuffer = new ComputeBuffer(numMasses, SizeOfPointMass);
        massesBuffer.SetData(masses);

        motionsBuffer = new ComputeBuffer(numMasses, SizeOfMotion);
        motionsBuffer.SetData(motions);

        computeShader.SetInt("numMasses", numMasses);
        computeShader.SetBuffer(stepSimId, "masses", massesBuffer);
        computeShader.SetBuffer(stepSimId, "motions", motionsBuffer);

        computeShader.SetBuffer(compEnergyId, "masses", massesBuffer);
        computeShader.SetBuffer(compEnergyId, "motions", motionsBuffer);
    }

    public void SetSimulationStateNonAllocBytes(byte[] pointMassesBuffer, int newNumMasses)
    {
        ReleaseBuffers();
        numMasses = newNumMasses;
        
        for (var index = 0; index < numMasses; index++)
        {
            var offset = index * (SizeOfPointMass + SizeOfMotion);
            var massOffset = index * SizeOfPointMass;
            var motionOffset = index * SizeOfMotion;
            for (var massIndex = 0; massIndex < SizeOfPointMass; massIndex++)
            {
                massesByteBuffer[massOffset + massIndex] = pointMassesBuffer[offset + massIndex];
            }

            offset += SizeOfPointMass;
            
            for (var motionIndex = 0; motionIndex < SizeOfMotion; motionIndex++)
            {
                motionsByteBuffer[motionOffset + motionIndex] = pointMassesBuffer[offset + motionIndex];
            }
        }
        
        massesBuffer = new ComputeBuffer(numMasses, SizeOfPointMass);
        massesBuffer.SetData(massesByteBuffer, 0, 0, numMasses * SizeOfPointMass);

        motionsBuffer = new ComputeBuffer(numMasses, SizeOfMotion);
        motionsBuffer.SetData(motionsByteBuffer, 0, 0, numMasses * SizeOfMotion);

        computeShader.SetInt("numMasses", numMasses);
        computeShader.SetBuffer(stepSimId, "masses", massesBuffer);
        computeShader.SetBuffer(stepSimId, "motions", motionsBuffer);

        computeShader.SetBuffer(compEnergyId, "masses", massesBuffer);
        computeShader.SetBuffer(compEnergyId, "motions", motionsBuffer);
    }

    public Vector3 GetTotalEnergy()
    {
        if (readoutBuffer == null)
        {
            Debug.Log("NULL");
        }
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
