#version 330 core

layout(location = 0) in vec3 vertextModelPosition;
layout(location = 1) in vec3 aNormal;

uniform mat4 model;
uniform mat4 MVP;

out vec3 Normal;
out vec3 FragPos;

void main(void)
{
    gl_Position =  MVP * vec4(vertextModelPosition, 1.0);
    FragPos = vec3(vec4(vertextModelPosition, 1.0) * model);
    Normal = aNormal * mat3(transpose(inverse(model)));
}