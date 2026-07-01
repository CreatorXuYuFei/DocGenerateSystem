using DocGenPlatform.Core.Abstractions;
using DocGenPlatform.Core.Enums;
using DocGenPlatform.Vector.Chroma;
using DocGenPlatform.Vector.Weaviate;

namespace DocGenPlatform.Api.Infrastructure;

/// <summary>
/// 向量库工厂实现
/// 唯一允许引用 Chroma/Weaviate 具体实现的地方，放在组合根 Api 层
/// </summary>
public class VectorStoreFactory(IConfiguration configuration) : IVectorStoreFactory
{
    private readonly IConfiguration _configuration = configuration;

    public IVectorStore Create(VectorEngineType engineType)
    {
        var ollamaHost = _configuration["Ollama:Host"]
            ?? throw new ArgumentNullException("Ollama:Host 配置缺失");

        return engineType switch
        {
            VectorEngineType.Chroma => CreateChromaStore(ollamaHost),
            VectorEngineType.Weaviate => CreateWeaviateStore(ollamaHost),
            VectorEngineType.ChromaByVllm => CreateChromaStoreByVllm(ollamaHost),
            _ => throw new NotSupportedException($"不支持的向量引擎: {engineType}")
        };
    }

    private ChromaVectorStore CreateChromaStore(string ollamaHost)
    {
        var chromaHost = _configuration["Vector:Chroma:Host"]
            ?? throw new ArgumentNullException("Chroma 地址配置缺失");
        return new ChromaVectorStore(chromaHost, ollamaHost);
    }

    private WeaviateVectorStore CreateWeaviateStore(string ollamaHost)
    {
        var weaviateHost = _configuration["Vector:Weaviate:Host"]
            ?? throw new ArgumentNullException("Weaviate 地址配置缺失");
        return new WeaviateVectorStore(weaviateHost, ollamaHost);
    }

    private ChromaVectorStoreByVllm CreateChromaStoreByVllm(string ollamaHost)
    {
        var chromaHost = _configuration["Vector:Chroma:Host"]
            ?? throw new ArgumentNullException("Chroma 地址配置缺失");
        return new ChromaVectorStoreByVllm(chromaHost, ollamaHost);
    }
}