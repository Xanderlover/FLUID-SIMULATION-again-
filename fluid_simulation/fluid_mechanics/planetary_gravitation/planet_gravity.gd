extends Node2D


enum GravitationalForce {
	ATTRACT, ## Slurp the fluid closer to the planets surface.
	REPEL, ## Squirt the fluid outward away from the planets surface.
}

#@export var planet_mass : float = 5000
#@export var big_g : float = 1

## What gravitational force do we want to apply? @deprecated @experimental
@export var gravity_type : GravitationalForce
## Should we change the gravity to normal when exiting?
@export var change_gravity_exiting : bool = false
## TODO: Store the previous up direction and use that when exiting.
@export var exit_gravity_dir := Vector2(0, -1)

var previous_gravity_dir : Array[Vector2]
var bodies : Array[CharacterBody2D]


func _physics_process(delta: float) -> void:
	gravitational_acceleration()


func gravitational_acceleration():
	if bodies.size() < 0:
		return

	for i in bodies.size():
		var down := Vector2(global_position - bodies[i].global_position)
		bodies[i].up_direction = -down


func _on_gravitational_area_body_entered(body: CharacterBody2D) -> void:
	#if change_gravity_exiting:
		#previous_gravity_dir.append(body.up_direction)
	bodies.append(body)


func _on_gravitational_area_body_exited(body: CharacterBody2D) -> void:
	if change_gravity_exiting:
		body.up_direction = exit_gravity_dir
		#previous_gravity_dir.erase(body.up_direction)
	bodies.erase(body)
