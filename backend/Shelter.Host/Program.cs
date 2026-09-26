var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

app.Run();

/// <summary>Entry point; public so integration tests can use <c>WebApplicationFactory&lt;Program&gt;</c>.</summary>
public partial class Program;
