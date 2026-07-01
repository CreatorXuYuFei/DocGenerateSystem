using DocGenPlatform.Core.Models;

namespace DocGenPlatform.Core.Abstractions;

/// <summary>向量存储统一抽象接口</summary>
public interface IVectorStore
{
    /// <summary>初始化向量库连接与集合</summary>
    Task InitializeAsync();

    /// <summary>生成文本向量</summary>
    Task<float[]> GetEmbeddingAsync(string text, string embeddingModel);

    /// <summary>新增/更新模板向量</summary>
    Task UpsertTemplateAsync(TemplateItem template, float[] embedding);

    /// <summary>相似度检索模板</summary>
    Task<List<TemplateItem>> SearchTemplateAsync(float[] queryEmbedding, int topK);

    /// <summary>新增/更新知识库分片向量</summary>
    Task UpsertKnowledgeAsync(KnowledgeChunk chunk, float[] embedding);

    /// <summary>按分类相似度检索知识库</summary>
    Task<List<KnowledgeChunk>> SearchKnowledgeAsync(float[] queryEmbedding, string category, int topK);
}