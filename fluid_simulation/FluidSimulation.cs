using Godot;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;

[GlobalClass]
public partial class FluidSimulation : Node
{
	[Export] private bool startSimulation = true;
	private int numParticles;

	private List<FluidParticle> fluidParticles = new List<FluidParticle>();
	private Vector2[] predictedPositions;
	private Vector2[] positions;
	private Vector2[] velocities;
	private float[] densities;
	private float[] nearDensities;

	const int hashK1 = 15823;
	const int hashK2 = 9737333;

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
	[Export] private float chunkRadius = 10.0f;

	public override void _PhysicsProcess(double delta)
	{
		if (fluidParticles.Count() <= 0) return;

		if (!startSimulation) return;

		SimulationStep((float)delta);
	}

	void SimulationStep(float deltaTime)
	{
		// TODO: get the position to avoid interlop, figure out a more optimal way to do this if required, or necessary
		for (int i = 0; i < numParticles; i++)
		{
			positions[i] = fluidParticles[i].GlobalPosition;
		}

		// Apply gravity and calculate predicted positions
		Parallel.For(0, numParticles, i =>
		{
			if (!fluidParticles[i].IsOnFloor())
			{
				fluidParticles[i].Velocity += -fluidParticles[i].UpDirection * fluidParticles[i].gravity * deltaTime;
			}

			predictedPositions[i] = positions[i] + velocities[i] * deltaTime;
		});

		// Update spatial lookup with predicted positions
		UpdateSpatialLookup(predictedPositions);

		// Calculate densities
		Parallel.For(0, numParticles, i =>
		{
			densities[i] = CalculateDensity(predictedPositions[i], fluidParticles[i].smoothingRadius, fluidParticles[i].mass);
			nearDensities[i] = CalculateNearDensity(predictedPositions[i], fluidParticles[i].smoothingRadius, fluidParticles[i].mass);
		});

		// Calculate and apply pressure forces, and viscosity
		Parallel.For(0, numParticles, i =>
		{
			velocities[i] = fluidParticles[i].Velocity;

			Vector2 pressureForce = CalculatePressureForce(i);
			Vector2 pressureAcceleration = pressureForce / densities[i];
			velocities[i] += pressureAcceleration * deltaTime;

			Vector2 viscosityForce = CalculateViscosityForce(i);
			velocities[i] += viscosityForce * deltaTime;
		});

		// Update positions and resolve collisions
		for (int i = 0; i < numParticles; i++)
		{
			fluidParticles[i].neighboringPosition = NeighborSearchPositions(predictedPositions[i], fluidParticles[i].smoothingRadius).ToArray();
			fluidParticles[i].Velocity = velocities[i];
			// TODO: Add collision (bouncing/reflecting) damping to the mix!?
			fluidParticles[i].MoveAndSlide();
		}
	}

	// Spiky and Smoothing curve kernels
	float DensityKernel(float dst, float radius)
	{
		if (dst >= radius) return 0;

		float volume = (Mathf.Pi * Mathf.Pow(radius, 4)) / 6;
		return (radius - dst) * (radius - dst) / volume;
	}

	// Derivative returns the slope of the kernel curve
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

	float CalculateDensity(Vector2 samplePoint, float smoothingRadius, float mass)
	{
		float density = 0;

		// Loop over all particle positions inside the smoothing radius
		foreach (int otherIndex in NeighborSearch(samplePoint, smoothingRadius))
		{
			float dst = (predictedPositions[otherIndex] - samplePoint).Length();
			float influence = DensityKernel(dst, smoothingRadius);
			density += mass * influence;
		}

		return density;
	}

	float CalculateNearDensity(Vector2 samplePoint, float smoothingRadius, float mass)
	{
		float nearDensity = 0;
		foreach (int otherIndex in NeighborSearch(samplePoint, smoothingRadius))
		{
			float dst = (predictedPositions[otherIndex] - samplePoint).Length();
			float nearInflunece = NearDensityKernel(dst, smoothingRadius);
			nearDensity += mass * nearInflunece;
		}

		return nearDensity;
	}

	// Convert density to pressure
	float PressureFromDensity(float density, float targetDensity, float pressureMultiplier)
	{
		float pressure = (density + targetDensity) * pressureMultiplier;
		return pressure;
	}

	// Convert near density to near pressure
	float NearPressureFromDensity(float nearDensity, float nearPressureMultiplier)
	{
		float nearPressure = nearPressureMultiplier * nearDensity;
		return nearPressure;
	}

	Vector2 CalculatePressureForce(int particleIndex)
	{
		Vector2 pressureForce = Vector2.Zero;
		Vector2 position = predictedPositions[particleIndex];

		// TODO: there may be a better way to optimize this, but it works for now. (used to be in a for loop ;o dummy)
		// Probably the usage of caching the values into a list is a better solution...
		float mass = fluidParticles[particleIndex].mass;
		float smoothingRadius = fluidParticles[particleIndex].smoothingRadius;
		float targetDensity = fluidParticles[particleIndex].targetDensity;
		float pressureMultiplier = fluidParticles[particleIndex].pressureMultiplier;
		float nearPressureMultiplier = fluidParticles[particleIndex].nearPressureMultiplier;
		bool sharedPressure = fluidParticles[particleIndex].sharedPressure;

		float density = densities[particleIndex];
		float pressure = PressureFromDensity(density, targetDensity, pressureMultiplier);

		float nearDensity = nearDensities[particleIndex];
		float nearPressure = NearPressureFromDensity(nearDensity, nearPressureMultiplier);

		foreach (int neighborIndex in NeighborSearch(position, smoothingRadius))
		{
			if (particleIndex == neighborIndex) continue;

			Vector2 offset = predictedPositions[neighborIndex] - predictedPositions[particleIndex];
			float dst = offset.Length();
			Vector2 dir = dst == 0 ? offset : offset / dst;

			float slope = DensityDerivative(dst, smoothingRadius);
			float nearSlope = NearDensityDerivative(dst, smoothingRadius);

			// TODO: Clean this up if possible!
			float neighborTargetDensity = fluidParticles[neighborIndex].targetDensity;
			float neighborPressureMultiplier = fluidParticles[neighborIndex].pressureMultiplier;
			float neighborNearPressureMultiplier = fluidParticles[neighborIndex].nearPressureMultiplier;

			float neighborDensity = densities[neighborIndex];
			float neighborPressure = PressureFromDensity(neighborDensity, neighborTargetDensity, neighborPressureMultiplier);

			float neighborNearDensity = nearDensities[neighborIndex];
			float neighborNearPressure = NearPressureFromDensity(neighborNearDensity, nearPressureMultiplier);

			// Calculate the shared pressure if desired
			if (sharedPressure)
            {
				pressure = (pressure + neighborPressure) * 0.5f;
				nearPressure = (nearPressure + neighborNearPressure) * 0.5f;
            }

			pressureForce += dir * slope * pressure * mass / density;
			pressureForce += dir * nearSlope * nearPressure * mass / nearDensity;
		}

		return pressureForce;
	}

	Vector2 CalculateViscosityForce(int particleIndex)
	{
		Vector2 viscosityForce = Vector2.Zero;
		Vector2 position = predictedPositions[particleIndex];
		Vector2 velocity = velocities[particleIndex];
		float smoothingRadius = fluidParticles[particleIndex].smoothingRadius;
		float viscosityStrength = fluidParticles[particleIndex].viscosityStrength;

		foreach (int otherIndex in NeighborSearch(position, smoothingRadius))
		{
			float dst = (position - predictedPositions[otherIndex]).Length();
			float influence = ViscositySmoothingKernel(dst, smoothingRadius);
			viscosityForce += (velocities[otherIndex] - velocity) * influence;
		}

		return viscosityForce * viscosityStrength;
	}

	/// Spatial Hashing!

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

	public void UpdateSpatialLookup(Vector2[] points)
	{
		// Create (unordered) spatial lookup
		Parallel.For(0, points.Length, i =>
		{
			(int cellX, int cellY) = PositionToCellCoord(points[i], chunkRadius);
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
	// // TODO: Loop over a cell offsets that match the size of the smoothing radius
	// This can be more expensive, but I haven't really figured out a better solution for this, the majority of the time we will probably use a small chunk...
	public List<int> NeighborSearch(Vector2 samplePoint, float radius)
	{
		// Find which cell the sample point is in (this wil be the center of our 3x3 block)
		(int centerX, int centerY) = PositionToCellCoord(samplePoint, chunkRadius);
		float sqrRadius = radius * radius;
		List<int> neighboringPoints = new List<int>();

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
					neighboringPoints.Add(particleIndex);
					// TODO: give this information to the fluid particle to create graphics and meta balls
					// Do something more! (add index value to list for calculations, change color of particle, change size?!)
				}
			}
		}

		return neighboringPoints;
	}

	public List<Vector2> NeighborSearchPositions(Vector2 samplePoint, float radius)
	{
		// Find which cell the sample point is in (this wil be the center of our 3x3 block)
		(int centerX, int centerY) = PositionToCellCoord(samplePoint, chunkRadius);
		float sqrRadius = radius * radius;
		List<Vector2> neighboringPositions = new List<Vector2>();

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
					neighboringPositions.Add(fluidParticles[particleIndex].GlobalPosition);
				}
			}
		}

		return neighboringPositions;
	}

	// TODO: There has to be a better way to do this, rather than recreating the arrays.
	// But doing so will lead to a deeper rabbit hole of updating the arrays into lists.
	// Which will be a hassle particularly in the spatial hashing section... (I'm tired)
	public void SpawnParticle(FluidParticle fluid)
    {
		numParticles += 1;

		// Add the new particle to the list.
		fluidParticles.Add(fluid);

		// Recreate particles arrays
		predictedPositions = new Vector2[numParticles];
		positions = new Vector2[numParticles];
		velocities = new Vector2[numParticles];
		densities = new float[numParticles];
		nearDensities = new float[numParticles];

		spatialLookUpParticleIndex = new int[numParticles];
		spatialLookupCellKey = new uint[numParticles];
		startIndices = new int[numParticles];
    }

	public void DrainParticle(FluidParticle fluid)
    {
        numParticles -= 1;

		// Remove the particle from the list
		fluidParticles.Remove(fluid);

		// Recreate particle
		predictedPositions = new Vector2[numParticles];
		positions = new Vector2[numParticles];
		velocities = new Vector2[numParticles];
		densities = new float[numParticles];
		nearDensities = new float[numParticles];

		spatialLookUpParticleIndex = new int[numParticles];
		spatialLookupCellKey = new uint[numParticles];
		startIndices = new int[numParticles];
    }

	void GravitationalAcceleration(int particleIndex)
    {
		Vector2 planetPos = Vector2.Zero;
		Vector2 down = planetPos - fluidParticles[particleIndex].GlobalPosition;
        fluidParticles[particleIndex].UpDirection = down;
    }
}