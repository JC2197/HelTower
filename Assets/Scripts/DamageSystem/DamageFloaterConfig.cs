using UnityEngine;
using TMPro;

[CreateAssetMenu(fileName = "DamageFloaterConfig", menuName = "Damage/Damage Floater Config")]
public class DamageFloaterConfig : ScriptableObject
{
    [Header("Display Settings")]
    public float lifetime = 1f;
    
    [Header("Movement (in Screen Units)")]
    public float floatSpeed = 50f;
    public Vector2 randomOffset = new Vector2(30f, 20f);
    public AnimationCurve movementCurve = AnimationCurve.EaseInOut(0, 1, 1, 0);
    
    [Header("Scale Animation")]
    public float startScale = 0.5f;
    public float endScale = 1.25f;
    public float scaleAnimationDuration = 0.25f; // As fraction of lifetime
    
    [Header("Position")]
    public Vector3 worldOffset = new Vector3(0f, 0.5f, 0f);
    public float directionalOffset = 0.5f;
    
    [Header("Font Settings")]
    public TMP_FontAsset customFont;
    public Material fontMaterial;
    public float fontSize = 24f;
    public float criticalFontSizeMultiplier = 1.3f;
    
    [Header("Outline Settings")]
    public bool enableOutline = true;
    public Color outlineColor = Color.black;
    [Range(0f, 1f)] public float outlineThickness = 0.2f;
    
    [Header("Colors")]
    public Color physicalDamageColor = Color.white;
    public Color criticalDamageColor = Color.yellow;

    public Color magicalDamageColor = new Color(0.5f, 0.5f, 1f);
    public Color magicalCriticalDamageColor = Color.yellow;
    public Color healingColor = Color.green;
    public Color energySpentColor = new Color(0.3f, 0.6f, 1f);


    public Color fireColor = Color.red;
    public Color fireCriticalDamageColor = Color.yellow;
    public Color iceColor = Color.cyan;
    public Color iceCriticalDamageColor = Color.yellow;
    public Color lightningColor = Color.yellow;
    public Color lightningCriticalDamageColor = Color.yellow;
    public Color poisonColor = Color.green;
    public Color bleedingColor = Color.green;
    public Color burningColor = Color.red;
    
    public Color darkColor = Color.magenta;
    public Color darkCriticalDamageColor = Color.yellow;
    public Color lightColor = Color.white;
    public Color lightCriticalDamageColor = Color.yellow;
    public Color natureColor = Color.green;
    public Color natureCriticalDamageColor = Color.yellow;


}
