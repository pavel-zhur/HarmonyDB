namespace OneShelf.Common.Database.Songs.Model.Enums;

public enum InteractionType
{
    Dialog,
    Start,
    InlineQuery,
    ChosenInlineResult,
    PdfGeneration,
    PublicChatterMessage,
    OwnChatterMessage,

    [Obsolete]
    OwnChatterMessageResponse,
    [Obsolete]
    OwnChatterMessageFunctionCall,

    OwnChatterMemoryPoint,

    OwnChatterSystemMessage,
    OwnChatterVersion,
    OwnChatterImagesVersion,

    [Obsolete]
    OwnChatterPrefix,

    OwnChatterFrequencyPenalty,
    OwnChatterPresencePenalty,
    OwnChatterResetDialog,

    SongImages,

    NeverPromoteTopics,
    AskWeb,
}