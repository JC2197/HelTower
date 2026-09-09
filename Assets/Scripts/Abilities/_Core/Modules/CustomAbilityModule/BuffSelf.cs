using System.Collections;
using UnityEngine;

[CreateAssetMenu(fileName = "BuffSelf", menuName = "Abilities/Custom/Buff Self")]
public class BuffSelf : CustomAbility
{
    [Header("Buff Settings")]
    [Min(0f)] public float buffDuration = 5f;
    public EffectConfig buffEffect;

    [Header("Stacking")]
    [Min(1)] public int maxStacks = 1;

    [Header("Next Attack")]
    public bool nextAttack;
    public AbilityDataConfig abilityToCast;
    public bool replaceAttack;
    public AbilityDataConfig replacementAbilityToCast;
    [Min(0.1f)] public float triggeredAbilityLifetime = 5f;

    [System.NonSerialized] protected int currentStacks;
    [System.NonSerialized] protected Coroutine stackExpiryCoroutine;
    [System.NonSerialized] protected DataDrivenAbility runtimeSourceAbility;
    [System.NonSerialized] private PlayerController subscribedPlayer;
    [System.NonSerialized] private bool isCastingNextAttack;
    public int CurrentStacks => currentStacks;

    public override void OnAbilityUse(CustomAbilityContext context)
    {
        AddStack(context);
        ApplyBuff(context);
        BindNextAttack(context);
    }

    protected virtual void AddStack(CustomAbilityContext context)
    {
        currentStacks = Mathf.Clamp(currentStacks + 1, 0, Mathf.Max(1, maxStacks));
        runtimeSourceAbility = context.sourceAbility;

        if (runtimeSourceAbility == null || buffDuration <= 0f) return;

        if (stackExpiryCoroutine != null)
            runtimeSourceAbility.StopCoroutine(stackExpiryCoroutine);

        stackExpiryCoroutine = runtimeSourceAbility.StartCoroutine(ExpireStacksAfterDuration());
    }

    protected virtual IEnumerator ExpireStacksAfterDuration()
    {
        yield return new WaitForSeconds(buffDuration);
        ClearStacks();
    }

    protected virtual void ClearStacks()
    {
        currentStacks = 0;
        stackExpiryCoroutine = null;
        UnbindNextAttack();
    }

    private void BindNextAttack(CustomAbilityContext context)
    {
        if ((!nextAttack || abilityToCast == null) && (!replaceAttack || replacementAbilityToCast == null)) return;

        PlayerController player = context.owner != null ? context.owner.GetComponent<PlayerController>() : null;
        if (player == null || context.sourceAbility == null) return;

        if (subscribedPlayer == player) return;

        UnbindNextAttack();
        subscribedPlayer = player;
        if (nextAttack && abilityToCast != null)
            subscribedPlayer.OnAttack += HandleNextAttack;
        if (replaceAttack && replacementAbilityToCast != null)
            subscribedPlayer.OnBeforeAttackAbilityUse += HandleReplacementAttack;
        Debug.Log($"[BuffSelf] Armed attack buff: stacks={currentStacks}, next={abilityToCast?.name ?? "none"}, replacement={replacementAbilityToCast?.name ?? "none"}");
    }

    private void UnbindNextAttack()
    {
        if (subscribedPlayer != null)
        {
            subscribedPlayer.OnAttack -= HandleNextAttack;
            subscribedPlayer.OnBeforeAttackAbilityUse -= HandleReplacementAttack;
        }

        subscribedPlayer = null;
    }

    private bool HandleReplacementAttack(AbilityDataConfig attackConfig)
    {
        Debug.Log($"[BuffSelf] Checking attack replacement: attack={attackConfig?.name ?? "null"}, stacks={currentStacks}, replacement={replacementAbilityToCast?.name ?? "null"}");

        if (!replaceAttack || replacementAbilityToCast == null || subscribedPlayer == null || currentStacks <= 0 || isCastingNextAttack)
            return false;

        if (attackConfig == replacementAbilityToCast)
            return false;

        bool cast = CastConfiguredAbility(replacementAbilityToCast);
        if (!cast)
            return false;

        currentStacks--;
        if (currentStacks <= 0)
            ClearStacks();

        return true;
    }

    private void HandleNextAttack(AbilityDataConfig attackConfig)
    {
        Debug.Log($"[BuffSelf] Observed attack: attack={attackConfig?.name ?? "null"}, stacks={currentStacks}, abilityToCast={abilityToCast?.name ?? "null"}");

        if (!nextAttack || abilityToCast == null || subscribedPlayer == null || currentStacks <= 0 || isCastingNextAttack)
            return;

        if (attackConfig == abilityToCast)
            return;

        currentStacks--;
        bool cast = CastConfiguredAbility(abilityToCast);
        Debug.Log($"[BuffSelf] Next attack cast '{abilityToCast.name}' result={cast}");

        if (currentStacks <= 0)
            ClearStacks();
    }

    private bool CastConfiguredAbility(AbilityDataConfig abilityConfig)
    {
        if (subscribedPlayer == null || abilityConfig == null)
            return false;

        isCastingNextAttack = true;
        DataDrivenAbility nextAbility = subscribedPlayer.gameObject.AddComponent<DataDrivenAbility>();
        nextAbility.SetAbilityReference(new AbilityReference(abilityConfig));
        nextAbility.InitializeAbility();
        nextAbility.RebuildConfigModifiers();
        bool cast = nextAbility.TryUseAbilityManually();
        Destroy(nextAbility, triggeredAbilityLifetime);
        isCastingNextAttack = false;
        return cast;
    }

    protected virtual void ApplyBuff(CustomAbilityContext context)
    {
        if (context.owner == null || buffEffect == null) return;

        EffectManager effectManager = context.owner.GetComponent<EffectManager>();
        if (effectManager == null)
            effectManager = context.owner.GetComponentInChildren<EffectManager>();

        if (effectManager == null)
        {
            Debug.LogWarning($"[BuffSelf] '{name}' cannot apply {buffEffect.effectName}: {context.owner.name} has no EffectManager component.");
            return;
        }

        EffectConfig runtimeEffect = Object.Instantiate(buffEffect);
        runtimeEffect.duration = buffDuration;
        effectManager.ApplyEffect(runtimeEffect, context.owner);
    }

    public override void OnRuntimeEnd()
    {
        if (nextAttack && currentStacks > 0)
            Debug.Log($"[BuffSelf] Runtime ended while next attack was armed: stacks={currentStacks}, abilityToCast={abilityToCast?.name ?? "null"}");

        if (runtimeSourceAbility != null && stackExpiryCoroutine != null)
            runtimeSourceAbility.StopCoroutine(stackExpiryCoroutine);

        ClearStacks();
        runtimeSourceAbility = null;
    }
}