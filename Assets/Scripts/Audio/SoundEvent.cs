using UnityEngine;

[CreateAssetMenu(fileName = "SoundEvent", menuName = "Audio/Sound Event")]
public class SoundEvent : ScriptableObject
{
    public AudioClip clip;
    [Range(0f, 1f)]
    public float volume = 1f;
    [Range(0.1f, 2f)]
    public float minPitch = 0.95f;
    [Range(0.1f, 2f)]
    public float maxPitch = 1.05f;

    [Header("Concurrency Control")]
    public bool limitConcurrency = false;
    public float minTimeBetweenPlays = 0.1f;
    private float lastPlayedTime;

    public bool CanPlay()
    {
        if (!limitConcurrency)
            return true;

        if (Time.time - lastPlayedTime >= minTimeBetweenPlays)
        {
            lastPlayedTime = Time.time;
            return true;
        }
        return false;
    }
}