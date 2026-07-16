using DocGenPlatform.Core.Abstractions;
using DocGenPlatform.Core.Models;
using System.Text.Json;
using ChromaDB.Client;
using DocGenPlatform.Tools;

namespace DocGenPlatform.Vector.Chroma;

/// <summary>
/// Chroma 向量存储 + vLLM 嵌入生成（标准 OpenAI 兼容接口）
/// 完全兼容 IVectorStore 接口，上层业务无感知
/// </summary>
public class ChromaVectorStoreByVllm : IVectorStore
{
    private readonly ChromaClient _chromaClient;
    private readonly ChromaCollectionClient _templateCollectionClient;
    private readonly ChromaCollectionClient _knowledgeCollectionClient;
    private readonly HttpClient _httpClient;
    private readonly string _embeddingEndpoint;
    private readonly string _defaultEmbeddingModel = "";//默认向量模型

    // 集合名称固定
    private static readonly string TemplateCollectionName = ConfigHelper.GetAppSettingValue("Vector:TemplateCollectionName")!;
    private static readonly string KnowledgeCollectionName = ConfigHelper.GetAppSettingValue("Vector:KnowledgeCollectionName")!;

    // 复用 JSON 序列化配置
    private static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="chromaHost">Chroma 服务地址</param>
    /// <param name="embeddingHost">vLLM 嵌入服务根地址（如 http://localhost:8001）</param>
    /// <param name="httpClient">可复用的 HttpClient 实例</param>
    public ChromaVectorStoreByVllm(string chromaHost, string embeddingHost, HttpClient? httpClient = null)
    {
        // 1. 初始化 Chroma 客户端
        var chromaBaseUrl = $"{chromaHost.TrimEnd('/')}/api/v1/";
        var cfg = new ChromaConfigurationOptions(chromaBaseUrl);
        httpClient ??= new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        _httpClient = httpClient;
        _chromaClient = new ChromaClient(cfg, httpClient);

        // 2. 初始化 vLLM 嵌入接口地址（标准 OpenAI 路径）
        _embeddingEndpoint = $"{embeddingHost.TrimEnd('/')}/v1/embeddings";
        //_defaultEmbeddingModel = embeddingModel;

        // 3. 创建/获取两个向量集合
        var templateColl = _chromaClient.GetOrCreateCollection(TemplateCollectionName).Result;
        _templateCollectionClient = new ChromaCollectionClient(templateColl, cfg, httpClient);

        var knowledgeColl = _chromaClient.GetOrCreateCollection(KnowledgeCollectionName).Result;
        _knowledgeCollectionClient = new ChromaCollectionClient(knowledgeColl, cfg, httpClient);
    }

    public Task InitializeAsync() => Task.CompletedTask;

    /// <summary>
    /// 调用 vLLM 生成文本向量（标准 OpenAI 兼容协议）
    /// </summary>
    public async Task<float[]> GetEmbeddingAsync(string text, string embeddingModel)
    {
        var requestBody = new
        {
            model = string.IsNullOrEmpty(embeddingModel) ? _defaultEmbeddingModel : embeddingModel,
            input = text
        };

        string jsonBody = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(jsonBody, System.Text.Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync(_embeddingEndpoint, content);
        response.EnsureSuccessStatusCode();

        await using var responseStream = await response.Content.ReadAsStreamAsync();
        var result = await JsonSerializer.DeserializeAsync<EmbeddingResponse>(responseStream, _jsonOptions);

        if (result?.Data == null || result.Data.Count == 0)
            throw new Exception("vLLM 嵌入接口返回空结果");

        // 返回第一条向量结果
        return result.Data[0].Embedding;
    }

    #region 模板读写逻辑
    public async Task UpsertTemplateAsync(TemplateItem template, float[] embedding)
    {
        var metadata = new Dictionary<string, object>(template.Metadata)
        {
            ["templateName"] = template.TemplateName,
            ["category"] = template.Category,
            ["templateMarkdown"] = template.TemplateMarkdown
        };

        var memVec = new ReadOnlyMemory<float>(embedding);
        await _templateCollectionClient.Add(
            ids: [template.Id],
            embeddings: [memVec],
            metadatas: [metadata],
            documents: [template.TemplateDesc]);
    }

    public async Task<List<TemplateItem>> SearchTemplateAsync(float[] queryEmbedding, int topK)
    {
        var memQuery = new ReadOnlyMemory<float>(queryEmbedding);
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
    #endregion

    #region 知识库读写逻辑
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

    public async Task<List<KnowledgeChunk>> SearchKnowledgeAsync(float[] queryEmbedding, string category, int topK)
    {
        var memQuery = new ReadOnlyMemory<float>(queryEmbedding);
        var resultList = await _knowledgeCollectionClient.Query(
            queryEmbeddings: memQuery,
            nResults: topK,
            where: string.IsNullOrEmpty(category) ? null : ChromaWhereOperator.Equal("templateCategory", category),
            include: ChromaQueryInclude.Documents | ChromaQueryInclude.Metadatas | ChromaQueryInclude.Distances);

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
    #endregion

    #region 内部响应实体（仅用于 JSON 反序列化）
    private class EmbeddingResponse
    {
        public string Object { get; set; } = string.Empty;
        public List<EmbeddingData> Data { get; set; } = [];
        public string Model { get; set; } = string.Empty;
    }

    private class EmbeddingData
    {
        public string Object { get; set; } = string.Empty;
        public int Index { get; set; }
        public float[] Embedding { get; set; } = [];
    }
    #endregion
}