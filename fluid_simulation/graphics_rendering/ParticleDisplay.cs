using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

[GlobalClass]
public partial class ParticleDisplay : Node2D
{
	[Export] FluidSimulation fluidServer;
	[Export] private float smoothingRadius = 10.0f;
	public int numParticles;
	public List<FluidParticle> fluidParticles = new List<FluidParticle>();
	public float chunkRadius;
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


	//[ExportGroup ("Spatial Hashing Debugging")]
	[Export] private bool drawSpatialHashing = true;
	[Export] private Color gridColor = new Color("3039477f");
	[Export] private Color chunkColor = new Color("008a9180");
	[Export] private Color chunkOutlineColor = new Color("5bcefa");
	[Export] private Color particlesInChunkColor = new Color("dea6ff");
	[Export] private Color smoothingRadiusColor = new Color("ffffff");
	[Export] private float hashingThickness = -1.0f;

	public override void _Process(double delta)
	{
		// TODO: Loop over all the particles on a separate thread. And send their speed properties
		// to their shader, this might be better for the visuals. Optimize and collect data to see which is better!

		// Calculate MetaBalls
		/*Parallel.For(0, fluidSimulation.numParticles, i =>
		{
			// fluids[i].SetInstanceShaderParameter("velocity", fluids[i].Velocity)
		});*/

		QueueRedraw();
	}

    public override void _Draw()
    {
        DrawSpatialHashing();
    }

	public (int x, int y) PositionToCellCoord(Vector2 point, float radius)
	{
		int cellX = (int)Mathf.Floor(point.X / radius);
		int cellY = (int)Mathf.Floor(point.Y / radius);
		return (cellX, cellY);
	}

	private void DrawSpatialHashing() 
    {
		// Spatial hashing
		if (!drawSpatialHashing) return;

		int cellSize = (int)Mathf.Floor(smoothingRadius);

		for (int i = 0; i < numParticles; i++)
		{
			(int cellX, int cellY) = PositionToCellCoord(fluidParticles[i].GlobalPosition, chunkRadius);
			DrawRect(new Rect2(new Vector2(cellX, cellY) * cellSize, new Vector2(cellSize, cellSize)), gridColor, false, hashingThickness);
		}

		// Draw 3x3 grid around the sample point
		(int centerX, int centerY) = PositionToCellCoord(GetLocalMousePosition(), smoothingRadius);
		foreach ((int offsetX, int offsetY) in cellOffsets)
		{
			DrawRect(new Rect2(new Vector2I(centerX + offsetX, centerY + offsetY) * cellSize, new Vector2(cellSize, cellSize)), chunkColor, true);
			DrawRect(new Rect2(new Vector2I(centerX + offsetX, centerY + offsetY) * cellSize, new Vector2(cellSize, chunkRadius)), chunkOutlineColor, false, hashingThickness);
		}
		DrawCircle(GetLocalMousePosition(), smoothingRadius, smoothingRadiusColor, false, hashingThickness);

		// Test if spatial hashing works properly
		foreach (int i in fluidServer.NeighborSearch(GetLocalMousePosition(), smoothingRadius))
		{
			DrawCircle(fluidParticles[i].GlobalPosition, 2.5f, particlesInChunkColor);
		}
    }

	// Trying out different methods of checking neighbourig cells
	private void MidPointCircleSearch()
    {
		int cellSize = (int)Mathf.Floor(chunkRadius);

		// Draw the circle grid using calculated cell offsets (usually 3x3) around the sample point
		(int centerX, int centerY) = PositionToCellCoord(GetLocalMousePosition(), chunkRadius);
		// Calculate the cell offsets
		int offsetX = 0;
		int offsetY = (int)-smoothingRadius;
		int midPoint = (int)-smoothingRadius;
		while (offsetX < -offsetY)
		{
			// If the midpoint is outside the circle, we increment by one to return to stay inside
			if (midPoint > 0)
            {
                offsetY += 1;
				midPoint += 2*(offsetX+offsetY) + 1;
            }
            else
            {
                midPoint += 2*offsetX + 1;
            }

			// Draw the circle sides
			DrawRect(new Rect2(new Vector2I(centerX + offsetX, centerY + offsetY) * cellSize, new Vector2(cellSize, cellSize)), chunkOutlineColor, true);
			DrawRect(new Rect2(new Vector2I(centerX - offsetX, centerY + offsetY) * cellSize, new Vector2(cellSize, cellSize)), chunkOutlineColor, true);
			DrawRect(new Rect2(new Vector2I(centerX + offsetX, centerY - offsetY) * cellSize, new Vector2(cellSize, cellSize)), chunkOutlineColor, true);
			DrawRect(new Rect2(new Vector2I(centerX - offsetX, centerY - offsetY) * cellSize, new Vector2(cellSize, cellSize)), chunkOutlineColor, true);

			DrawRect(new Rect2(new Vector2I(centerX + offsetY, centerY + offsetX) * cellSize, new Vector2(cellSize, cellSize)), chunkOutlineColor, true);
			DrawRect(new Rect2(new Vector2I(centerX + offsetY, centerY - offsetX) * cellSize, new Vector2(cellSize, cellSize)), chunkOutlineColor, true);
			DrawRect(new Rect2(new Vector2I(centerX - offsetY, centerY + offsetX) * cellSize, new Vector2(cellSize, cellSize)), chunkOutlineColor, true);
			DrawRect(new Rect2(new Vector2I(centerX - offsetY, centerY - offsetX) * cellSize, new Vector2(cellSize, cellSize)), chunkOutlineColor, true);
			offsetX += 1;
		}
    }
}
