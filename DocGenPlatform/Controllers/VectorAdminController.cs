using DocGenPlatform.Core.Abstractions;
using DocGenPlatform.Core.Models;
using DocGenPlatform.Tools;
using DocGenPlatform.Tools.Model;
using Microsoft.AspNetCore.Mvc;

namespace DocGenPlatform.Api.Controllers;

[ApiController]
[Route("api/vector-admin")]
public class VectorAdminController(IVectorStoreFactory vectorFactory) : ControllerBase
{
    private readonly IVectorStoreFactory _vectorFactory = vectorFactory;

    /// <summary>入库文档模板</summary>
    [HttpPost("upsert-template")]
    public async Task<IActionResult> UpsertTemplate([FromBody] VectorUpsertRequest request)
    {
        if (request.Template == null) return BadRequest("模板数据不能为空");

        var vectorStore = _vectorFactory.Create(request.VectorEngine);
        await vectorStore.InitializeAsync();

        var embedding = await vectorStore.GetEmbeddingAsync(request.Template.TemplateDesc, "bge-m3");
        await vectorStore.UpsertTemplateAsync(request.Template, embedding);

        //模板入库成功后存入原始模板匹配
        TemplateBindConfigHelper.SaveBind([new TemplateBindItem {Category =request.Template.Category, TemplateId = request.Template.Id, TemplateName = request.Template.TemplateAddress }]);
        return Ok(new { request.Template.Id, Message = "模板入库成功" });
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

        return Ok(new { request.Knowledge.Id, Message = "知识库入库成功" });
    }

    /// <summary>入库文档模板</summary>
    [HttpPost("get-template-all")]
    public IActionResult GetTemplateAll([FromBody] QueryTemplate request)
    {
        // 读取
        var binds = TemplateBindConfigHelper.ReadAllBind();
        //判断是否携带查询条件，组装查询条件
        if(request.TemplateId !=string.Empty)
            binds = binds.Where(o=>o.TemplateId == request.TemplateId).ToList();

        if (request.Category != string.Empty)
            binds = binds.Where(o => o.Category == request.Category).ToList();

        return Ok(new { binds });
    }
}