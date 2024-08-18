using OneShelf.OneDragon.Processor.Commands;
using OneShelf.Telegram.Commands;
using OneShelf.Telegram.Model;
using OneShelf.Telegram.Services.Base;

namespace OneShelf.OneDragon.Processor.Services;

public class SingletonAbstractions : ISingletonAbstractions
{
    public List<List<Type>> GetCommandsGrid() => [
        [
            typeof(Default), // requires parameters
        ],
        [
            typeof(Start),
            typeof(Help),
        ],
        [
            typeof(UpdateCommands),
        ]
    ];

    public Type GetDefaultCommand() => typeof(Default);

    public Type GetHelpCommand() => typeof(Help);

    public Markdown GetStartResponse()
    {
        var result = new Markdown();
        result.AppendLine("Привет!");
        return result;
    }

    public Markdown GetHelpResponseHeader()
    {
        var result = new Markdown();
        result.AppendLine("Вот что я умею:");
        return result;
    }

    public string GetDialogContinuation() => "Давайте болтать дальше или посмотрите помощь - /help.";
}