shader_type canvas_item;

uniform float u_time = 0.0;
uniform float u_motion_scale = 1.0;
uniform float u_mobile_soften = 0.0;

float hash(vec2 p) {
	return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453123);
}

float noise(vec2 p) {
	vec2 i = floor(p);
	vec2 f = fract(p);

	float a = hash(i);
	float b = hash(i + vec2(1.0, 0.0));
	float c = hash(i + vec2(0.0, 1.0));
	float d = hash(i + vec2(1.0, 1.0));

	vec2 u = f * f * (3.0 - 2.0 * f);
	return mix(a, b, u.x) + (c - a) * u.y * (1.0 - u.x) + (d - b) * u.x * u.y;
}

float fbm(vec2 p) {
	float sum = 0.0;
	float amp = 0.55;
	for (int i = 0; i < 4; i++) {
		sum += amp * noise(p);
		p = p * 2.03 + vec2(10.4, 4.2);
		amp *= 0.5;
	}
	return sum;
}

void fragment() {
	vec2 uv = UV;
	float t = u_time * (0.62 + 0.38 * (1.0 - u_mobile_soften)) * u_motion_scale;

	float cloud = fbm(uv * vec2(3.1, 5.8) + vec2(0.0, t * 0.08));
	float grain = noise(uv * vec2(140.0, 95.0) + vec2(t * 0.30, 0.0));
	float bands = 0.5 + 0.5 * sin(uv.y * 24.0 + t * 0.75);
	float horizon = exp(-pow((uv.y - 0.16) * 8.5, 2.0));

	vec3 deep = vec3(0.018, 0.012, 0.070);
	vec3 mid = vec3(0.052, 0.018, 0.130);
	vec3 top = vec3(0.090, 0.028, 0.185);

	vec3 col = mix(deep, mid, clamp(uv.y * 1.3, 0.0, 1.0));
	col = mix(col, top, clamp(0.20 + cloud * 0.65, 0.0, 1.0));
	col += vec3(0.24, 0.07, 0.40) * horizon * (0.24 + 0.20 * bands);
	col += vec3(0.020, 0.010, 0.045) * (grain - 0.5) * (0.5 + 0.5 * cloud);

	vec2 centered = (uv - vec2(0.5)) * vec2(1.55, 1.08);
	float vignette = smoothstep(1.30, 0.35, length(centered));
	col *= mix(0.70, 1.0, vignette);

	COLOR = vec4(col, 1.0);
}
