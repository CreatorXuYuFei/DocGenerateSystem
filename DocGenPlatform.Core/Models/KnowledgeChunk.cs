using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DocGenPlatform.Core.Models
{
    /// <summary>RAG 知识库分片实体</summary>
    public class KnowledgeChunk
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        /// <summary>分片文本内容</summary>
        public string Content { get; set; } = string.Empty;
        /// <summary>数据来源</summary>
        public string Source { get; set; } = string.Empty;
        /// <summary>关联模板分类</summary>
        public string TemplateCategory { get; set; } = string.Empty;
        /// <summary>扩展元数据</summary>
        public Dictionary<string, object> Metadata { get; set; } = new();
    }
}
