using RsaSignApi.Data;
using Microsoft.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddSingleton<MongoDbContext>();
builder.Services.AddEndpointsApiExplorer();
//builder.Services.AddSwaggerGen();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    //app.UseSwagger();
    //app.UseSwaggerUI();
}

//if (!app.Environment.IsDevelopment()) 
//{
//    app.UseHttpsRedirection();
//}

// Serve SPA static files
app.UseDefaultFiles();
app.UseStaticFiles();

app.UseAuthorization();
app.UseCors();
app.MapControllers();

// SPA fallback to index.html
app.MapFallbackToFile("index.html");

app.Run();