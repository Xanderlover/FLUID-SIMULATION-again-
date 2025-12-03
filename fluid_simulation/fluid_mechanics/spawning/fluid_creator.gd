extends Area2D


signal squirt_fluid

## What type of particle do we want to spawn?
@export var fluid_scene : PackedScene = preload("uid://b7agk2p24fsqd")

var input_instantiate : String = 'force_push'
var desired_instantiate : bool = false

var input_swallow : String = "force_pull"
var desired_swallow : bool = false


func _input(event: InputEvent) -> void:
	if event.is_action_pressed(input_instantiate):
		desired_instantiate = true
	elif event.is_action_released(input_instantiate):
		desired_instantiate = false

	if event.is_action_pressed(input_swallow):
		desired_swallow = true
	elif event.is_action_released(input_swallow):
		desired_swallow = false


func _process(delta: float) -> void:
	#global_position = get_global_mouse_position()
	look_at(get_global_mouse_position())
	
	if desired_instantiate:
		squirt_fluid.emit()
		#spawn_particles()
		squirt_particles()


func squirt_particles() -> void:
	var fluid_particle = fluid_scene.instantiate()
	get_owner().add_child(fluid_particle) # TODO: change this to a proper instancing system
	fluid_particle.global_rotation = rotation
	fluid_particle.global_position = global_position
	fluid_particle.velocity += fluid_particle.velocity.rotated(rotation)


func spawn_particles() -> void:
	var fluid_particle = fluid_scene.instantiate()
	get_owner().add_child(fluid_particle) # TODO: change this to a proper instancing system
	fluid_particle.global_position = global_position


func _on_body_entered(particle: FluidParticle) -> void:
	if desired_swallow:
		particle.DrainParticle()
