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
    public int NumMasses => masses.Length;

    public SimulationState(PointMassState[] masses)
    {
        this.masses = masses;
    }

    public SimulationState()
    {
        masses = new[]
        {
            new PointMassState
            {
                Mass = 100,
                Position = new Vector3(200, 210, 220),
                Velocity = new Vector3(300, 310, 320),
                Acceleration = new Vector3(400, 410, 420),
            },
            new PointMassState
            {
                Mass = 101,
                Position = new Vector3(201, 211, 221),
                Velocity = new Vector3(301, 311, 321),
                Acceleration = new Vector3(401, 411, 421),
            }
        };
    }

    public SimulationState(int numMasses)
    {
        masses = new PointMassState[numMasses];
        
        for (var i = 0; i < numMasses; i++) // create a little galaxy
        {
            Vector3 pos = Random.insideUnitSphere * 100;
            // pos.z = pos.y;
            pos.y = Random.Range(-10, 10);

            var vel = Vector3.Cross(pos, Vector3.up).normalized * pos.sqrMagnitude * 0.015500f; // circular motion
            //pos.x += 1000;

            masses[i] = new PointMassState
            {
                // Mass = 101,
                // Position = new Vector3(201, 211, 221),
                // Velocity = new Vector3(301, 311, 321),
                // Acceleration = new Vector3(401, 411, 421),
                Mass = 4000000000000,
                Position = pos,
                Velocity = vel,
                Acceleration = Vector3.zero
            };
        }
    }

    public PointMassState[] StateMasses => masses;
}
