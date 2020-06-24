#version 330 core

struct Material {
    float     shininess;
};

struct DirLight {
    vec3 direction;
    //vec3 color;

    vec3 ambient;
    vec3 diffuse;
    vec3 specular;
};

struct PointLight {
    vec3 position;
    vec3 color;

    float constant;
    float linear;
    float quadratic;

    vec3 ambient;
    vec3 diffuse;
    vec3 specular;
};

#define NR_DIR_LIGHTS 2

uniform DirLight dirLights[NR_DIR_LIGHTS];
uniform Material material;
uniform vec3 viewPos;

out vec4 FragColor;
in vec3 Normal;
in vec3 FragPos;

vec3 CalcDirLight(DirLight light, vec3 normal, vec3 viewDir);
vec3 CalcPointLight(PointLight light, vec3 normal, vec3 fragPos, vec3 viewDir);

void main()
{
    vec3 norm = normalize(Normal);
    vec3 viewDir = normalize(viewPos - FragPos);

    vec3 result = vec3(0.0);
    for (int i = 0; i < NR_DIR_LIGHTS; i++)
        result += CalcDirLight(dirLights[i], norm, viewDir);

    FragColor = vec4(result, 1.0);
}

vec3 CalcDirLight(DirLight light, vec3 normal, vec3 viewDir)
{
    vec3 lightDir = normalize(-light.direction);

    // Diffuse.
    float diff = max(dot(normal, lightDir), 0.0);

    // Specular.
    vec3 reflectDir = reflect(-lightDir, normal);
    float spec = pow(max(dot(viewDir, reflectDir), 0.0), material.shininess);

    // Total.
    vec3 ambient  = light.ambient * vec3(1.0);
    vec3 diffuse  = light.diffuse * diff * vec3(1.0);
    vec3 specular = light.specular * spec * vec3(1.0);
    return (ambient + diffuse + specular);
}

vec3 CalcPointLight(PointLight light, vec3 normal, vec3 fragPos, vec3 viewDir)
{
    vec3 lightDir = normalize(light.position - fragPos);

    // Diffuse.
    float diff = max(dot(normal, lightDir), 0.0);

    // Specular.
    vec3 reflectDir = reflect(-lightDir, normal);
    float spec = pow(max(dot(viewDir, reflectDir), 0.0), material.shininess);

    // Attenuation.
    float distance    = length(light.position - fragPos);
    float attenuation = 1.0 / (light.constant + light.linear * distance + light.quadratic * (distance * distance));
    
    // Total.
    vec3 ambient  = light.ambient  * light.color;
    vec3 diffuse  = light.diffuse  * diff * light.color;
    vec3 specular = light.specular * spec * light.color;

    ambient  *= attenuation;
    diffuse  *= attenuation;
    specular *= attenuation;

    return (ambient + diffuse + specular);
}