using DocGenPlatform.Core.Abstractions;
using DocGenPlatform.Core.Models;
using DocGenPlatform.SkKernel.Plugins;
using DocGenPlatform.Tools;
using Microsoft.Extensions.Configuration;
using Microsoft.SemanticKernel;
using System.Net.Http.Json;

namespace DocGenPlatform.SkKernel.Services;

/// <summary>文档生成核心编排服务</summary>
public class DocGenerateSkService(
    IVectorStoreFactory vectorFactory,
    IDocConvertService convertService,
    IConfiguration configuration)
{
    private readonly IVectorStoreFactory _vectorFactory = vectorFactory;
    private readonly IDocConvertService _convertService = convertService;
    private readonly string _ollamaHost = ConfigHelper.GetAppSettingValue("LLMSettings:BaseAddress")!;

    /// <summary>执行完整文档生成流程</summary>
    public async Task<(byte[],string)> GenerateDocumentAsync(DocGenerateRequest request)
    {
        // 1. 创建向量库实例
        var vectorStore = _vectorFactory.Create(request.VectorEngine);
        await vectorStore.InitializeAsync();

        // 2. 构建 SK 内核并注册插件
        var kernel = KernelBuilder.CreateOllamaKernel(_ollamaHost, request.LlmModel);
        kernel.Plugins.AddFromObject(
            new TemplateRetrievePlugin(vectorStore, request.EmbeddingModel),
            "TemplatePlugin");
        kernel.Plugins.AddFromObject(
            new RagKnowledgePlugin(vectorStore, request.EmbeddingModel),
            "KnowledgePlugin");
        kernel.Plugins.AddFromObject(new MarkdownFormatPlugin(), "FormatPlugin");

        // 3. 召回最优模板
        var templateFunc = kernel.Plugins["TemplatePlugin"]["search_template"];
        var templateResult = await kernel.InvokeAsync(templateFunc, new KernelArguments
        {
            ["userPrompt"] = request.UserPrompt,
            ["topK"] = request.TemplateTopK
        });
        var template = templateResult.GetValue<TemplateItem>()!;

        if (string.IsNullOrWhiteSpace(template.TemplateMarkdown))
            throw new Exception("未匹配到符合要求的文档模板，请先入库模板");

        // 4. 召回知识库素材
        var knowledgeFunc = kernel.Plugins["KnowledgePlugin"]["search_knowledge"];
        var knowledgeResult = await kernel.InvokeAsync(knowledgeFunc, new KernelArguments
        {
            ["query"] = request.UserPrompt,
            ["category"] = template.Category,
            ["topK"] = request.KnowledgeTopK
        });
        var knowledgeList = knowledgeResult.GetValue<List<KnowledgeChunk>>() ?? [];
        var knowledgeText = string.Join("\n\n---\n\n", knowledgeList.Select(k => k.Content));

        // 5. 构建强约束 Prompt
        var systemPrompt = BuildGeneratePrompt(request.UserPrompt, template.TemplateMarkdown, knowledgeText);

        //6.调用大模型
        string rawMarkdown = string.Empty;
        if (request.VectorEngine != Core.Enums.VectorEngineType.ChromaByVllm)
        {
            var rawResponse = await kernel.InvokePromptAsync(systemPrompt, new KernelArguments
            {
                ["temperature"] = 0.3,
                ["max_tokens"] = 8192
            });

            rawMarkdown = rawResponse.ToString().Trim();
        }
        else
            // ✅ 原生调用vLLM 生成文档，彻底避开 SK 兼容问题
            rawMarkdown = await CallVllmGenerateAsync(systemPrompt, request.LlmModel);


        // 7. 格式化清洗 ✅ 修正：同样用 GetValue 提取，避免 ToString() 不稳定
        var formatFunc = kernel.Plugins["FormatPlugin"]["fix_markdown"];
        var formatResult = await kernel.InvokeAsync(formatFunc, new KernelArguments
        {
            ["rawText"] = rawMarkdown
        });
        string standardMarkdown = formatResult.GetValue<string>() ?? rawMarkdown;


        // 8. 格式转换并返回
        return await _convertService.ConvertAsync(standardMarkdown.ToString(), request.ExportType);
    }

    /// <summary>构建结构化生成 Prompt</summary>
    private static string BuildGeneratePrompt(string userDemand, string templateMd, string knowledge)
    {
        return $"""
        ## 角色定位
        是专业的企业文档生成专家，严格遵循模板结构输出高质量文档，禁止输出任何与文档内容无关的解释、思考、寒暄。

        ## 核心规则
        1. 必须完整保留【标准模板】的所有标题层级、表格结构、列表顺序，不得增删一级/二级标题
        2. 【参考素材】仅用于填充模板内容，绝对不允许修改模板框架
        3. 输出必须是纯净的标准 Markdown 格式，不得包裹代码块，不得添加前置/后置说明
        4. 内容贴合用户需求，专业严谨，逻辑清晰

        ## 用户需求
        {userDemand}

        ## 标准模板结构（必须严格遵守）
        {templateMd}

        ## 参考素材（按需选用填充）
        {knowledge}

        ## 输出要求
        直接输出最终文档内容，从第一个 # 标题开始，不要任何前缀后缀。
        """;
    }

    private async Task<string> CallVllmGenerateAsync(string prompt, string modelName)
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(300) };

        var requestBody = new
        {
            model = modelName,
            messages = new[]
            {
            new { role = "user", content = prompt }
        },
            temperature = 0.3,
            // 建议调大，避免文档被截断（的测试里 finish_reason = length 就是被截断了）
            max_tokens = 8192
        };

        var response = await client.PostAsJsonAsync(
            $"{_ollamaHost.TrimEnd('/')}/v1/chat/completions",
            requestBody);

        response.EnsureSuccessStatusCode();

        // System.Text.Json 默认 UTF-8 解析，中文完全正常
        var result = await response.Content.ReadFromJsonAsync<ChatCompletionResponse>();
        string content = result?.Choices?.FirstOrDefault()?.Message?.Content ?? string.Empty;

        return content.Trim();
    }

    // 对齐 vLLM 返回结构的实体类
    private class ChatCompletionResponse
    {
        public List<ChoiceItem> Choices { get; set; } = [];

        public class ChoiceItem
        {
            public MessageItem Message { get; set; } = new();

            public class MessageItem
            {
                public string Role { get; set; } = string.Empty;
                public string Content { get; set; } = string.Empty;
                // 兼容推理模型的思考字段，不影响解析
                public string Reasoning { get; set; } = string.Empty;
            }
        }
    }
}