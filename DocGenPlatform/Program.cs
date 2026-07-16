using DocGenPlatform.Core.Abstractions;
using DocGenPlatform.Api.Infrastructure;
using DocGenPlatform.Convert;
using DocGenPlatform.SkKernel.Services;
using CoreLogger;
using CoreLogger.Enum;
using DocGenPlatform.Tools;

var builder = WebApplication.CreateBuilder(args);
#region 初始化日志配置
SysLogHelper.Configure(opt =>
{
    // ================== 全局配置 ==================
    opt.GlobalMinLevel = LogLevel.Information;    // 全局最低日志级别（Debug及以上全部记录）
    opt.SampleRate = 1.0;                   // 日志采样率（1.0=100%记录）
    opt.TextTemplate = "{UtcTimestamp:yyyy-MM-dd HH:mm:ss} [{ThreadId}] [{Level}] [{Module}] {Scope} {Message}"; // 日志模板

    // ================== 控制台输出（开启） ==================
    opt.Console.Enabled = false;                // 启用控制台打印
    opt.Console.MinLevel = LogLevel.Information;     // 控制台最低级别
    opt.Console.Format = LogFormatType.Text;  // 输出格式：Text=文本 / Json=JSON

    // ================== 文件输出（核心配置） ==================
    opt.File.Enabled = true;                  // 启用文件日志
    opt.File.RootPath = Path.Combine(AppContext.BaseDirectory, "my_logs"); // 自定义存储目录
    opt.File.FileName = "app_runtime";        // 自定义日志文件名（无后缀）
    opt.File.RollingInterval = RollingInterval.Day; // 按天滚动文件
    opt.File.MaxFileSizeMB = 10;              // 单个文件最大10MB
    opt.File.MaxBackupCount = 30;             // 最多保留30个历史文件
    opt.File.EnableCompress = true;           // 自动压缩旧日志
    opt.File.Format = LogFormatType.Json;     // 文件日志格式

    // ================== 敏感信息脱敏（自动隐藏手机号/身份证） ==================
    opt.SensitiveMask.Enabled = true;
    // 新增自定义脱敏规则（邮箱）
    opt.SensitiveMask.Rules.Add(@"[\w]+@[\w]+\.[\w]+", "***@mail.com");

    // ================== 日志过滤 ==================
    opt.Filter.Enabled = false;
    opt.Filter.IncludeModules.Add("User");    // 只包含 User 模块
    opt.Filter.ExcludeModules.Add("System");  // 排除 System 模块
    opt.Filter.BlockKeywords.Add("测试密码");  // 屏蔽包含该关键词的日志
});
#endregion

//设置端口
string httpUrl = ConfigHelper.GetAppSettingValue("appSettings:IpAndPort");
Console.WriteLine($"程序监听端口：{httpUrl}");
builder.WebHost.UseUrls(httpUrl);
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
//if (app.Environment.IsDevelopment())
//{
//    app.UseSwagger();
//    app.UseSwaggerUI();
//}

app.UseSwagger();
app.UseSwaggerUI();

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.Run();