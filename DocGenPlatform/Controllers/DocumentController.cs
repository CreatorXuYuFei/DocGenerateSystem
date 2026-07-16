using DocGenPlatform.Core.Models;
using DocGenPlatform.SkKernel.Services;
using Microsoft.AspNetCore.Mvc;
using NewLife;

namespace DocGenPlatform.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DocumentController(DocGenerateSkService generateService) : ControllerBase
{
    private readonly DocGenerateSkService _generateService = generateService;

    /// <summary>生成结构化文档并返回文件信息</summary>
    [HttpPost("generate")]
    public async Task<GenerateDocumentResult> GenerateDocument([FromBody] DocGenerateRequest request)
    {
        var fileBytes = await _generateService.GenerateDocumentAsync(request);

        var fileExt = request.ExportType switch
        {
            Core.Enums.DocExportType.Word => ".docx",
            Core.Enums.DocExportType.Pdf => ".pdf",
            Core.Enums.DocExportType.Html => ".html",
            _ => ".md"
        };

        var contentType = request.ExportType switch
        {
            Core.Enums.DocExportType.Word => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            Core.Enums.DocExportType.Pdf => "application/pdf",
            Core.Enums.DocExportType.Html => "text/html",
            _ => "text/markdown"
        };

        return new GenerateDocumentResult
        {
            FileName = $"generated_document{fileExt}",
            ContentType = contentType,
            FileUrl = fileBytes.Item2,                           // 文件地址
            FileBase64 = fileBytes.Item1.ToBase64(),   // 文件流转 base64
            FileSize = fileBytes.Item1.Length                     // 文件大小（字节）
        };
    }
}