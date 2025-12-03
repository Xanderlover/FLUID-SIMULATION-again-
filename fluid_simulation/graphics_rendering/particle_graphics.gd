extends MeshInstance2D


@export var fluid : FluidParticle
var speed_parameter : String = "velocity"
var neighboring_particles_parameter : String = "neighbors"


func _process(delta: float) -> void:
	# Pass the velocity to the shader, to show varying speeds using color, based on the velocity
	set_instance_shader_parameter(speed_parameter, fluid.velocity)
	set_instance_shader_parameter(neighboring_particles_parameter, fluid.neighboringPosition)
