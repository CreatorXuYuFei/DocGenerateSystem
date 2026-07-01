using DocGenPlatform.Core.Enums;

namespace DocGenPlatform.Core.Abstractions;

/// <summary>向量库工厂抽象</summary>
public interface IVectorStoreFactory
{
    /// <summary>根据引擎类型创建向量存储实例</summary>
    IVectorStore Create(VectorEngineType engineType);
}