using DocGenPlatform.Core.Abstractions;
using DocGenPlatform.Core.Models;
using Microsoft.SemanticKernel;
using System.ComponentModel;

namespace DocGenPlatform.SkKernel.Plugins;

/// <summary>RAG 知识库检索插件</summary>
public class RagKnowledgePlugin
{
    private readonly IVectorStore _vectorStore;
    private readonly string _embeddingModel;

    public RagKnowledgePlugin(IVectorStore vectorStore, string embeddingModel)
    {
        _vectorStore = vectorStore;
        _embeddingModel = embeddingModel;
    }

    [KernelFunction("search_knowledge")]
    [Description("根据模板分类检索相关知识库素材")]
    public async Task<List<KnowledgeChunk>> SearchKnowledgeAsync(
        [Description("检索查询文本")] string query,
        [Description("模板分类标签")] string category,
        [Description("召回分片数量")] int topK)
    {
        var embedding = await _vectorStore.GetEmbeddingAsync(query, _embeddingModel);
        return await _vectorStore.SearchKnowledgeAsync(embedding, category, topK);
    }
}