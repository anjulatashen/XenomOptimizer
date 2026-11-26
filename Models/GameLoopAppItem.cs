using System.Windows.Media;

namespace XenomOptimizer.Models;

public class GameLoopAppItem
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string PackageName { get; set; } = "";
    public string Description { get; set; } = "";
    public string BadgeText { get; set; } = "";
    public string BadgeColor { get; set; } = "#10B981";
    public string IconGlyph { get; set; } = "\uE7FC";
    public string ActionButtonText { get; set; } = "Launch";
    public string Category { get; set; } = "Game";
    public bool IsSpecialAction { get; set; } = false;
    public string SpecialActionId { get; set; } = "";
}
