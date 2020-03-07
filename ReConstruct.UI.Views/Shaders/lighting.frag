#version 330 core
out vec4 FragColor;

uniform vec3 objectColor;
uniform vec3 lightColor;
uniform vec3 lightPos;
uniform vec3 viewPos;

in vec3 Normal; //The normal of the fragment is calculated in the vertex shader.
in vec3 FragPos; //The fragment position.

void main()
{
    // Ambient color.
    float ambientStrength = 0.15;
    vec3 ambient = ambientStrength * lightColor;

    // Light direction. The light points from the light to the fragment.
    vec3 norm = normalize(Normal);
    //vec3 norm = Normal;
    vec3 lightDir = normalize(lightPos - FragPos);

    // Diffuse lighting.
    float diff = max(dot(norm, lightDir), 0.0);
    vec3 diffuse = diff * lightColor;

    // Specular lighting.
    float specularStrength = 0.1;
    vec3 viewDir = normalize(viewPos - FragPos);
    vec3 reflectDir = reflect(-lightDir, norm);
    float spec = pow(max(dot(viewDir, reflectDir), 0.0), 62);
    vec3 specular = specularStrength * spec * lightColor;

    // Combine all lighting components.
    vec3 result = (ambient + diffuse + specular) * objectColor;
    //vec3 result = (ambient + diffuse) * objectColor;
    //vec3 result = ambient * objectColor;
    FragColor = vec4(result, 1.0);
    
    //Note we still use the light color * object color from the last tutorial.
    //This time the light values are in the phong model (ambient, diffuse and specular)
}