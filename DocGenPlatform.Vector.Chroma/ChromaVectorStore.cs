using DocGenPlatform.Core.Abstractions;
using DocGenPlatform.Core.Models;
using OllamaSharp;
using Microsoft.Extensions.AI;
using ChromaDB.Client;

namespace DocGenPlatform.Vector.Chroma;

/// <summary>
/// 严格对齐你提供的示例代码 | ChromaDB.Client 1.0.0-pre10
/// 兼容 IVectorStore 接口，无缝接入原有项目
/// </summary>
public class ChromaVectorStore : IVectorStore
{
    private readonly ChromaClient _chromaClient;
    private readonly ChromaCollectionClient _templateCollectionClient;
    private readonly ChromaCollectionClient _knowledgeCollectionClient;
    private readonly OllamaApiClient _ollamaClient;

    // 集合名称固定
    private const string TemplateCollectionName = "doc_templates";
    private const string KnowledgeCollectionName = "doc_knowledge";

    /// <summary>
    /// 构造函数：完全复刻你的示例写法
    /// </summary>
    public ChromaVectorStore(string chromaHost, string ollamaHost, HttpClient? httpClient = null)
    {
        // 1. 严格按照你的示例配置 Chroma
        var baseUrl = $"{chromaHost.TrimEnd('/')}/api/v1/";
        var cfg = new ChromaConfigurationOptions(baseUrl);
        httpClient ??= new HttpClient();
        _chromaClient = new ChromaClient(cfg, httpClient);

        // 2. 初始化 Ollama 向量生成客户端
        _ollamaClient = new OllamaApiClient(new Uri(ollamaHost));

        // 3. 创建/获取两个集合（模板库 + 知识库），复刻你的写法
        var templateColl = _chromaClient.GetOrCreateCollection(TemplateCollectionName).Result;
        _templateCollectionClient = new ChromaCollectionClient(templateColl, cfg, httpClient);

        var knowledgeColl = _chromaClient.GetOrCreateCollection(KnowledgeCollectionName).Result;
        _knowledgeCollectionClient = new ChromaCollectionClient(knowledgeColl, cfg, httpClient);
    }

    /// <summary>
    /// 初始化（空实现，构造函数已完成初始化，对齐你的代码）
    /// </summary>
    public Task InitializeAsync()
    {
        // 你的示例中无初始化方法，构造函数已完成所有操作
        return Task.CompletedTask;
    }

    /// <summary>
    /// 生成文本向量（Ollama 不变）
    /// </summary>
    public async Task<float[]> GetEmbeddingAsync(string text, string embeddingModel)
    {
        _ollamaClient.SelectedModel = embeddingModel;
        IEmbeddingGenerator<string, Embedding<float>> generator = _ollamaClient;
        var result = await generator.GenerateAsync([text]);
        return result[0].Vector.ToArray();
    }

    /// <summary>
    /// 插入模板向量：对齐你的 Add 方法
    /// </summary>
    public async Task UpsertTemplateAsync(TemplateItem template, float[] embedding)
    {
        var metadata = new Dictionary<string, object>(template.Metadata)
        {
            ["templateName"] = template.TemplateName,
            ["category"] = template.Category,
            ["templateMarkdown"] = template.TemplateMarkdown
        };

        // float[] 转 ReadOnlyMemory<float>（你的示例核心写法）
        var memVec = new ReadOnlyMemory<float>(embedding);

        // 用 Add 方法，完全匹配你的代码
        await _templateCollectionClient.Add(
            ids: [template.Id],
            embeddings: [memVec],
            metadatas: [metadata],
            documents: [template.TemplateDesc]);
    }

    /// <summary>
    /// 检索模板：严格对齐你的 Query 写法
    /// </summary>
    public async Task<List<TemplateItem>> SearchTemplateAsync(float[] queryEmbedding, int topK)
    {
        var memQuery = new ReadOnlyMemory<float>(queryEmbedding);

        // 你的示例写法：无多余参数，直接 Query
        var resultList = await _templateCollectionClient.Query(
            queryEmbeddings: memQuery,
            nResults: topK,
            include: ChromaQueryInclude.Documents | ChromaQueryInclude.Metadatas | ChromaQueryInclude.Distances);

        var items = new List<TemplateItem>();
        if (resultList == null || resultList.Count == 0) return items;

        foreach (var entry in resultList)
        {
            var meta = entry.Metadata ?? [];
            items.Add(new TemplateItem
            {
                Id = entry.Id,
                TemplateDesc = entry.Document ?? string.Empty,
                TemplateName = meta.TryGetValue("templateName", out var name) ? name.ToString()! : string.Empty,
                Category = meta.TryGetValue("category", out var cat) ? cat.ToString()! : string.Empty,
                TemplateMarkdown = meta.TryGetValue("templateMarkdown", out var md) ? md.ToString()! : string.Empty,
                Metadata = meta.ToDictionary(k => k.Key, v => v.Value)
            });
        }
        return items;
    }

    /// <summary>
    /// 插入知识库向量
    /// </summary>
    public async Task UpsertKnowledgeAsync(KnowledgeChunk chunk, float[] embedding)
    {
        var metadata = new Dictionary<string, object>(chunk.Metadata)
        {
            ["source"] = chunk.Source,
            ["templateCategory"] = chunk.TemplateCategory
        };

        var memVec = new ReadOnlyMemory<float>(embedding);
        await _knowledgeCollectionClient.Add(
            ids: [chunk.Id],
            embeddings: [memVec],
            metadatas: [metadata],
            documents: [chunk.Content]);
    }

    /// <summary>
    /// 检索知识库（带分类过滤）
    /// </summary>
    public async Task<List<KnowledgeChunk>> SearchKnowledgeAsync(float[] queryEmbedding, string category, int topK)
    {
        var memQuery = new ReadOnlyMemory<float>(queryEmbedding);
        var resultList = await _knowledgeCollectionClient.Query(
            queryEmbeddings: memQuery,
            nResults: topK,
            where: ChromaWhereOperator.Equal("templateCategory", category),
            include: ChromaQueryInclude.Documents| ChromaQueryInclude.Metadatas | ChromaQueryInclude.Distances);// ["documents", "metadatas", "distances"]

        var items = new List<KnowledgeChunk>();
        if (resultList == null || resultList.Count == 0) return items;

        foreach (var entry in resultList)
        {
            var meta = entry.Metadata ?? [];
            items.Add(new KnowledgeChunk
            {
                Id = entry.Id,
                Content = entry.Document ?? string.Empty,
                Source = meta.TryGetValue("source", out var src) ? src.ToString()! : string.Empty,
                TemplateCategory = meta.TryGetValue("templateCategory", out var cat) ? cat.ToString()! : string.Empty,
                Metadata = meta.ToDictionary(k => k.Key, v => v.Value)
            });
        }
        return items;
    }
}