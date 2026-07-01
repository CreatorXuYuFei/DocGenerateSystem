using DocGenPlatform.Core.Enums;

namespace DocGenPlatform.Core.Abstractions;

/// <summary>文档格式转换服务抽象</summary>
public interface IDocConvertService
{
    /// <summary>Markdown 转换为目标格式二进制流</summary>
    Task<byte[]> ConvertAsync(string markdownContent, DocExportType exportType);
}