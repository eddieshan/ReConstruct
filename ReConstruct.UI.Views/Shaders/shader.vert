#version 330 core

layout(location = 0) in vec3 vertextModelPosition;
//layout(location = 1) in vec3 vertexColor;
layout(location = 1) in vec3 aNormal;

uniform mat4 model;
uniform mat4 MVP;

//out vec3 fragmentColor;
out vec3 Normal;
out vec3 FragPos;

// Like C, we have an entrypoint function. In this case, it takes void and returns void, and must be named main.
// You can do all sorts of calculations here to modify your vertices, but right now, we don't need to do any of that.
// gl_Position is the final vertex position; pass a vec4 to it and you're done.
// Keep in mind that we only pass a vec3 to this shader; the fourth component of a vertex is known as "w".
// It's only used in some more advanced OpenGL functions; it's not needed here.
// so with a call to the vec4 function, we just give it a constant value of 1.0

void main(void)
{
    gl_Position =  MVP * vec4(vertextModelPosition, 1.0);
    FragPos = vec3(vec4(vertextModelPosition, 1.0) * model);
    //gl_Position = vec4(vertextModelPosition, 1.0);
    //fragmentColor = vertexColor;
    Normal = aNormal * mat3(transpose(inverse(model)));
}