# Dynamic Trait Description - Example

## Scenario: Rolling a "Vitality Boost" Trait

### Trait Configuration (in Inspector)
```
─────────────────────────────────────────
TraitData Asset: "Vitality Boost"
─────────────────────────────────────────
Display Name: Vitality Boost
Description: (leave blank - will auto-generate)
Trait Type: Stat
Tier Config: DefaultTierScaling
Tier Level: I (base)

Stat Modifiers:
  [0]
    statID: MaxHealth
    modifierType: Flat
    value: 50
─────────────────────────────────────────
```

### TierScalingConfig (DefaultTierScaling asset)
```
Tier I:   1.0x
Tier II:  1.5x
Tier III: 2.5x
Tier IV:  4.0x
Tier V:   6.0x
Tier VI:  10.0x
```

---

## Results

### When Rolled as Tier I
**Calculation:** 50 * 1.0 = 50  
**Generated Description:** 
```
+50 Max Health
```

### When Rolled as Tier II
**Calculation:** 50 * 1.5 = 75  
**Generated Description:** 
```
+75 Max Health
```

### When Rolled as Tier III
**Calculation:** 50 * 2.5 = 125  
**Generated Description:** 
```
+125 Max Health
```

### When Rolled as Tier VI
**Calculation:** 50 * 10.0 = 500  
**Generated Description:** 
```
+500 Max Health
```

---

## Multi-Effect Example

### Trait Configuration
```
─────────────────────────────────────────
TraitData Asset: "Critical Expert"
─────────────────────────────────────────
Display Name: Critical Expert
Trait Type: Stat
Tier Config: DefaultTierScaling
Tier Level: III

Stat Modifiers:
  [0]
    statID: CritChance
    modifierType: Flat
    value: 5
  [1]
    statID: CritMultiplier
    modifierType: Flat
    value: 25
─────────────────────────────────────────
```

### Generated Description (Tier III)
```
+12.5% Crit Chance
+62.5% Crit Multiplier
```

**Math:**
- CritChance: 5 * 2.5 = 12.5
- CritMultiplier: 25 * 2.5 = 62.5

---

## Ability Trait Example

### Trait Configuration
```
─────────────────────────────────────────
TraitData Asset: "Multishot"
─────────────────────────────────────────
Display Name: Multishot
Trait Type: Ability

Ability Modifiers:
  [0]
    abilityName: Bow Attack
    modificationType: ProjectileCount
    value: 2
    description: (leave blank)
─────────────────────────────────────────
```

### Generated Description
```
Bow Attack fires +2 additional projectile(s)
```

**Note:** Ability traits don't use tier scaling (they're build-defining mechanics).

---

## Tag-Based Trait Example

### Trait Configuration
```
─────────────────────────────────────────
TraitData Asset: "Projectile Mastery"
─────────────────────────────────────────
Display Name: Projectile Mastery
Trait Type: Stat
Tier Config: DefaultTierScaling
Tier Level: II

Tag Modifiers:
  [0]
    tagName: Projectile
    modificationType: Damage
    value: 15
─────────────────────────────────────────
```

### Generated Description (Tier II)
```
+22.5% damage for Projectile abilities
```

**Math:** 15 * 1.5 = 22.5

---

## Complex Multi-Effect Example

### Trait Configuration
```
─────────────────────────────────────────
TraitData Asset: "Bleeding Strikes"
─────────────────────────────────────────
Display Name: Bleeding Strikes
Trait Type: Stat
Tier Config: DefaultTierScaling
Tier Level: IV

Stat Modifiers:
  [0]
    statID: PhysicalDamage
    modifierType: Percentage
    value: 10

Status Effect Modifiers:
  [0]
    abilityName: Primary Attack
    addBleed: true
    bleedDamage: 5
    bleedDuration: 3
    bleedChance: 0.3
─────────────────────────────────────────
```

### Generated Description (Tier IV)
```
+40% Physical Damage
Primary Attack inflict: Bleed (5 damage over 3s) (30% chance)
```

**Math:** 10 * 4.0 = 40% Physical Damage

**Note:** Status effect values (bleed damage, duration) are NOT scaled by tier in this implementation. Only stat modifiers are scaled. This is intentional - status effects should remain consistent.

---

## Before & After Comparison

### OLD SYSTEM (Manual Descriptions)
❌ You had to write: "Increases Max Health by 50"  
❌ If rolled as Tier III, description still says 50 (WRONG!)  
❌ If you change the value from 50 to 60, description is outdated  
❌ For multi-stat traits, you write long descriptions manually  

### NEW SYSTEM (Dynamic Descriptions)
✅ Leave description blank  
✅ Tier III automatically shows 125 (CORRECT!)  
✅ Change value to 60? Description updates automatically  
✅ Multi-stat traits? All formatted perfectly  

---

## Special Cases

### Percentage Stats vs Absolute Stats

**Absolute Stat (Health):**
```
+100 Max Health       (no % sign)
```

**Percentage Stat (Attack Speed):**
```
+15% Attack Speed     (has % sign)
```

The system detects this automatically via `StatTypeDatabase.isPercentage`.

### Negative Values

```
Stat Modifier:
  statID: MoveSpeed
  modifierType: Percentage
  value: -20
```

**Generated Description:**
```
-20% Move Speed
```

### Decimal Formatting

Small values show decimals when needed:
```
+2.5% Crit Chance     (not +2%)
+125 Max Health       (not +125.00)
```

---

## Summary

The **TraitDescriptionBuilder** system:
1. ✅ Reads all effects from your trait
2. ✅ Applies tier scaling to stat modifiers
3. ✅ Formats values with proper signs (+/-) and suffixes (%)
4. ✅ Handles multiple effects with line breaks
5. ✅ Falls back to manual description if no effects exist

**Result:** Descriptions are always accurate, always up-to-date, and always properly scaled to the rolled tier!
