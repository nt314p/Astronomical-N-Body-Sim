using UnityEngine;
using Random = UnityEngine.Random;

public struct PointMassState
{
    public float Mass;
    public Vector3 Position;
    public Vector3 Velocity;
    public Vector3 Acceleration;
}

public enum RadiusRelation
{
    RadiusSquareRoot=0, 
    Radius=1,
    RadiusSquared=2,
    Constant=3
}

public class SimulationState
{
    private readonly PointMassState[] masses;
    public int NumMasses => masses.Length;

    public SimulationState(PointMassState[] masses)
    {
        this.masses = masses;
    }

    public SimulationState(int numMasses)
    {
        masses = new PointMassState[numMasses];

        for (var index = 0; index < numMasses; index++) // create a little galaxy
        {
            var pos = Random.insideUnitSphere * 100;
            // pos.z = pos.y;
            pos.y = Random.Range(-5.0f, 5.0f);

            var vel = Vector3.Cross(pos, Vector3.up).normalized * Mathf.Sqrt(pos.magnitude) *
                      20.15500f; // circular motion
            //pos.x += 1000;

            masses[index] = new PointMassState
            {
                Mass = 1000,
                Position = pos,
                Velocity = vel,
                Acceleration = Vector3.zero
            };
        }
    }

    public SimulationState(int numMasses, float mass, float initialVelocity, float galaxyRadius, RadiusRelation massDistribution, RadiusRelation velocityRelation)
    {
        masses = new PointMassState[numMasses];

        for (var index = 0; index < numMasses; index++)
        {
            var radius = Random.Range(0.0f, 1.0f);
            switch (massDistribution)
            {
                case RadiusRelation.RadiusSquareRoot: // *?
                    radius = Mathf.Sqrt(radius);
                    break;
                case RadiusRelation.Radius: // * not mathematically correct, but an okay approximation
                    radius = Mathf.Pow(radius, 0.3333f);
                    break;
            }

            var speed = radius;
            switch (velocityRelation)
            {
                case RadiusRelation.RadiusSquareRoot:
                    speed = Mathf.Sqrt(speed);
                    break;
                case RadiusRelation.Radius:
                    break;
                case RadiusRelation.RadiusSquared:
                    speed *= speed * speed;
                    break;
                case RadiusRelation.Constant:
                    speed = 1;
                    break;
            }

            radius *= galaxyRadius;

            var pos = (Vector3) Random.insideUnitCircle.normalized;
            pos.z = pos.y;
            pos *= radius;
            
            var heightVariation = galaxyRadius * 0.05f;
            pos.y = Random.Range(-heightVariation, heightVariation);
            
            speed *= initialVelocity;

            var vel = Vector3.Cross(pos, Vector3.up).normalized * speed;

            masses[index] = new PointMassState
            {
                Mass = mass,
                Position = pos,
                Velocity = vel,
                Acceleration = Vector3.zero
            };
        }
    }
    
    public PointMassState[] StateMasses => masses;
}
