using DocGenPlatform.Core.Abstractions;
using DocGenPlatform.Core.Models;
using Microsoft.AspNetCore.Mvc;

namespace DocGenPlatform.Api.Controllers;

[ApiController]
[Route("api/vector-admin")]
public class VectorAdminController : ControllerBase
{
    private readonly IVectorStoreFactory _vectorFactory;

    public VectorAdminController(IVectorStoreFactory vectorFactory)
    {
        _vectorFactory = vectorFactory;
    }

    /// <summary>入库文档模板</summary>
    [HttpPost("upsert-template")]
    public async Task<IActionResult> UpsertTemplate([FromBody] VectorUpsertRequest request)
    {
        if (request.Template == null) return BadRequest("模板数据不能为空");

        var vectorStore = _vectorFactory.Create(request.VectorEngine);
        await vectorStore.InitializeAsync();

        var embedding = await vectorStore.GetEmbeddingAsync(request.Template.TemplateDesc, "bge-m3");
        await vectorStore.UpsertTemplateAsync(request.Template, embedding);

        return Ok(new { Id = request.Template.Id, Message = "模板入库成功" });
    }

    /// <summary>入库知识库分片</summary>
    [HttpPost("upsert-knowledge")]
    public async Task<IActionResult> UpsertKnowledge([FromBody] VectorUpsertRequest request)
    {
        if (request.Knowledge == null) return BadRequest("知识库数据不能为空");

        var vectorStore = _vectorFactory.Create(request.VectorEngine);
        await vectorStore.InitializeAsync();

        var embedding = await vectorStore.GetEmbeddingAsync(request.Knowledge.Content, "bge-m3");
        await vectorStore.UpsertKnowledgeAsync(request.Knowledge, embedding);

        return Ok(new { Id = request.Knowledge.Id, Message = "知识库入库成功" });
    }
}