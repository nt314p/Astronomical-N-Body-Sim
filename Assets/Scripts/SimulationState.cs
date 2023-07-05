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
    RadiusSquareRoot, 
    Radius,
    RadiusSquared,
    Constant
}

public class SimulationState // TODO: rework this class, only used for initializing the sim
{
    public PointMassState[] StateMasses { get; }
    public int NumMasses => StateMasses.Length;

    // TODO: allow for more galaxy customizations
    public SimulationState(int numMasses) 
    {
        StateMasses = new PointMassState[numMasses];

        for (var index = 0; index < numMasses; index++) // create a little galaxy
        {
            var pos = Random.insideUnitSphere * 100;
            // pos.z = pos.y;
            pos.y = Random.Range(-5.0f, 5.0f);

            var vel = Vector3.Cross(pos, Vector3.up).normalized * Mathf.Sqrt(pos.magnitude) *
                      20.15500f; // circular motion
            //pos.x += 1000;

            StateMasses[index] = new PointMassState
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
        StateMasses = new PointMassState[numMasses];

        for (var index = 0; index < numMasses; index++)
        {
            var radius = Random.Range(0.0f, 1.0f);
            switch (massDistribution)
            {
                case RadiusRelation.RadiusSquareRoot: // *?
                    radius = Mathf.Sqrt(radius);
                    break;
                case RadiusRelation.Radius: // TODO: * not mathematically correct, but an okay approximation
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

            StateMasses[index] = new PointMassState
            {
                Mass = mass,
                Position = pos,
                Velocity = vel,
                Acceleration = Vector3.zero
            };
        }
    }
}
