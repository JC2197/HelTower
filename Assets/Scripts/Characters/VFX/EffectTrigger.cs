using UnityEngine;

public class EffectTrigger : MonoBehaviour
{
    public ParticleSystem targetParticle;

    public void PlayEffect()
    {
        if (targetParticle != null)
            targetParticle.Play();
    }
}
