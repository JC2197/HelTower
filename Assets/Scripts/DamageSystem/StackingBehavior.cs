/// <summary>
/// Defines how damage over time effects stack when reapplied
/// </summary>
public enum StackingBehavior
{
    /// <summary>Increases stacks up to max, each stack does full damage</summary>
    Stack,
    
    /// <summary>Resets duration back to full, doesn't increase stacks</summary>
    Refresh,
    
    /// <summary>Adds duration to existing effect (up to max duration)</summary>
    Extend,
    
    /// <summary>Only replaces if new duration is longer</summary>
    KeepLongest
}
