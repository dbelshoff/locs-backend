using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using System;
using System.Linq;

namespace projetoX;

public class FileUploadOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var formParameters = context.ApiDescription.ParameterDescriptions
            .Where(p => p.Source.Id == "Form")
            .ToList();

        if (formParameters.Any())
        {
            // Limpa parâmetros existentes para evitar duplicatas
            operation.Parameters.Clear();

            // Define o RequestBody como multipart/form-data
            operation.RequestBody = new OpenApiRequestBody
            {
                Content = new Dictionary<string, OpenApiMediaType>
                {
                    ["multipart/form-data"] = new OpenApiMediaType
                    {
                        Schema = new OpenApiSchema
                        {
                            Type = "object",
                            Properties = formParameters.ToDictionary(
                                param => param.Name,
                                param =>
                                {
                                    if (param.Type == typeof(IFormFile))
                                    {
                                        return new OpenApiSchema
                                        {
                                            Type = "string",
                                            Format = "binary",
                                            Description = "Arquivo para upload (JPEG ou PNG)"
                                        };
                                    }
                                    return new OpenApiSchema
                                    {
                                        Type = param.Type == typeof(string) ? "string" : "object",
                                        Description = param.Name == "placeId" ? "ID do estabelecimento" : null
                                    };
                                }
                            ),
                            Required = formParameters
                                .Where(p => p.IsRequired || p.Type == typeof(IFormFile))
                                .Select(p => p.Name)
                                .ToHashSet()
                        },
                        Encoding = formParameters.ToDictionary(
                            param => param.Name,
                            param => new OpenApiEncoding
                            {
                                ContentType = param.Type == typeof(IFormFile) ? "application/octet-stream" : null
                            }
                        )
                    }
                }
            };
        }
    }
}