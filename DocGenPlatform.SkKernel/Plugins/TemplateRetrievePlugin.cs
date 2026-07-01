using DocGenPlatform.Core.Abstractions;
using DocGenPlatform.Core.Models;
using Microsoft.SemanticKernel;
using System.ComponentModel;

namespace DocGenPlatform.SkKernel.Plugins;

/// <summary>模板检索插件</summary>
public class TemplateRetrievePlugin
{
    private readonly IVectorStore _vectorStore;
    private readonly string _embeddingModel;

    public TemplateRetrievePlugin(IVectorStore vectorStore, string embeddingModel)
    {
        _vectorStore = vectorStore;
        _embeddingModel = embeddingModel;
    }

    [KernelFunction("search_template")]
    [Description("根据用户需求语义匹配最优文档模板")]
    public async Task<TemplateItem> SearchTemplateAsync(
        [Description("用户文档生成需求文本")] string userPrompt,
        [Description("召回模板数量")] int topK)
    {
        var embedding = await _vectorStore.GetEmbeddingAsync(userPrompt, _embeddingModel);
        var templates = await _vectorStore.SearchTemplateAsync(embedding, topK);
        return templates.FirstOrDefault() ?? new TemplateItem();
    }
}