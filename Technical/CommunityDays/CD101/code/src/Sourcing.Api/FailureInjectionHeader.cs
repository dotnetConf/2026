using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Sourcing.Api;

public sealed class FailureInjectionHeader : IOperationFilter
{
    public const string Name = "X-Fault-Injection";

    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        if (context.ApiDescription.HttpMethod != "POST"
            || context.ApiDescription.RelativePath != "api/sourcing/requests")
            return;

        operation.Parameters ??= [];
        operation.Parameters.Add(new OpenApiParameter
        {
            Name = Name,
            In = ParameterLocation.Header,
            Required = false,
            Description = "Local Development fault control only. Leave empty for normal processing; use audit-persistence-failure to exercise transaction rollback.",
            Schema = new OpenApiSchema { Type = JsonSchemaType.String }
        });
    }
}
