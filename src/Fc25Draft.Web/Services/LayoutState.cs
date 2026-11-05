using System;
using System.Collections.Generic;
using Fc25Draft.Web.Models.Navigation;
using Microsoft.AspNetCore.Components;

namespace Fc25Draft.Web.Services;

public class LayoutState
{
    private PageDefinition? _currentDefinition;
    private IReadOnlyList<BreadcrumbSegment> _breadcrumbs = Array.Empty<BreadcrumbSegment>();
    private string? _subtitle;
    private RenderFragment? _actions;

    public event Action? StateChanged;

    public string Title => _currentDefinition?.Title ?? string.Empty;
    public string? Subtitle => _subtitle ?? _currentDefinition?.Subtitle;
    public IReadOnlyList<BreadcrumbSegment> Breadcrumbs => _breadcrumbs.Count > 0
        ? _breadcrumbs
        : _currentDefinition?.Breadcrumbs ?? Array.Empty<BreadcrumbSegment>();

    public RenderFragment? Actions => _actions;

    /// <summary>O cabeçalho como está agora, para a aba guardar e devolver quando voltar a ser a da frente.</summary>
    public Retrato Atual => new(_currentDefinition, _breadcrumbs, _subtitle, _actions);

    public void Restaurar(Retrato retrato) =>
        SetPage(retrato.Definicao, retrato.Caminho, retrato.Subtitulo, retrato.Acoes);

    public sealed record Retrato(
        PageDefinition? Definicao,
        IReadOnlyList<BreadcrumbSegment> Caminho,
        string? Subtitulo,
        RenderFragment? Acoes);

    public void SetPage(PageDefinition? definition, IReadOnlyList<BreadcrumbSegment>? breadcrumbs = null, string? subtitle = null, RenderFragment? actions = null)
    {
        _currentDefinition = definition;
        _breadcrumbs = breadcrumbs ?? Array.Empty<BreadcrumbSegment>();
        _subtitle = subtitle;
        _actions = actions;
        NotifyChanged();
    }

    public void Reset()
    {
        _currentDefinition = null;
        _breadcrumbs = Array.Empty<BreadcrumbSegment>();
        _subtitle = null;
        _actions = null;
        NotifyChanged();
    }

    private void NotifyChanged() => StateChanged?.Invoke();
}
