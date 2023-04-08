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
    private readonly ComputeShader computeShader;

    public int NumMasses { get; private set; }
    public ComputeBuffer MassesBuffer { get; private set; }
    public ComputeBuffer MotionsBuffer { get; private set; }

    private readonly byte[] massesByteBuffer = new byte[65536 * SizeOfPointMass];
    private readonly byte[] motionsByteBuffer = new byte[65536 * SizeOfMotion];

    private ComputeBuffer readoutBuffer;

    private readonly int stepSimId;
    private readonly int compEnergyId;
    public float TimeStep { get; set; } = 0.01f;

    public AstronomicalSimulator(ComputeShader computeShader, SimulationState simulationState)
    {
        this.computeShader = computeShader;
        
        stepSimId = computeShader.FindKernel("StepSimulation");
        compEnergyId = computeShader.FindKernel("ComputeTotalEnergy");

        NumMasses = simulationState.NumMasses;
        readoutBuffer = new ComputeBuffer(NumMasses, 8);
        readoutBuffer.SetData(new Vector2[NumMasses]);
        computeShader.SetBuffer(compEnergyId, "readout", readoutBuffer);

        SetSimulationState(simulationState);
    }

    public void ReleaseBuffers(bool releaseReadout = false)
    {
        MassesBuffer?.Release();
        MotionsBuffer?.Release();
        MassesBuffer = null;
        MotionsBuffer = null;
        
        if (releaseReadout)
        {
            readoutBuffer?.Release();
            readoutBuffer = null;
        }
    }

    public void UpdateMasses() // compute a single step from the simulation
    {
        computeShader.SetFloat("deltaTime", TimeStep);
        computeShader.SetFloat("halfDeltaTime", TimeStep * 0.5f);
        computeShader.Dispatch(stepSimId, NumMasses / ComputeThreads, 1, 1); 
    }

    public SimulationState GetSimulationState()
    {
        var pointMasses = new PointMassState[NumMasses];
        GetSimulationStateNonAlloc(pointMasses);
        return new SimulationState(pointMasses);
    }

    private void GetSimulationStateNonAlloc(PointMassState[] pointMasses)
    {
        if (pointMasses.Length < NumMasses)
        {
            throw new InvalidOperationException("Buffer length too small");
        }
        
        var massesArr = new PointMass[NumMasses];
        var motionsArr = new Motion[NumMasses];
        MassesBuffer.GetData(massesArr);
        MotionsBuffer.GetData(motionsArr);

        for (var index = 0; index < NumMasses; index++)
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
        if (pointMassesBuffer.Length < NumMasses)
        {
            throw new InvalidOperationException("Buffer length too small");
        }
        
        MassesBuffer.GetData(massesByteBuffer, 0, 0, NumMasses * SizeOfPointMass);
        MotionsBuffer.GetData(motionsByteBuffer, 0, 0, NumMasses * SizeOfMotion);
        
        for (var index = 0; index < NumMasses; index++)
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
        NumMasses = simulationState.NumMasses;

        var masses = new PointMass[NumMasses];
        var motions = new Motion[NumMasses];
        var stateMasses = simulationState.StateMasses;

        for (var i = 0; i < NumMasses; i++) // Change point mass creation here
        {
            var stateMass = stateMasses[i];
            masses[i] = new PointMass{Mass= stateMass.Mass, Position = stateMass.Position};
            motions[i] = new Motion {Velocity = stateMass.Velocity, Acceleration = stateMass.Acceleration};
        }

        MassesBuffer = new ComputeBuffer(NumMasses, SizeOfPointMass);
        MassesBuffer.SetData(masses);

        MotionsBuffer = new ComputeBuffer(NumMasses, SizeOfMotion);
        MotionsBuffer.SetData(motions);

        computeShader.SetInt("numMasses", NumMasses);
        computeShader.SetBuffer(stepSimId, "masses", MassesBuffer);
        computeShader.SetBuffer(stepSimId, "motions", MotionsBuffer);

        computeShader.SetBuffer(compEnergyId, "masses", MassesBuffer);
        computeShader.SetBuffer(compEnergyId, "motions", MotionsBuffer);
    }

    public void SetSimulationStateNonAllocBytes(byte[] pointMassesBuffer, int newNumMasses)
    {
        ReleaseBuffers();
        NumMasses = newNumMasses;
        
        for (var index = 0; index < NumMasses; index++)
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
        
        MassesBuffer = new ComputeBuffer(NumMasses, SizeOfPointMass);
        MassesBuffer.SetData(massesByteBuffer, 0, 0, NumMasses * SizeOfPointMass);

        MotionsBuffer = new ComputeBuffer(NumMasses, SizeOfMotion);
        MotionsBuffer.SetData(motionsByteBuffer, 0, 0, NumMasses * SizeOfMotion);

        computeShader.SetInt("numMasses", NumMasses);
        computeShader.SetBuffer(stepSimId, "masses", MassesBuffer);
        computeShader.SetBuffer(stepSimId, "motions", MotionsBuffer);

        computeShader.SetBuffer(compEnergyId, "masses", MassesBuffer);
        computeShader.SetBuffer(compEnergyId, "motions", MotionsBuffer);
    }

    public Vector3 GetTotalEnergy()
    {
        if (readoutBuffer == null)
        {
            throw new NullReferenceException("Readout buffer is null");
        }
        computeShader.Dispatch(compEnergyId, NumMasses / 256, 1, 1);
        var readoutArr = new float[NumMasses * 2];
        readoutBuffer.GetData(readoutArr);

        var kineticEnergy = 0f;
        var potentialEnergy = 0f;

        for (var i = 0; i < NumMasses; i++)
        {
            var offset = i * 2;
            kineticEnergy += readoutArr[offset];
            potentialEnergy += readoutArr[offset + 1];
        }

        return new Vector3(kineticEnergy, potentialEnergy, kineticEnergy + potentialEnergy); // Kinetic, Potential, Total
    }
}
