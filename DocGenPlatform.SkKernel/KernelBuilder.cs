using Microsoft.SemanticKernel;

namespace DocGenPlatform.SkKernel;

public static class KernelBuilder
{
    /// <summary>
    /// 基于标准 OpenAI 兼容接口创建 Kernel（通用于 Ollama / vLLM / LM Studio 等）
    /// </summary>
    /// <param name="baseAddress">推理服务根地址，如 http://localhost:11434</param>
    /// <param name="modelId">模型完整名称</param>
    /// <param name="apiKey">API 密钥，本地服务可随意填写</param>
    /// <returns>Kernel 实例</returns>
    public static Kernel CreateOpenAICompatibleKernel(
        string baseAddress,
        string modelId,
        string apiKey = "sk-local")
    {
        var builder = Kernel.CreateBuilder();

        builder.AddOpenAIChatCompletion(
            modelId: modelId,
            apiKey: apiKey,
            endpoint: new Uri($"{baseAddress.TrimEnd('/')}/v1")
        );

        return builder.Build();
    }

    // 保留原方法，向后兼容
    public static Kernel CreateOllamaKernel(string ollamaHost, string modelName)
        => CreateOpenAICompatibleKernel(ollamaHost, modelName, "ollama");
}