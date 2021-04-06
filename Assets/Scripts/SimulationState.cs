using UnityEngine;

public struct PointMassState
{
    public float Mass;
    public Vector3 Position;
    public Vector3 Velocity;
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
            Vector3 pos = Random.insideUnitCircle * 200;
            pos.z = pos.y;
            pos.y = Random.Range(-10, 10);

            var vel = Vector3.Cross(pos, Vector3.up).normalized * pos.sqrMagnitude * 0.0035f; // circular motion
            //pos.x += 1000;

            masses[i] = new PointMassState
            {
                Mass = 1000000000000,
                Position = pos,
                Velocity = vel
            };
        }
    }

    public PointMassState[] StateMasses => masses;

    public int NumMasses()
    {
        return masses.Length;
    }
}
