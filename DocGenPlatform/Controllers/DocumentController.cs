using DocGenPlatform.Core.Models;
using DocGenPlatform.SkKernel.Services;
using Microsoft.AspNetCore.Mvc;

namespace DocGenPlatform.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DocumentController(DocGenerateSkService generateService) : ControllerBase
{
    private readonly DocGenerateSkService _generateService = generateService;

    /// <summary>生成结构化文档并下载</summary>
    [HttpPost("generate")]
    public async Task<IActionResult> GenerateDocument([FromBody] DocGenerateRequest request)
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

        return File(fileBytes, contentType, $"generated_document{fileExt}");
    }
}