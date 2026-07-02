using DocGenPlatform.Api.Infrastructure;
using DocGenPlatform.Core.Models;
using DocGenPlatform.SkKernel.Plugins;
using DocGenPlatform.SkKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel;

namespace DocGenPlatform.Api
{
    public class OneTest
    {
        public static void test()
        { 
            // ========== 1. 配置参数 ==========
            string ollamaHost = "http://localhost:11434";
            string llmModel = "qwen3:35b";
            string embeddingModel = "nomic-embed-text";
            string vectorEngine = "chroma"; // 与的向量库实现对应

            // ========== 2. 初始化向量库 ==========
            var vectorFactory = new VectorStoreFactory(null); // 的向量库工厂
            var vectorStore = vectorFactory.Create(Core.Enums.VectorEngineType.Chroma);
            vectorStore.InitializeAsync();

            // ========== 3. 构建 SK 内核并注册插件 ==========
            var kernel = KernelBuilder.CreateOllamaKernel(ollamaHost, llmModel);

            // 注册三大插件
            kernel.Plugins.AddFromObject(
                new TemplateRetrievePlugin(vectorStore, embeddingModel),
                "TemplatePlugin");
            kernel.Plugins.AddFromObject(
                new RagKnowledgePlugin(vectorStore, embeddingModel),
                "KnowledgePlugin");
            kernel.Plugins.AddFromObject(new MarkdownFormatPlugin(), "FormatPlugin");

            // ========== 4. 手动检索知识库（手动 RAG）==========
            string userQuery = "项目立项报告需要包含哪些章节？";
            string category = "project_report";
            int topK = 5;

            // 调用知识库检索插件
            var searchResult = kernel.InvokeAsync<List<KnowledgeChunk>>(
                "KnowledgePlugin",
                "search_knowledge",
                new KernelArguments
                {
                    ["query"] = userQuery,
                    ["category"] = category,
                    ["topK"] = topK
                });

            // 拼接检索结果为上下文
            string knowledgeContext = string.Join("\n\n", searchResult.Result.Select((c, i) => $"【参考资料{i + 1}】{c.Content}"));

            // ========== 5. 调用大模型生成回答 ==========
            var chatService = kernel.GetRequiredService<IChatCompletionService>();
            var history = new ChatHistory();

            // 系统提示词 + 知识库上下文 + 用户问题
            history.AddSystemMessage($"是专业文档助手，请基于以下参考资料回答用户问题，禁止编造内容。\n\n参考资料：\n{knowledgeContext}");
            history.AddUserMessage(userQuery);

            var response = chatService.GetChatMessageContentAsync(history);
            Console.WriteLine("回答结果：");
            Console.WriteLine(response.Result.Content);
        }
    }
}
