using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Reflection;

namespace FrancProject.Helpers;

/// <summary>
/// Maps multipart/form-data actions for Swagger (including IFormFile inside form DTOs).
/// </summary>
public class FileUploadOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var formParameters = context.MethodInfo.GetParameters()
            .Where(p => p.GetCustomAttribute<FromFormAttribute>() != null)
            .ToList();

        if (formParameters.Count == 0)
            return;

        if (!formParameters.Any(p => UsesFormFile(p.ParameterType)))
            return;

        var properties = new Dictionary<string, OpenApiSchema>();

        foreach (var parameter in formParameters)
        {
            if (IsFormFileType(parameter.ParameterType))
            {
                properties[parameter.Name!] = BinarySchema();
                continue;
            }

            foreach (var property in parameter.ParameterType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                properties[property.Name] = SchemaForProperty(property);
            }
        }

        operation.RequestBody = new OpenApiRequestBody
        {
            Content =
            {
                ["multipart/form-data"] = new OpenApiMediaType
                {
                    Schema = new OpenApiSchema
                    {
                        Type = "object",
                        Properties = properties
                    }
                }
            }
        };

        operation.Parameters.Clear();
    }

    private static bool UsesFormFile(Type type)
    {
        if (IsFormFileType(type))
            return true;

        return type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Any(p => IsFormFileType(p.PropertyType));
    }

    private static bool IsFormFileType(Type type)
    {
        if (type == typeof(IFormFile))
            return true;

        if (Nullable.GetUnderlyingType(type) == typeof(IFormFile))
            return true;

        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IEnumerable<>))
        {
            var arg = type.GetGenericArguments()[0];
            return arg == typeof(IFormFile);
        }

        return false;
    }

    private static OpenApiSchema SchemaForProperty(PropertyInfo property)
    {
        var type = property.PropertyType;

        if (IsFormFileType(type))
            return BinarySchema();

        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IEnumerable<>))
        {
            var elementType = type.GetGenericArguments()[0];
            if (elementType == typeof(IFormFile))
            {
                return new OpenApiSchema
                {
                    Type = "array",
                    Items = BinarySchema()
                };
            }
        }

        if (type == typeof(int) || type == typeof(int?))
            return new OpenApiSchema { Type = "integer", Format = "int32" };

        if (type == typeof(bool) || type == typeof(bool?))
            return new OpenApiSchema { Type = "boolean" };

        return new OpenApiSchema { Type = "string" };
    }

    private static OpenApiSchema BinarySchema() =>
        new() { Type = "string", Format = "binary" };
}
