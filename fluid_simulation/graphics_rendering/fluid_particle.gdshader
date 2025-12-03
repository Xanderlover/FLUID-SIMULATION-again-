shader_type canvas_item;

uniform vec4 color : source_color = vec4(0.357, 0.808, 0.98, 1.0);
uniform sampler2D gradient_texture; // linear_clamp_sampler?

// Change the color depending on speed
uniform float velocity_max = 100.0f;
instance uniform vec2 velocity;

// Vertex testing
uniform float frequency = 0.5;
uniform float amplitude = 0.5;
uniform float wave_speed = 2.0;

uniform sampler2D SCREEN_TEXTURE : hint_screen_texture, filter_linear_mipmap;

void vertex() {
	//VERTEX.x += cos(-VERTEX.y * frequency + TIME * wave_speed) * amplitude;
	//VERTEX.y += cos(-VERTEX.x * frequency + TIME * wave_speed) * amplitude;
}

void fragment() {
	// Create the circle shape
	vec2 center_offset = (UV - 0.5f) * 2.0;
	float sqr_dst = dot(center_offset, center_offset);
	if (sqr_dst > 1.0f) discard;

	// Handle color based on the speed
	float speed = length(velocity) / 100.0;
	float speed_t = mix(0.0, 1.0, speed / velocity_max ); // Saturate, lerp between 0 and 1
	vec4 gradient_color = texture(gradient_texture, vec2(speed, 0.5), 0); // Sample based on speed

	// Blurring
	

	COLOR = gradient_color;
}

//void light() {
//	// Called for every pixel for every light affecting the CanvasItem.
//	// Uncomment to replace the default light processing function with this one.
//}
