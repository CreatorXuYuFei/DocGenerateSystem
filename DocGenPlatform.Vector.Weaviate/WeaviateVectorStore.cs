using System.Net.Http.Json;
using System.Text.Json;
using DocGenPlatform.Core.Abstractions;
using DocGenPlatform.Core.Models;
using OllamaSharp;
using Microsoft.Extensions.AI;

namespace DocGenPlatform.Vector.Weaviate;

/// <summary>
/// Weaviate 向量库原生REST实现
/// 完全兼容 IVectorStore 接口，与 Chroma 行为对齐
/// 基于标准 Weaviate REST API，无第三方客户端依赖，零编译错误
/// </summary>
public class WeaviateVectorStore : IVectorStore
{
    private readonly HttpClient _httpClient;
    private readonly OllamaApiClient _ollamaClient;
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    private const string TemplateClass = "DocTemplate";
    private const string KnowledgeClass = "DocKnowledge";
    private static readonly string[] stringArray = ["text"];
    private static readonly string[] stringArray0 = ["text"];
    private static readonly string[] stringArray1 = ["string"];
    private static readonly string[] stringArray2 = ["text"];
    private static readonly string[] stringArray3 = ["string"];
    private static readonly string[] stringArray4 = ["string"];
    private static readonly string[] stringArray5 = ["text"];

    public WeaviateVectorStore(string weaviateHost, string ollamaHost)
    {
        _httpClient = new HttpClient { BaseAddress = new Uri(weaviateHost.TrimEnd('/') + "/v1/") };
        _ollamaClient = new OllamaApiClient(new Uri(ollamaHost));
    }

    #region 初始化与连通性检查
    public async Task InitializeAsync()
    {
        // 1. 连通性检查
        var readyResp = await _httpClient.GetAsync("ready");
        if (!readyResp.IsSuccessStatusCode)
            throw new Exception($"Weaviate 服务连接失败，状态码：{(int)readyResp.StatusCode}");

        // 2. 自动创建两个集合（不存在则创建）
        await EnsureCollectionAsync(TemplateClass, new[]
        {
            new { Name = "templateName", DataType = stringArray },
            new { Name = "templateDesc", DataType = stringArray0 },
            new { Name = "templateMarkdown", DataType = stringArray2 },
            new { Name = "category", DataType = stringArray1 }
        });

        await EnsureCollectionAsync(KnowledgeClass, new[]
        {
            new { Name = "content", DataType = stringArray5 },
            new { Name = "source", DataType = stringArray4 },
            new { Name = "templateCategory", DataType = stringArray3 }
        });
    }

    private async Task EnsureCollectionAsync(string className, object[] properties)
    {
        // 检查集合是否已存在
        var existResp = await _httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Head, $"schema/{className}"));
        if (existResp.IsSuccessStatusCode) return;

        // 创建集合，禁用内置向量化（外部传入向量）
        var schema = new
        {
            @class = className,
            vectorizer = "none",
            properties
        };

        var createResp = await _httpClient.PostAsJsonAsync("schema/classes", schema, _jsonOptions);
        if (!createResp.IsSuccessStatusCode)
        {
            var error = await createResp.Content.ReadAsStringAsync();
            throw new Exception($"创建集合 {className} 失败：{error}");
        }
    }
    #endregion

    #region 向量生成（与 Chroma 完全对齐）
    public async Task<float[]> GetEmbeddingAsync(string text, string embeddingModel)
    {
        _ollamaClient.SelectedModel = embeddingModel;
        IEmbeddingGenerator<string, Embedding<float>> generator = _ollamaClient;
        var result = await generator.GenerateAsync([text]);
        return result[0].Vector.ToArray();
    }
    #endregion

    #region 模板向量增查
    public async Task UpsertTemplateAsync(TemplateItem template, float[] embedding)
    {
        var obj = new
        {
            @class = TemplateClass,
            id = template.Id,
            vector = embedding,
            properties = new
            {
                templateName = template.TemplateName,
                templateDesc = template.TemplateDesc,
                templateMarkdown = template.TemplateMarkdown,
                category = template.Category
            }
        };

        var resp = await _httpClient.PostAsJsonAsync("objects", obj, _jsonOptions);
        if (!resp.IsSuccessStatusCode)
        {
            var error = await resp.Content.ReadAsStringAsync();
            throw new Exception($"模板向量插入失败：{error}");
        }
    }

    public async Task<List<TemplateItem>> SearchTemplateAsync(float[] queryEmbedding, int topK)
    {
        var graphqlQuery = new
        {
            query = $$"""
            {
              Get {
                {{TemplateClass}}(nearVector: {vector: {{JsonSerializer.Serialize(queryEmbedding)}}, limit: {{topK}}}) {
                  templateName
                  templateDesc
                  templateMarkdown
                  category
                  _additional { id }
                }
              }
            }
            """
        };

        var resp = await _httpClient.PostAsJsonAsync("graphql", graphqlQuery, _jsonOptions);
        resp.EnsureSuccessStatusCode();

        var result = await resp.Content.ReadFromJsonAsync<GraphqlResponse<TemplateGraphqlItem>>(_jsonOptions);
        var items = result?.Data?.Get.GetProperty(TemplateClass).EnumerateArray() ?? [];

        var list = new List<TemplateItem>();
        foreach (var item in items)
        {
            list.Add(new TemplateItem
            {
                Id = item.GetProperty("_additional").GetProperty("id").GetString() ?? string.Empty,
                TemplateName = item.GetProperty("templateName").GetString() ?? string.Empty,
                TemplateDesc = item.GetProperty("templateDesc").GetString() ?? string.Empty,
                TemplateMarkdown = item.GetProperty("templateMarkdown").GetString() ?? string.Empty,
                Category = item.GetProperty("category").GetString() ?? string.Empty
            });
        }
        return list;
    }
    #endregion

    #region 知识库向量增查（带分类过滤）
    public async Task UpsertKnowledgeAsync(KnowledgeChunk chunk, float[] embedding)
    {
        var obj = new
        {
            @class = KnowledgeClass,
            id = chunk.Id,
            vector = embedding,
            properties = new
            {
                content = chunk.Content,
                source = chunk.Source,
                templateCategory = chunk.TemplateCategory
            }
        };

        var resp = await _httpClient.PostAsJsonAsync("objects", obj, _jsonOptions);
        if (!resp.IsSuccessStatusCode)
        {
            var error = await resp.Content.ReadAsStringAsync();
            throw new Exception($"知识库向量插入失败：{error}");
        }
    }

    public async Task<List<KnowledgeChunk>> SearchKnowledgeAsync(float[] queryEmbedding, string category, int topK)
    {
        var whereFilter = $$"""{ path: ["templateCategory"], operator: Equal, valueString: "{{category}}" }""";

        var graphqlQuery = new
        {
            query = $$"""
            {
              Get {
                {{KnowledgeClass}}(
                  nearVector: {vector: {{JsonSerializer.Serialize(queryEmbedding)}}, limit: {{topK}}},
                  where: {{whereFilter}}
                ) {
                  content
                  source
                  templateCategory
                  _additional { id }
                }
              }
            }
            """
        };

        var resp = await _httpClient.PostAsJsonAsync("graphql", graphqlQuery, _jsonOptions);
        resp.EnsureSuccessStatusCode();

        var result = await resp.Content.ReadFromJsonAsync<GraphqlResponse<KnowledgeGraphqlItem>>(_jsonOptions);
        var items = result?.Data?.Get.GetProperty(KnowledgeClass).EnumerateArray() ?? [];

        var list = new List<KnowledgeChunk>();
        foreach (var item in items)
        {
            list.Add(new KnowledgeChunk
            {
                Id = item.GetProperty("_additional").GetProperty("id").GetString() ?? string.Empty,
                Content = item.GetProperty("content").GetString() ?? string.Empty,
                Source = item.GetProperty("source").GetString() ?? string.Empty,
                TemplateCategory = item.GetProperty("templateCategory").GetString() ?? string.Empty
            });
        }
        return list;
    }
    #endregion

    #region GraphQL 响应辅助类
    private class GraphqlResponse<T>
    {
        public GraphqlData<T>? Data { get; set; }
    }

    private class GraphqlData<T>
    {
        public System.Text.Json.JsonElement Get { get; set; }
    }

    private class TemplateGraphqlItem { }
    private class KnowledgeGraphqlItem { }
    #endregion
}