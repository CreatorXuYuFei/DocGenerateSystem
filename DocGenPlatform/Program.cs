using DocGenPlatform.Core.Abstractions;
using DocGenPlatform.Api.Infrastructure;
using DocGenPlatform.Convert;
using DocGenPlatform.SkKernel.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// 注册核心服务（全部面向接口注册）
builder.Services.AddSingleton<IVectorStoreFactory, VectorStoreFactory>();
builder.Services.AddSingleton<IDocConvertService, PandocDocConvertService>();
builder.Services.AddScoped<DocGenerateSkService>();
builder.Services.AddLogging(b => b.AddConsole().SetMinimumLevel(LogLevel.Debug));
var app = builder.Build();

// 中间件...
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.Run();