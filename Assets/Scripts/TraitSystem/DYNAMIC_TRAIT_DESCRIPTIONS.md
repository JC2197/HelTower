# Dynamic Trait Description System

## Overview
The **TraitDescriptionBuilder** automatically generates trait descriptions based on their effects and tier scaling. This eliminates the need to manually write descriptions and ensures values always reflect the actual tier-scaled numbers.

## Key Features
✅ **Automatically generates descriptions** from trait effects  
✅ **Applies tier scaling** - shows actual values for higher tier traits  
✅ **Supports all effect types**:
- Stat Modifiers (Flat & Percentage)
- Ability Modifiers (Damage, Cooldown, Range, etc.)
- Tag-Based Ability Modifiers (affect all abilities with a tag)
- Status Effect Modifiers (Bleed, Burn, Poison, Root, Slow, Stun)
- Ability Replacements
- Ability Unlocks

✅ **Fallback to manual description** if no effects are defined  
✅ **Integrated** with TraitRollerUI and TraitTreeUI tooltips

---

## How It Works

### Example: Tier Scaling in Action

**Trait Setup:**
```
Trait Name: "Critical Strike"
Stat Modifier: CritChance = +5 (Flat)
TierConfig: Tier I = 1.0x, Tier II = 1.5x, Tier III = 2.5x
```

**Generated Descriptions:**
- **Tier I**: "+5% Crit Chance"
- **Tier II**: "+7.5% Crit Chance" (5 * 1.5)
- **Tier III**: "+12.5% Crit Chance" (5 * 2.5)

---

## Setting Up Traits for Dynamic Descriptions

### 1. Create Stat-Based Traits
For traits that modify stats, just add stat modifiers. The description is auto-generated:

**Inspector Setup:**
```
Display Name: "Iron Skin"
Stat Modifiers:
  - statID: "Armor"
    modifierType: Flat
    value: 10
Tier Config: [reference your TierScalingConfig]
Tier Level: I (or roll to higher tier)
```

**Generated Description:**
- Tier I: "+10 Armor"
- Tier III: "+25 Armor"

---

### 2. Create Ability-Based Traits
For traits that modify abilities:

**Inspector Setup:**
```
Display Name: "Multishot"
Ability Modifiers:
  - abilityName: "Primary Attack"
    modificationType: ProjectileCount
    value: 2
    description: (optional - will auto-generate if blank)
```

**Generated Description:**
- "Primary Attack fires +2 additional projectile(s)"

---

### 3. Tag-Based Modifications
Affect all abilities with specific tags:

**Inspector Setup:**
```
Display Name: "Projectile Mastery"
Tag Modifiers:
  - tagName: "Projectile"
    modificationType: Damage
    value: 15
```

**Generated Description:**
- "+15% damage for Projectile abilities"

---

### 4. Status Effect Traits
Add status effects to abilities:

**Inspector Setup:**
```
Display Name: "Bleeding Edge"
Status Effect Modifiers:
  - abilityName: "Primary Attack"
    addBleed: true
    bleedDamage: 5
    bleedDuration: 3
    bleedChance: 0.25
```

**Generated Description:**
- "Primary Attack inflict: Bleed (5 damage over 3s) (25% chance)"

---

## Manual Descriptions (Fallback)

If your trait has **complex custom logic** that can't be expressed through standard effects, you can still use manual descriptions:

**When to use manual descriptions:**
- Custom TraitEffect scripts with unique behavior
- Traits that don't fit standard modifier patterns
- Narrative/lore descriptions

The system will use your manual `description` field if no stat/ability effects are detected.

---

## Technical Details

### Files Modified
1. **TraitDescriptionBuilder.cs** (NEW)
   - `BuildDynamicDescription(TraitData)` - Main entry point
   - Formatting helpers for all effect types
   - Tier scaling integration via `TraitData.GetScaledValue()`

2. **TraitOptionUI.cs** (UPDATED)
   - Now calls `TraitDescriptionBuilder.BuildDynamicDescription()` instead of using `trait.description`
   - Line ~78-83

3. **TraitTreeUI.cs** (UPDATED)
   - `BuildTooltipDescription()` now uses dynamic builder
   - Line ~463

### How Tier Scaling Works
```csharp
// In TraitData.cs:
public float GetScaledValue(float baseValue)
{
    if (traitType == TraitType.Ability || tierConfig == null)
        return baseValue; // No scaling for Ability traits
    
    return baseValue * tierConfig.GetMultiplier(tierLevel);
}
```

**TraitDescriptionBuilder** automatically calls `GetScaledValue()` for all stat modifiers, ensuring displayed values match the actual tier-scaled numbers.

### Percentage vs Flat Stat Formatting
The system automatically determines formatting based on `StatTypeDatabase`:

**Percentage Stats** (Attack Speed, Crit Chance, etc.):
- `isPercentage = true` in StatTypeDatabase
- Flat modifier: "+15% Attack Speed" (adds percentage points)
- Percentage modifier: "+20% Attack Speed" (multiplicative)

**Absolute Stats** (Health, Armor, etc.):
- `isPercentage = false` in StatTypeDatabase
- Flat modifier: "+50 Health" (adds absolute value)
- Percentage modifier: "+20% Health" (multiplicative)

---

## Testing Your Traits

### 1. Test in Trait Roller UI
- Start the game and level up
- Check that rolled traits show scaled values based on their tier
- Example: A Tier III trait should show 2.5x the base values (default scaling)

### 2. Test in Trait Tree UI
- Open the trait tree
- Hover over nodes
- Verify tooltip shows proper formatting

### 3. Check Formatting
✅ Positive values should show "+" prefix  
✅ Percentage stats should show "%" suffix  
✅ Multi-line descriptions should be readable  
✅ Status effects should show duration and chance  

---

## Customization

### Custom Ability Modifier Descriptions
If auto-generated text isn't perfect, provide a custom description:

```
Ability Modifiers:
  - abilityName: "Fireball"
    modificationType: Damage
    value: 25
    description: "Fireball becomes SUPERCHARGED! +25% damage!"
```

The custom description will be used instead of the auto-generated one.

### Custom Tag Modifier Descriptions
Same principle:

```
Tag Modifiers:
  - tagName: "Fire"
    modificationType: Damage
    value: 30
    description: "Your flames burn hotter! Fire abilities deal 30% more damage."
```

---

## Best Practices

### ✅ DO:
- Use stat modifiers for simple number changes
- Let the system auto-generate descriptions when possible
- Use TierScalingConfig for stat traits
- Set `tierLevel = ItemTier.I` in the trait asset (it will be rolled at runtime)

### ❌ DON'T:
- Write manual descriptions for simple stat changes
- Use tier scaling for Ability-type traits (they're build-defining and shouldn't scale)
- Forget to assign a TierScalingConfig to stat traits
- Manually update descriptions when changing stat values

---

## Troubleshooting

**Problem:** Description shows "MaxHealth" instead of "Max Health"  
**Solution:** Make sure the stat exists in your StatTypeDatabase with a proper `displayName`

**Problem:** Tier scaling not working  
**Solution:** 
1. Check that `tierConfig` is assigned in the trait
2. Verify `traitType` is set to `Stat` (not `Ability`)
3. Confirm the trait's `tierLevel` field matches the rolled tier

**Problem:** Description is blank  
**Solution:** The trait has no effects and no manual description. Add stat modifiers or write a description.

**Problem:** Values look wrong  
**Solution:** Check your TierScalingConfig multipliers. Default is 1.0x, 1.5x, 2.5x, 4.0x, 6.0x, 10.0x

---

## Future Enhancements (Optional)

If you want to extend the system:

### Add Color/Rich Text
Modify `TraitDescriptionBuilder.cs` to add color tags:
```csharp
return $"<color=#00FF00>+{value:0.##}%</color> {statDisplayName}";
```

### Add Icons
Integrate with stat icons:
```csharp
// Could return icon + text pairs for UI builder
public static List<(Sprite icon, string text)> BuildDescriptionWithIcons(TraitData trait)
```

### Add Stat Categories
Group effects by category (Offensive, Defensive, etc.):
```csharp
description += "\n<b>Offensive Bonuses:</b>\n";
// ... offensive stats
description += "\n<b>Defensive Bonuses:</b>\n";
// ... defensive stats
```

---

## Summary

The **TraitDescriptionBuilder** system automatically generates human-readable trait descriptions with proper tier scaling. Simply define your trait effects through stat/ability modifiers, and the system handles the rest.

**Key Benefit:** When you roll a Tier III trait with "+5% Crit Chance", the description will correctly show "+12.5% Crit Chance" based on the tier multiplier.

This eliminates description maintenance and ensures players always see accurate values!
