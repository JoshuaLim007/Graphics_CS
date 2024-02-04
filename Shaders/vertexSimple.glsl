#version 410

//in from program
layout(location = 0) in vec3 aPosition;

uniform mat4 ViewMatrix;
uniform mat4 ProjectionMatrix;
uniform mat4 ModelMatrix;
invariant gl_Position;

void main() {
	gl_Position = ProjectionMatrix * ViewMatrix * ModelMatrix * vec4(aPosition, 1.0);
}