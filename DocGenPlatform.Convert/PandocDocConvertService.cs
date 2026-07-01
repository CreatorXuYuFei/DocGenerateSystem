using DocGenPlatform.Core.Abstractions;
using DocGenPlatform.Core.Enums;
using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace DocGenPlatform.Convert;

/// <summary>基于 Pandoc 的文档格式转换服务</summary>
public class PandocDocConvertService : IDocConvertService
{
    public async Task<byte[]> ConvertAsync(string markdownContent, DocExportType exportType)
    {
        if (exportType == DocExportType.Markdown)
            return Encoding.UTF8.GetBytes(markdownContent);

        var format = exportType switch
        {
            DocExportType.Word => "docx",
            DocExportType.Pdf => "pdf",
            DocExportType.Html => "html5",
            _ => "markdown"
        };

        // 创建临时输入文件
        var tempMd = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.md");
        var tempOut = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.{format}");
        await File.WriteAllTextAsync(tempMd, markdownContent, Encoding.UTF8);

        try
        {
            var process = new ProcessStartInfo
            {
                FileName = "C:\\Users\\mayn\\AppData\\Local\\Microsoft\\WinGet\\Packages\\JohnMacFarlane.Pandoc_Microsoft.Winget.Source_8wekyb3d8bbwe\\pandoc-3.10\\pandoc.exe",
                Arguments = $"-s \"{tempMd}\" -o \"{tempOut}\" -f markdown -t {format}",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true
            };

            using var proc = Process.Start(process);
            await proc!.WaitForExitAsync();

            if (proc.ExitCode != 0)
            {
                var error = await proc.StandardError.ReadToEndAsync();
                throw new Exception($"Pandoc 转换失败: {error}");
            }

            return await File.ReadAllBytesAsync(tempOut);
        }
        catch (Exception ex)
        {
            // 打断点查看以下信息
            string errMsg = $"启动Pandoc失败: {ex.Message}，异常类型: {ex.GetType().Name}";
            if (ex is Win32Exception winEx)
            {
                errMsg += $"，Win32错误码: {winEx.NativeErrorCode}";
            }
            throw new Exception(errMsg, ex);
        }
        finally
        {
            // 清理临时文件
            if (File.Exists(tempMd)) File.Delete(tempMd);
            if (File.Exists(tempOut)) File.Delete(tempOut);
        }
    }
}