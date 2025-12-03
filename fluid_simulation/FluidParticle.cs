using System;
using System.Collections;
using System.Collections.Generic;
using Godot;


[Tool, GlobalClass]
public partial class FluidParticle : CharacterBody2D
{
	//[Signal] public delegate void OnStartEventHandler();

	// Mass changes the density and pressure forces applied.
	// Higher mass particles take higher priority pushing lower massed particles more
	[Export] public float mass = 1.0f;
    /// How fast does our particle fall. NOTE: you can use negative values for upside-down gravity!
	[Export] public float gravity = 98.0f;
	[Export] public float collisionDamping = 0.69f;
	public float smoothingRadius = 10.0f;
	[Export] private float _smoothingRadius
    {
        get { return smoothingRadius; }
        set
        {
            smoothingRadius = value;
			QueueRedraw();
        }
    }
	[Export] public float targetDensity = 10.0f;
	[Export] public float pressureMultiplier = 60.0f;
	[Export] public float nearPressureMultiplier = 30.0f;
	[Export] public float viscosityStrength = 1.0f;
	// Do we share the pressure between nearby particles? If false this describes the behavior of gasses more than liquid.
	[Export] public bool sharedPressure = true;

	[ExportGroup("Deactivation")]
	[Export] private bool sleeping;
	[Export] private bool canSleep = true;
	[Export] private bool freeze;

	[ExportGroup("Editor")]
	private bool drawSmoothingRadius;
	[Export] private bool _drawSmoothingRadius
    {
        get { return drawSmoothingRadius; }
        set
        {
            drawSmoothingRadius = value;
			QueueRedraw();
        }
    }
	private Color smoothingRadiusColor = new Color("5bcefa");
	[Export] private Color _smoothingRadiusColor
    {
        get { return smoothingRadiusColor; }
        set
        {
            smoothingRadiusColor = value;
			QueueRedraw();
        }
    }

	private const String fluidServerGroupName = "fluid_server";
	private FluidSimulation fluidServer;
	public Vector2[] neighboringPosition;

    public override void _Ready()
    {
		if (Engine.IsEditorHint()) return;
		
        SpawnParticle();
    }

    public override void _Draw()
	{
		if (!Engine.IsEditorHint()) return;

		if (drawSmoothingRadius)
		{
			DrawCircle(Position, smoothingRadius, smoothingRadiusColor, false);
		}
    }

	private void SpawnParticle()
    {
		// TODO: It would be better if we could a signal or event of some sorts?
		// Or this could work...
		fluidServer = (FluidSimulation)GetTree().GetFirstNodeInGroup(fluidServerGroupName);
		if (fluidServer != null)
        {
            fluidServer.SpawnParticle(this);
        }   
    }

	public void DrainParticle()
    {
        fluidServer.DrainParticle(this);
		QueueFree();
    }
}
