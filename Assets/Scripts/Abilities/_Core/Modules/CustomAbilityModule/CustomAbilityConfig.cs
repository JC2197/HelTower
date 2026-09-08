using UnityEngine;

public readonly struct CustomAbilityContext
{
	public readonly GameObject owner;
	public readonly DataDrivenAbility sourceAbility;
	public readonly AbilityDataConfig sourceConfig;

	public CustomAbilityContext(GameObject owner, DataDrivenAbility sourceAbility, AbilityDataConfig sourceConfig)
	{
		this.owner = owner;
		this.sourceAbility = sourceAbility;
		this.sourceConfig = sourceConfig;
	}
}

public abstract class CustomAbility : ScriptableObject
{
	public abstract void OnAbilityUse(CustomAbilityContext context);

	public virtual void OnRuntimeEnd()
	{
	}
}

[System.Obsolete("Use CustomAbility instead.")]
public abstract class CustomAbilityConfig : CustomAbility
{
}