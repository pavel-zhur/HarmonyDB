using OneShelf.Telegram.Model;

namespace OneShelf.Telegram.Services.Base;

public interface ISingletonAbstractions
{
    List<List<Type>> GetCommandsGrid();
    Type GetDefaultCommand();
    Type GetHelpCommand();
    Markdown GetStartResponse();
    Markdown GetHelpResponseHeader();
}