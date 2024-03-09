#version 410

layout(location = 0) out vec2 oVelocity;
smooth in vec4 vPosition;
smooth in vec4 vPrevPosition;

void main(void) {
   vec2 a = (vPosition.xy / vPosition.w) * 0.5 + 0.5;
   vec2 b = (vPrevPosition.xy / vPrevPosition.w) * 0.5 + 0.5;
   oVelocity =  a - b;
}