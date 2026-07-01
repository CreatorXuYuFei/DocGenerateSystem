using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DocGenPlatform.Core.Enums
{
    /// <summary>向量引擎类型</summary>
    public enum VectorEngineType
    {
        Chroma = 0,
        Weaviate = 1,
        ChromaByVllm = 2
    }
}
