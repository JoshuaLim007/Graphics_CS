#version 410

//in from program
layout(location = 0) in vec3 aPosition;

out vec3 localPos;

uniform mat4 ViewMatrix;
uniform mat4 ProjectionMatrix;
invariant gl_Position;

void main() {
    localPos = aPosition;
    vec4 clipPos = ProjectionMatrix * ViewMatrix * vec4(localPos, 1.0);
    gl_Position = clipPos.xyww;
}