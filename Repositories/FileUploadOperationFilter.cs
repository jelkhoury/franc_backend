using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Linq;

public class FileUploadOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var hasFile = context.MethodInfo.GetParameters()
            .Any(p => p.ParameterType == typeof(IFormFile) ||
                      (p.ParameterType.IsGenericType &&
                       p.ParameterType.GetGenericArguments().Contains(typeof(IFormFile))));

        if (!hasFile) return;

        operation.RequestBody = new OpenApiRequestBody
        {
            Content =
            {
                ["multipart/form-data"] = new OpenApiMediaType
                {
                    Schema = new OpenApiSchema
                    {
                        Type = "object",
                        Properties = context.MethodInfo.GetParameters().ToDictionary(
                            p => p.Name,
                            p => p.ParameterType == typeof(IFormFile)
                                ? new OpenApiSchema { Type = "string", Format = "binary" }
                                : new OpenApiSchema { Type = "string" }),
                        Required = context.MethodInfo.GetParameters()
                            .Where(p => !p.IsOptional)
                            .Select(p => p.Name)
                            .ToHashSet()
                    }
                }
            }
        };
    }
}
