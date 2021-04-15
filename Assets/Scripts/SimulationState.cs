using UnityEngine;

public struct PointMassState
{
    public float Mass;
    public Vector3 Position;
    public Vector3 Velocity;
    public Vector3 Acceleration;
}

public class SimulationState
{
    private PointMassState[] masses;

    public SimulationState(PointMassState[] masses)
    {
        this.masses = masses;
    }

    public SimulationState(int numMasses)
    {
        masses = new PointMassState[numMasses];
        
        for (var i = 0; i < numMasses; i++) // create a little galaxy
        {
            Vector3 pos = Random.insideUnitCircle * 100;
            pos.z = pos.y;
            pos.y = Random.Range(-10, 10);

            var vel = Vector3.Cross(pos, Vector3.up).normalized * pos.sqrMagnitude * 0.1000f; // circular motion
            //pos.x += 1000;

            masses[i] = new PointMassState
            {
                Mass = 50000000000000,
                Position = pos,
                Velocity = vel,
                Acceleration = Vector3.zero
            };
        }
    }

    public PointMassState[] StateMasses => masses;

    public int NumMasses()
    {
        return masses.Length;
    }
}
