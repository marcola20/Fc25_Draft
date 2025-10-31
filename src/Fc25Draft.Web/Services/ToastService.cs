using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Fc25Draft.Web.Services;

public class ToastService
{
    private readonly ConcurrentDictionary<Guid, ToastMessage> _messages = new();

    public event Action<ToastMessage>? ToastAdded;
    public event Action<Guid>? ToastRemoved;

    public IReadOnlyCollection<ToastMessage> CurrentMessages => _messages.Values.ToList();

    public void ShowSuccess(string message, string? title = null, TimeSpan? duration = null)
        => Show(ToastLevel.Success, message, title, duration);

    public void ShowError(string message, string? title = null, TimeSpan? duration = null)
        => Show(ToastLevel.Error, message, title, duration);

    public void ShowInfo(string message, string? title = null, TimeSpan? duration = null)
        => Show(ToastLevel.Info, message, title, duration);

    public void Dismiss(Guid id)
    {
        if (_messages.TryRemove(id, out _))
        {
            ToastRemoved?.Invoke(id);
        }
    }

    private void Show(ToastLevel level, string message, string? title, TimeSpan? duration)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        var toast = new ToastMessage(
            Guid.NewGuid(),
            title ?? GetDefaultTitle(level),
            message.Trim(),
            level,
            duration ?? TimeSpan.FromSeconds(6));

        _messages[toast.Id] = toast;
        ToastAdded?.Invoke(toast);
    }

    private static string GetDefaultTitle(ToastLevel level) => level switch
    {
        ToastLevel.Success => "Sucesso",
        ToastLevel.Error => "Erro",
        _ => "Informação"
    };

    public enum ToastLevel
    {
        Info,
        Success,
        Error
    }

    public sealed record ToastMessage(Guid Id, string Title, string Message, ToastLevel Level, TimeSpan Duration);
}
