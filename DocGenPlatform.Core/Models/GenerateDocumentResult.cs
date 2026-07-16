using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DocGenPlatform.Core.Models
{
    /// <summary>文档生成结果</summary>
    public class GenerateDocumentResult
    {
        public string FileName { get; set; }
        public string ContentType { get; set; }
        public string FileUrl { get; set; }
        public string FileBase64 { get; set; }
        public long FileSize { get; set; }
    }
}
