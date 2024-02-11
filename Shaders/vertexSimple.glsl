#version 410

//in from program
layout(location = 0) in vec3 aPosition;

uniform mat4 ProjectionViewMatrix;
uniform mat4 ModelMatrix;

void main() {
	gl_Position = ProjectionViewMatrix * ModelMatrix * vec4(aPosition, 1.0);
}