namespace SupplierDirectory.Api;

public partial class SupplierApiProgram
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.AddServiceDefaults();
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(options => options.SwaggerDoc("v1", new Microsoft.OpenApi.OpenApiInfo
        {
            Title = "Supplier Directory API",
            Version = "v1",
            Description = "Query active supplier capabilities and annual-spend ranges by procurement category."
        }));
        builder.Services.AddSingleton<SupplierStore>();
        builder.Services.AddProblemDetails();
        var app = builder.Build();
        app.UseExceptionHandler();
        await app.Services.GetRequiredService<SupplierStore>().InitializeAsync(app.Lifetime.ApplicationStopping);

        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();
        app.MapGet("/api/suppliers", async (string? category, SupplierStore store, CancellationToken cancellationToken) =>
            Results.Ok(await store.GetAsync(category, cancellationToken)))
            .WithSummary("Retrieve active suppliers from the supplier directory by procurement category.");
        app.MapDefaultEndpoints();
        await app.RunAsync();
    }
}
