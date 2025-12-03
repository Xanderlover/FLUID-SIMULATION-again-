using Godot;
using System;
using System.Collections.Generic;

[GlobalClass]
// Allows the capabilities to Attract (Pull), Repel (Push), Emit (Spawn), Absorb (Drain/Remove), CharacterBody2Ds'!
public partial class FluidInteraction : Area2D
{
    public enum Capabilities
    {
        Attract, // Slurp
        Repel, // Squirt
        Drain, // Swallow
        Spawn // Summon
    }
    [Export] Capabilities interactionType;
	[Export] private float forceRadius = 50.0f;
	[Export] private float forceStrength = 250.0f;
	[Export] private Color forcePullColor = new Color("00ff00");
    [Export] private Color forcePushColor = new Color("ff0000");
    
	// Attract the fluid towards this position
	private bool desiredForcePull;
    // Repel the fluid opposing this position
    private bool desiredForcePush;
    
    List<CharacterBody2D> bodies = new List<CharacterBody2D>();

	public override void _Input(InputEvent @event)
	{
		if (@event.IsActionPressed("force_pull"))
		{
			desiredForcePull = true;
		}
		else if (@event.IsActionReleased("force_pull"))
		{
			desiredForcePull = false;
		}

		if (@event.IsActionPressed("force_push"))
		{
			desiredForcePush = true;
		}
		else if (@event.IsActionReleased("force_push"))
		{
			desiredForcePush = false;
		}
	}

    public override void _PhysicsProcess(double delta)
    {
        if (bodies.Count < 0) return;

        for (int i = 0; i < bodies.Count; i++)
        {
            if (desiredForcePull)
            {
                bodies[i].Velocity += InteractionForce(GlobalPosition, forceRadius, forceStrength, i) * (float)delta;
            }
            if (desiredForcePush)
            {
                bodies[i].Velocity += InteractionForce(GlobalPosition, forceRadius, -forceStrength, i) * (float)delta;
            }
        }
    }
    
    // External force to move the bodies/particles
    Vector2 InteractionForce(Vector2 inputPos, float radius, float strength, int particleIndex)
    {
        Vector2 interactionForce = Vector2.Zero;
        Vector2 offset = inputPos - bodies[particleIndex].GlobalPosition;
        float sqrDst = offset.Dot(offset);

        // If particle is inside of input radius, calculate force towards input point
        if (sqrDst < radius * radius)
        {
            float dst = Mathf.Sqrt(sqrDst);
            Vector2 dirToInputPoint = dst <= float.Epsilon ? Vector2.Zero : offset / dst;
            // Value is 1 when particle is exactly at input point; 0 when at edge of input circle
            float centerT = 1 - dst / radius;
            // Calculate the force (velocity is subtracted to slow the particle down)
            interactionForce += (dirToInputPoint * strength - bodies[particleIndex].Velocity) * centerT;
        }

        return interactionForce;
    }

    private void OnBodiesEntered(CharacterBody2D body)
    {
        bodies.Add(body);
    }
    
    private void OnBodiesExited(CharacterBody2D body)
    {
        bodies.Remove(body);
    }
}
