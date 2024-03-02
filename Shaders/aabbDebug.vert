#version 410

//in from program
layout(location = 0) in vec3 aPosition;

uniform mat4 ProjectionViewMatrix;

void main() {
	gl_Position = ProjectionViewMatrix * vec4(aPosition.xyz, 1);
}