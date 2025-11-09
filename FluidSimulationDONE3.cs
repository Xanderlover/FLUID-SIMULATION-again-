using Godot;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;


[Tool]
public partial class FluidSimulationDONE3 : Node2D
{
	const float mass = 1;

	const int hashK1 = 15823;
	const int hashK2 = 9737333;

	[Export] private bool startSimulation = true;
	private int numParticles = 16;
	[Export] private int _numParticles
	{
		get { return numParticles; }
		set
		{
			numParticles = value;
			CreateParticles();
		}
	}
	
	[Export] private float gravity = 98f;
	[Export] private float collisionDamping = 0.69f;
	[Export] private float smoothingRadius = 10.0f;
	[Export] private float targetDensity = 10.0f;
	[Export] private float pressureMultiplier = 60.0f;
	[Export] private float nearPressureMultiplier; // = 60.0f;
	[Export] private float viscosityStrength = 1.0f;

	[Export] private Color particleColor = new Color("5bcefa");
	[Export] private Gradient particleSpeedColor;
	[Export] private float particleSize = 2.5f;
	private float particleSpacing;
	[Export] private float _particleSpacing
    {
		get { return particleSpacing; }
        set
        {
			particleSpacing = value;
			CreateParticles();
        }
    }

	[ExportGroup("Bounding Box")]
	[Export] private Vector2 boundsSize = new Vector2(320, 180);
	[Export] private Color boundsColor = new Color("00ff00");
	[Export] private float boundsThickness = 0.69f;

	private Vector2[] predictedPositions;
	private Vector2[] positions;
	private Vector2[] velocities;
	private float[] densities;
	private float[] nearDensities;

	[ExportGroup("Interaction Force")]
	[Export] private float forceRadius = 50.0f;
	[Export] private float forceStrength = 250.0f;
	[Export] private Color forcePullColor = new Color("00ff00");
	[Export] private Color forcePushColor = new Color("ff0000");
	// Attract the fluid towards the cursor
	private bool desiredForcePull;
	// Repel the fluid opposing the curosor
	private bool desiredForcePush;


	[ExportGroup("Editor")]
	[Export] private bool _drawSmoothinRadius;

	[ExportSubgroup("Spatial Hashing Debugging")]
	[Export] private bool drawSpatialHashing;
	[Export] private Color gridColor = new Color("3039477f");
	[Export] private Color chunkColor = new Color("008a9180");
	[Export] private Color chunkOutlineColor = new Color("5bcefa");
	[Export] private Color particlesInChunkColor = new Color("dea6ff");
	[Export] private Color smoothingRadiusColor = new Color("ffffff");
	[Export] private float hashingThickness = -1.0f;

	private int[] spatialLookUpParticleIndex;
	private uint[] spatialLookupCellKey;
	private int[] startIndices;
	private Vector2I[] cellOffsets =
	{
		new Vector2I(-1, -1),
		new Vector2I(0, -1),
		new Vector2I(1, -1),

		new Vector2I(-1, 0),
		new Vector2I(0, 0),
		new Vector2I(1, 0),

		new Vector2I(-1, 1),
		new Vector2I(0, 1),
		new Vector2I(1, 1)
	};

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

	public override void _Ready()
    {
		CreateParticles();
    }

	// TODO: public override void _PhysicsProcess(double delta)
	public override void _Process(double delta)
	{
		// Visualize the simulati9n in the editor
		if (Engine.IsEditorHint())
		{
			UpdateSpatialLookup(predictedPositions, smoothingRadius);
			QueueRedraw();
			return;
		}

		if (startSimulation == false) return;

		SimulationStep((float)delta);
		QueueRedraw();
	}
	
	public override void _Draw()
	{
		// Particles
		for (int i = 0; i < numParticles; i++)
		{

			// TODO: Replace this with GPU rendering
			// TODO: Make the particles change color based on their velocities
			DrawCircle(positions[i], particleSize, particleColor);
			if (particleSpeedColor != null)
			{
				Color gradientColor = particleSpeedColor.Sample(Mathf.InverseLerp(0, 128, velocities[i].LengthSquared()));
				DrawCircle(positions[i], particleSize, gradientColor);
			}

			if (_drawSmoothinRadius)
			{
				DrawCircle(positions[i], smoothingRadius, particleColor, false);
			}
		}

		// Bounding Box
		DrawRect(new Rect2(-boundsSize / 2f, boundsSize), boundsColor, false, boundsThickness);

		// Interaction forces
		if (desiredForcePull)
		{
			DrawCircle(GetGlobalMousePosition(), forceRadius, forcePullColor, false);
		}
		if (desiredForcePush)
		{
			DrawCircle(GetGlobalMousePosition(), forceRadius, forcePushColor, false);
		}

		// Spatial hashing
		if (!drawSpatialHashing) return;

		int cellSize = (int)Mathf.Floor(smoothingRadius);

		for (int i = 0; i < numParticles; i++)
		{
			(int cellX, int cellY) = PositionToCellCoord(positions[i], smoothingRadius);
			DrawRect(new Rect2(new Vector2(cellX, cellY) * cellSize, new Vector2(cellSize, cellSize)), gridColor, false, hashingThickness);
		}

		// Draw 3x3 grid around the sample point and smoothing radius
		(int centerX, int centerY) = PositionToCellCoord(GetLocalMousePosition(), smoothingRadius);
		foreach ((int offsetX, int offsetY) in cellOffsets)
		{
			DrawRect(new Rect2(new Vector2I(centerX + offsetX, centerY + offsetY) * cellSize, new Vector2(cellSize, cellSize)), chunkColor, true);
			DrawRect(new Rect2(new Vector2I(centerX + offsetX, centerY + offsetY) * cellSize, new Vector2(cellSize, smoothingRadius)), chunkOutlineColor, false, hashingThickness);
		}
		DrawCircle(GetLocalMousePosition(), smoothingRadius, smoothingRadiusColor, false, hashingThickness);

		// Test if spatial hashing works properly
		foreach (int i in ForeachPointWithinRadius(GetLocalMousePosition()))
		{
			DrawCircle(positions[i], particleSize, particlesInChunkColor);
		}
	}
	
	void CreateParticles()
	{
		// Create particles arrays
		predictedPositions = new Vector2[numParticles];
		positions = new Vector2[numParticles];
		velocities = new Vector2[numParticles];
		densities = new float[numParticles];
		nearDensities = new float[numParticles];


		spatialLookUpParticleIndex = new int[numParticles];
		spatialLookupCellKey = new uint[numParticles];
		startIndices = new int[numParticles];

		// Place particles in a grid formation
		int particlesPerRow = (int)Mathf.Sqrt(numParticles);
		int particlesPerCol = (numParticles - 1) / particlesPerRow + 1;
		float spacing = particleSize * 2 + particleSpacing;

		for (int i = 0; i < numParticles; i++)
		{
			float x = (i % particlesPerRow - particlesPerRow / 2f + 0.5f) * spacing;
			float y = (i / particlesPerRow - particlesPerCol / 2f + 0.5f) * spacing;
			positions[i] = new Vector2(x, y);
		}
	}
	
	// TODO: Replace the simulation step to use Object Oriented Programming and Godot CharacterBody2D Nodes.
	void SimulationStep(float deltaTime)
	{
		// Apply gravity and calculate predicted positions
		Parallel.For(0, numParticles, i =>
		{
			velocities[i] += Vector2.Down * gravity * deltaTime;
			predictedPositions[i] = positions[i] + velocities[i] * 1 / 120f; // Constant lookahead factor to make the simulation consistent on various frames
		});

		// Update spatial lookup with predicted positions
		UpdateSpatialLookup(predictedPositions, smoothingRadius);

		// Calculate densities
		Parallel.For(0, numParticles, i =>
		{
			(densities[i], nearDensities[i]) = CalculateDensity(predictedPositions[i]);
		});

		// Calculate and apply (external) pressure forces,and viscosity

		Vector2 mousePosition = GetLocalMousePosition();
		Parallel.For(0, numParticles, i =>
		{
			Vector2 pressureForce = CalculatePressureForce(i);
			Vector2 pressureAcceleration = pressureForce / densities[i];
			velocities[i] += pressureAcceleration * deltaTime;

			Vector2 viscosityForce = CalculateViscosityForce(i);
			velocities[i] += viscosityForce * deltaTime;

			if (desiredForcePull)
			{
				velocities[i] += InteractionForce(mousePosition, forceRadius, forceStrength, i) * deltaTime;
			}
			if (desiredForcePush)
			{
				velocities[i] += InteractionForce(mousePosition, forceRadius, -forceStrength, i) * deltaTime;
			}
		});

		// Update positions and resolve collisions
		Parallel.For(0, numParticles, i =>
		{
			positions[i] += velocities[i] * deltaTime;
			ResolveCollisions(i);
		});

		GD.Print("FPS: ", Engine.GetFramesPerSecond());
	}

	void ResolveCollisions(int particleIndex)
	{
		Vector2 halfBoundsSize = boundsSize / 2 - Vector2.One * particleSize;

		if (Mathf.Abs(positions[particleIndex].X) > halfBoundsSize.X)
		{
			positions[particleIndex].X = halfBoundsSize.X * Mathf.Sign(positions[particleIndex].X);
			velocities[particleIndex].X *= -1 * collisionDamping;
		}
		if (Mathf.Abs(positions[particleIndex].Y) > halfBoundsSize.Y)
		{
			positions[particleIndex].Y = halfBoundsSize.Y * Mathf.Sign(positions[particleIndex].Y);
			velocities[particleIndex].Y *= -1 * collisionDamping;
		}
	}

	// Spiky and Smoothing Kernels
	float DensityKernel(float dst, float radius)
	{
		if (dst >= radius) return 0;

		float volume = (Mathf.Pi * Mathf.Pow(radius, 4)) / 6;
		return (radius - dst) * (radius - dst) / volume;
	}

	// Returns the slope of the kernel
	float DensityDerivative(float dst, float radius)
	{
		if (dst >= radius) return 0;

		float scale = 12 / (Mathf.Pow(radius, 4) * Mathf.Pi);
		return (dst - radius) * scale;
	}

	float NearDensityKernel(float dst, float radius)
	{
		if (dst >= radius) return 0;

		float v = radius - dst;
		float scale = 10 / (Mathf.Pi * Mathf.Pow(radius, 4));
		return v * v * v * scale;
	}
	
	float NearDensityDerivative(float dst, float radius)
    {
		if (dst >= radius) return 0;

		float v = radius - dst;
		float scale = 30 / (Mathf.Pow(radius, 5) * Mathf.Pi);
		return -v * v * scale;
    }

	float ViscositySmoothingKernel(float dst, float radius)
	{
		if (dst >= radius) return 0;

		float volume = Mathf.Pi * Mathf.Pow(radius, 8) / 4;
		float value = Mathf.Max(0, radius * radius - dst * dst);
		return value * value * value / volume;
	}

	(float, float) CalculateDensity(Vector2 samplePoint)
	{
		float density = 0;
		float nearDensity = 0;

		// Loop over all particle positions inside the smoothing radius
		foreach (int otherIndex in ForeachPointWithinRadius(samplePoint))
		{
			float dst = (positions[otherIndex] - samplePoint).Length();
			float influence = DensityKernel(dst, smoothingRadius);
			density += mass * influence;

			float nearInfluence = NearDensityKernel(dst, smoothingRadius);
			nearDensity += mass * influence;
		}

		return (density, nearDensity);
	}

	Vector2 CalculatePressureForce(int particleIndex)
	{
		Vector2 pressureForce = Vector2.Zero;
		Vector2 position = predictedPositions[particleIndex];

		foreach (int otherParticleIndex in ForeachPointWithinRadius(position))
		{
			if (particleIndex == otherParticleIndex) continue;

			Vector2 offset = predictedPositions[otherParticleIndex] - predictedPositions[particleIndex];
			float dst = offset.Length();
			Vector2 dir = dst == 0 ? GetRandomDir() : offset / dst;

			float slope = DensityDerivative(dst, smoothingRadius);
			float nearSlope = NearDensityDerivative(dst, smoothingRadius);
			
			float density = densities[otherParticleIndex];
			float nearDensity = nearDensities[otherParticleIndex];

			float sharedPressure = CalculateSharedPressure(density, densities[otherParticleIndex]);
			float sharedNearPressure = CalculateSharedNearPressure(density, densities[otherParticleIndex]);

			//pressureForce += dir * slope * sharedPressure / densities[otherParticleIndex];
			//pressureForce += dir * nearSlope * sharedNearPressure / nearDensities[otherParticleIndex];
			pressureForce += sharedPressure * dir * slope * mass / density;
			pressureForce += sharedNearPressure * dir * nearSlope * mass / nearDensity;
		}

		return pressureForce;
	}

	// Pressure from density
	float ConvertDensityToPressure(float density)
	{
		float pressure = (density + targetDensity) * pressureMultiplier;
		return pressure;
	}

	float ConvertNearDensityToPressure(float nearDensity)
    {
		float nearPressure = nearPressureMultiplier * nearDensity;
		return nearPressure;
    }

	float CalculateSharedPressure(float densityA, float densityB)
	{
		float pressureA = ConvertDensityToPressure(densityA);
		float pressureB = ConvertDensityToPressure(densityB);

		return (pressureA + pressureB) / 2;
	}

	float CalculateSharedNearPressure(float nearDensityA, float nearDensityB)
    {
		float nearPressureA = ConvertDensityToPressure(nearDensityA);
		float nearPressureB = ConvertDensityToPressure(nearDensityB);

		return (nearPressureA + nearPressureB) / 2;
    }

	Vector2 GetRandomDir()
	{
		int randomX = GD.RandRange(-1, 1);
		int randomY = GD.RandRange(-1, 1);
		Vector2 randomDir = new Vector2(randomX, randomY);
		return randomDir;
	}

	Vector2 CalculateViscosityForce(int particleIndex)
	{
		Vector2 viscosityForce = Vector2.Zero;
		Vector2 position = positions[particleIndex];

		foreach (int otherIndex in ForeachPointWithinRadius(position))
		{
			float dst = (position - positions[otherIndex]).Length();
			float influence = ViscositySmoothingKernel(dst, smoothingRadius);
			viscosityForce += (velocities[otherIndex] - velocities[particleIndex]) * influence;
		}

		return viscosityForce * viscosityStrength;
	}

	// Interaction and external forces!

	// TODO: Optimizze this to use spatial hashing rather than looping through the entire particle simulation index?
	Vector2 InteractionForce(Vector2 inputPos, float radius, float strength, int particleIndex)
	{
		Vector2 interactionForce = Vector2.Zero;
		Vector2 offset = inputPos - positions[particleIndex];
		float sqrDST = offset.Dot(offset);

		// If particle is inside of input radius, calculate force towards input point
		if (sqrDST < radius * radius)
		{
			float dst = Mathf.Sqrt(sqrDST);
			Vector2 dirToInputPoint = dst <= float.Epsilon ? Vector2.Zero : offset / dst;
			// Value is 1 hwen particle is exactly at input point; 0 when at edge of input circle
			float centerT = 1 - dst / radius;
			// Calculate the force (velocity is subtracted to slow the particle down)
			interactionForce += (dirToInputPoint * strength - velocities[particleIndex] * centerT);
		}

		return interactionForce;
	}

	// Spatial Hashing!

	// Convert a position to the coordinate of the cell it is within
	public (int x, int y) PositionToCellCoord(Vector2 point, float radius)
	{
		int cellX = (int)Mathf.Floor(point.X / radius);
		int cellY = (int)Mathf.Floor(point.Y / radius);
		return (cellX, cellY);
	}

	// Convert a cell coordinate into a single number
	// Hash collisions (different cells -> same value) are unavoidable, but we want to at
	// least try to minimize collisions for nearby cells. I'm sure there are better ways,
	// but this seems to work okay.
	public uint HashCell(int cellX, int cellY)
	{
		uint a = (uint)cellX * hashK1;
		uint b = (uint)cellY * hashK2;
		return a + b;
	}

	// Wrap the hash value around the length of the array (so it can be used as an index)
	// Hash cell coordinate to a single unsigned integer
	public uint GetKeyFromHash(uint hash)
	{
		return hash % (uint)spatialLookupCellKey.Length;
	}

	public void UpdateSpatialLookup(Vector2[] points, float radius)
	{
		// Create (unordered) spatial lookup
		Parallel.For(0, points.Length, i =>
		{
			(int cellX, int cellY) = PositionToCellCoord(points[i], radius);
			uint cellKey = GetKeyFromHash(HashCell(cellX, cellY));
			spatialLookupCellKey[i] = cellKey;
			spatialLookUpParticleIndex[i] = i;
			startIndices[i] = int.MaxValue; // Reset start index
		});

		// Sort by cell key
		Array.Sort(spatialLookupCellKey, spatialLookUpParticleIndex);

		// Calculate start indices of each unique cell key in the spatial lookup
		Parallel.For(0, points.Length, i =>
		{
			uint key = spatialLookupCellKey[i];
			uint keyPrev = i == 0 ? uint.MaxValue : spatialLookupCellKey[i - 1];
			if (key != keyPrev)
			{
				startIndices[key] = i;
			}
		});
	}

	// Neighbor search for nearby particles inside of the samplePoints smoothing radius
	public List<int> ForeachPointWithinRadius(Vector2 samplePoint)
	{
		// Find which cell the sample point is in (this wil be the center of our 3x3 block)
		(int centerX, int centerY) = PositionToCellCoord(samplePoint, smoothingRadius);
		float sqrRadius = smoothingRadius * smoothingRadius;

		List<int> neighbouringPoints = new List<int>();

		// Loop over all cells of the 3x3 block around the center cell
		foreach ((int offsetX, int offsetY) in cellOffsets)
		{
			// Get key of current cell, then loop over all points that share that key
			uint key = GetKeyFromHash(HashCell(centerX + offsetX, centerY + offsetY));
			int cellStartIndex = startIndices[key];

			for (int i = cellStartIndex; i < spatialLookupCellKey.Length; i++)
			{
				// Exit loop if we're no longer looking at the correct cell
				if (spatialLookupCellKey[i] != key) break;

				int particleIndex = spatialLookUpParticleIndex[i];
				float sqrDst = (positions[particleIndex] - samplePoint).LengthSquared();

				// Test if the point is inside the radius
				if (sqrDst <= sqrRadius)
				{
					// Do something with the particleIndex!
					// (either by writing coe here that uses it directly, or more likely by
					// having this function take in a callback, or return an IEnumerable, etc.)
					neighbouringPoints.Add(particleIndex);

					// Do something more! (add index value to list for calculations, change color of particle, change size?!)
				}
			}
		}

		return neighbouringPoints;
	}
}