@tool
class_name ParticleSpawner
extends Node2D


## What type of particle do we want to spawn?
@export var fluid_scene : PackedScene = preload("uid://b7agk2p24fsqd")
## how many particles do we want to spawn?
@export_range(0, 56, 1, "or_greater") var num_particles : int = 132:
	set(value):
		num_particles = value
		if not Engine.is_editor_hint():
			create_particles()
		else:
			queue_redraw()
## How big are our particles?
@export var particle_size : float = 4
## How far apart are the [param num_particles] spaced?
@export_range(0.0, 1.0) var particle_spacing : float = 0.1

@export_group("Editor")
@export var particle_color := Color(0.09, 0.278, 0.639, 1.0):
	set(value):
		if Engine.is_editor_hint():
			queue_redraw()


func _ready() -> void:
	if Engine.is_editor_hint():
		queue_redraw()
		return
	
	if not fluid_scene:
		return
	
	if num_particles <= 0:
		return

	create_particles()


func _draw() -> void:
	if Engine.is_editor_hint():
		# Place particles in a grid formation
		var particles_per_row : int = sqrt(num_particles)
		var particles_per_col : int = (num_particles - 1) / particles_per_row + 1
		var spacing : float = particle_size * 2 + particle_spacing
		
		for i in num_particles:
			var x : float = (i % particles_per_row - particles_per_row / 2.0 + 0.5) * spacing
			var y : float = (i / particles_per_row - particles_per_col / 2.0 + 0.5) * spacing
			var draw_position := Vector2(x, y)
			draw_circle(draw_position, particle_size, particle_color)


func create_particles() -> void:
	# Place particles in a grid formation
	var particles_per_row : int = sqrt(num_particles)
	var particles_per_col : int = (num_particles - 1) / particles_per_row + 1
	var spacing : float = particle_size * 2 + particle_spacing
	
	for i in num_particles:
		var fluid_particle = fluid_scene.instantiate()
		add_child(fluid_particle)
		
		var x : float = (i % particles_per_row - particles_per_row / 2.0 + 0.5) * spacing
		var y : float = (i / particles_per_row - particles_per_col / 2.0 + 0.5) * spacing
		fluid_particle.position = Vector2(x, y)
