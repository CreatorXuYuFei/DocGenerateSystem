using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DocGenPlatform.Core.Models
{
    /// <summary>文档模板实体</summary>
    public class TemplateItem
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        /// <summary>模板名称</summary>
        public string TemplateName { get; set; } = string.Empty;
        /// <summary>模板功能描述（用于向量检索匹配）</summary>
        public string TemplateDesc { get; set; } = string.Empty;
        /// <summary>模板完整 Markdown 结构</summary>
        public string TemplateMarkdown { get; set; } = string.Empty;
        /// <summary>模板分类（合同/方案/周报/报告等）</summary>
        public string Category { get; set; } = string.Empty;
        /// <summary>扩展元数据</summary>
        public Dictionary<string, object> Metadata { get; set; } = new();
    }
}
