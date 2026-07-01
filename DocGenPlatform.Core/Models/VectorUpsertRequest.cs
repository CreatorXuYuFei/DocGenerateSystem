using DocGenPlatform.Core.Enums;

namespace DocGenPlatform.Core.Models;

/// <summary>向量数据入库请求</summary>
public class VectorUpsertRequest
{
    public VectorEngineType VectorEngine { get; set; } = VectorEngineType.ChromaByVllm;
    public TemplateItem? Template { get; set; }
    public KnowledgeChunk? Knowledge { get; set; }
}