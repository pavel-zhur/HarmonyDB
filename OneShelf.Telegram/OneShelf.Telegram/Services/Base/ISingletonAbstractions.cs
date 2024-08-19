using OneShelf.Telegram.Model;

namespace OneShelf.Telegram.Services.Base;

public interface ISingletonAbstractions
{
    List<List<Type>> GetCommandsGrid();
    Type? GetDefaultCommand();
    Type GetHelpCommand();
    Markdown GetStartResponse();
    Markdown GetHelpResponseHeader();
    string? DialogContinuation { get; }
    string CommandNotFound { get; }
    string BackgroundErrors { get; }
    string BackgroundOperationComplete { get; }
    string OperationError { get; }
    string? NoOperationPlaceholder { get; }
    string MiddleCommandResponsePostfix { get; }
}