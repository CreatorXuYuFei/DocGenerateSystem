using DocGenPlatform.Core.Enums;
using System.ComponentModel.DataAnnotations;

namespace DocGenPlatform.Core.Models;

/// <summary>文档生成请求参数</summary>
public class DocGenerateRequest
{
    [Required(ErrorMessage = "文档生成需求不能为空")]
    public string UserPrompt { get; set; } = string.Empty;

    /// <summary>向量引擎，默认 Chroma</summary>
    public VectorEngineType VectorEngine { get; set; } = VectorEngineType.ChromaByVllm;

    /// <summary>导出格式，默认 Markdown</summary>
    public DocExportType ExportType { get; set; } = DocExportType.Markdown;

    /// <summary>模板召回数量</summary>
    public int TemplateTopK { get; set; } = 3;

    /// <summary>知识库召回数量</summary>
    public int KnowledgeTopK { get; set; } = 3;

    /// <summary>Ollama 生成模型名称</summary>
    public string LlmModel { get; set; } = "Qwen/Qwen3.6-35B-A3B";

    /// <summary>Ollama 嵌入模型名称</summary>
    public string EmbeddingModel { get; set; } = "bge-m3";
}