using DocGenPlatform.Core.Abstractions;
using DocGenPlatform.Core.Enums;
using DocGenPlatform.Tools;
using NewLife.Serialization;
using System.ComponentModel;
using System.Diagnostics;
using System.IO.Compression;
using System.Text;

namespace DocGenPlatform.Convert;

/// <summary>基于 Pandoc 的文档格式转换服务</summary>
public class PandocDocConvertService : IDocConvertService
{
    public async Task<(byte[], string)> ConvertAsync(string markdownContent, DocExportType exportType)
    {
        if (exportType == DocExportType.Markdown)
            return (Encoding.UTF8.GetBytes(markdownContent), "");

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
            var startInfo = new ProcessStartInfo
            {
                FileName = ConfigHelper.GetAppSettingValue("Doc:DocToolAddress"),
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardErrorEncoding = Encoding.UTF8,
                StandardOutputEncoding = Encoding.UTF8
            };

            // 所有参数全部通过ArgumentList添加，和Arguments属性互斥
            startInfo.ArgumentList.Add("-s");
            startInfo.ArgumentList.Add(tempMd);
            startInfo.ArgumentList.Add("-o");
            startInfo.ArgumentList.Add(tempOut);
            startInfo.ArgumentList.Add("-f");
            startInfo.ArgumentList.Add("markdown");
            startInfo.ArgumentList.Add("-t");
            startInfo.ArgumentList.Add(format);

            //调整文档字体排版
            if (format.Equals("docx", StringComparison.OrdinalIgnoreCase))
            {
                // 加载自定义字体模板
                string templateFile = Path.Combine(AppContext.BaseDirectory, "Template", "font_template.docx");//await BuildTempFontTemplate(ConfigHelper.GetAppSettingValue("Doc:DocToolAddress")??"");
                if (File.Exists(templateFile))
                {
                    startInfo.ArgumentList.Add("--reference-doc");
                    startInfo.ArgumentList.Add(templateFile);
                }
            }
            else if (format.Equals("pdf", StringComparison.OrdinalIgnoreCase))
            {
                // 替换引擎为wkhtmltopdf，不再依赖xelatex/MiKTeX
                startInfo.ArgumentList.Add("--pdf-engine");
                startInfo.ArgumentList.Add("wkhtmltopdf");

                // PDF页面边距配置，替代原xelatex字体参数
                startInfo.ArgumentList.Add("-V");
                startInfo.ArgumentList.Add("margin-top=2cm");
                startInfo.ArgumentList.Add("-V");
                startInfo.ArgumentList.Add("margin-bottom=2cm");
                startInfo.ArgumentList.Add("-V");
                startInfo.ArgumentList.Add("margin-left=2cm");
                startInfo.ArgumentList.Add("-V");
                startInfo.ArgumentList.Add("margin-right=2cm");
                // 基础字体大小适配
                startInfo.ArgumentList.Add("-V");
                startInfo.ArgumentList.Add("font-size=12pt");
            }



            using var proc = Process.Start(startInfo);
            await proc!.WaitForExitAsync();

            if (proc.ExitCode != 0)
            {
                var error = await proc.StandardError.ReadToEndAsync();
                throw new Exception($"Pandoc 转换失败: {error}");
            }

            //上传文件
            string fileDic = FunctionTool.SendImgByCompany(tempOut);

            return (await File.ReadAllBytesAsync(tempOut), fileDic);
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

    /// <summary>
    /// 动态生成自定义字体的reference-docx模板
    /// </summary>
    private async Task<string> BuildTempFontTemplate(string pandocExe)
    {
        string tempDocx = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.docx");
        string unzipDir = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}");

        try
        {
            // 1. 从pandoc标准输出读取默认模板二进制流，写入本地docx文件
            var psi = new ProcessStartInfo(pandocExe)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true
            };
            psi.ArgumentList.Add("--print-default-data-file");
            psi.ArgumentList.Add("reference.docx");

            using var proc = Process.Start(psi)!;
            // 二进制流直接复制到文件，禁止用ReadToEnd读字符串
            using var fileStream = File.Create(tempDocx);
            await proc.StandardOutput.BaseStream.CopyToAsync(fileStream);
            await proc.WaitForExitAsync();
            fileStream.Close();

            if (proc.ExitCode != 0 || !File.Exists(tempDocx))
                throw new Exception("导出pandoc默认模板失败");

            // 2. 解压docx（本质是zip包）
            Directory.CreateDirectory(unzipDir);
            ZipFile.ExtractToDirectory(tempDocx, unzipDir, overwriteFiles: true);

            // 3. 修改主题字体配置
            string themeXmlPath = Path.Combine(unzipDir, "word", "theme", "theme1.xml");
            if (!File.Exists(themeXmlPath))
                throw new Exception("模板主题文件不存在，pandoc版本可能不兼容");

            string xmlContent = await File.ReadAllTextAsync(themeXmlPath);
            // 替换默认西文字体 Calibri → 微软雅黑
            xmlContent = xmlContent.Replace("w:val=\"Calibri\"", "w:val=\"Microsoft YaHei\"");
            // 替换默认标题字体 Cambria → 黑体
            xmlContent = xmlContent.Replace("w:val=\"Cambria\"", "w:val=\"SimHei\"");
            await File.WriteAllTextAsync(themeXmlPath, xmlContent);

            // 4. 重新打包回docx
            File.Delete(tempDocx);
            ZipFile.CreateFromDirectory(unzipDir, tempDocx, CompressionLevel.Optimal, false);

            //清理窗口进程占用
            proc.Dispose();

            return tempDocx;
        }
        catch
        {
            // 失败清理资源
            File.Delete(tempDocx);
            throw;
        }
        finally
        {
            // 清理解压临时目录
            try { if (Directory.Exists(unzipDir)) Directory.Delete(unzipDir, true); } catch { }
        }
    }
}