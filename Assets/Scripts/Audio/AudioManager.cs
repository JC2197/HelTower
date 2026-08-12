using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }
    [SerializeField] private AudioMixerGroup sfxMixerGroup;
    [SerializeField] private AudioMixerGroup musicMixerGroup;
    [SerializeField] private int poolSize = 10;
    private readonly List<AudioSource> _pool = new List<AudioSource>();
    private readonly Dictionary<AudioSource, Transform> _followTargets = new Dictionary<AudioSource, Transform>();
    private AudioSource _musicSource;

    private void Awake()
    {
        if (Instance == null) { Instance = this; DontDestroyOnLoad(gameObject); }
        else { Destroy(gameObject); return; }

        InitializePool();
        InitializeMusicSource();
        SceneManager.activeSceneChanged += OnActiveSceneChanged;
        SceneManager.sceneUnloaded += OnSceneUnloaded;
    }

    private void OnDestroy()
    {
        if (Instance != this)
            return;

        SceneManager.activeSceneChanged -= OnActiveSceneChanged;
        SceneManager.sceneUnloaded -= OnSceneUnloaded;
    }

    private void InitializePool()
    {
        for (int i = 0; i < poolSize; i++)
            CreateAudioSource();
    }

    private void InitializeMusicSource()
    {
        GameObject musicSourceObject = new GameObject("MusicSource");
        musicSourceObject.transform.SetParent(transform);
        _musicSource = musicSourceObject.AddComponent<AudioSource>();
        _musicSource.playOnAwake = false;
        _musicSource.spatialBlend = 0f;
        _musicSource.loop = true;
        _musicSource.outputAudioMixerGroup = musicMixerGroup;
    }

    private void LateUpdate()
    {
        foreach (var entry in _followTargets)
        {
            if (entry.Key != null && entry.Value != null)
                entry.Key.transform.position = entry.Value.position;
        }
    }

    private AudioSource CreateAudioSource()
    {
        GameObject audioSourceObject = new GameObject($"AudioSource_{_pool.Count}");
        audioSourceObject.transform.SetParent(transform);
        AudioSource source = audioSourceObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.outputAudioMixerGroup = sfxMixerGroup;
        _pool.Add(source);
        return source;
    }

    private AudioSource GetAvailableAudioSource()
    {
        foreach (var source in _pool)
        {
            if (!source.isPlaying)
            {
                ResetSource(source);
                return source;
            }
        }
        return CreateAudioSource();
    }

    public void PlaySpatialSound(SoundEvent soundEvent, Vector3 position)
    {
        if (soundEvent == null || !soundEvent.CanPlay()) return;

        AudioSource source = GetAvailableAudioSource();
        source.transform.position = position;
        source.spatialBlend = 1f;
        Play(source, soundEvent.clip, soundEvent.volume, Random.Range(soundEvent.minPitch, soundEvent.maxPitch));
    }

    public void Play2DSound(SoundEvent soundEvent)
    {
        if (soundEvent == null || !soundEvent.CanPlay()) return;

        AudioSource source = GetAvailableAudioSource();
        source.spatialBlend = 0f;
        Play(source, soundEvent.clip, soundEvent.volume, Random.Range(soundEvent.minPitch, soundEvent.maxPitch));
    }

    public void PlayMusic(AudioClip clip, float volume = 1f, bool loop = true)
    {
        if (_musicSource == null)
            return;

        if (clip == null)
        {
            StopMusic();
            return;
        }

        if (_musicSource.isPlaying && _musicSource.clip == clip)
        {
            _musicSource.volume = volume;
            _musicSource.loop = loop;
            return;
        }

        _musicSource.Stop();
        _musicSource.clip = clip;
        _musicSource.volume = volume;
        _musicSource.pitch = 1f;
        _musicSource.loop = loop;
        _musicSource.outputAudioMixerGroup = musicMixerGroup;
        _musicSource.Play();
    }

    public void StopMusic()
    {
        if (_musicSource == null)
            return;

        _musicSource.Stop();
        _musicSource.clip = null;
        _musicSource.volume = 1f;
        _musicSource.pitch = 1f;
        _musicSource.loop = true;
        _musicSource.outputAudioMixerGroup = musicMixerGroup;
    }

    public void PlaySpatialSound(AudioClip clip, Vector3 position, float volume = 1f, float pitch = 1f)
    {
        if (clip == null) return;

        AudioSource source = GetAvailableAudioSource();
        source.transform.position = position;
        source.spatialBlend = 1f;
        Play(source, clip, volume, pitch);
    }

    public void Play2DSound(AudioClip clip, float volume = 1f, float pitch = 1f)
    {
        if (clip == null) return;

        AudioSource source = GetAvailableAudioSource();
        source.spatialBlend = 0f;
        Play(source, clip, volume, pitch);
    }

    public AudioSource PlayLoopingSpatialSound(AudioClip clip, Transform followTarget, float volume = 1f, float pitch = 1f)
    {
        if (clip == null) return null;

        AudioSource source = GetAvailableAudioSource();
        source.transform.position = followTarget != null ? followTarget.position : transform.position;
        source.spatialBlend = 1f;
        source.loop = true;
        _followTargets[source] = followTarget;
        Play(source, clip, volume, pitch);
        return source;
    }

    public void StopSound(AudioSource source)
    {
        if (source == null) return;

        source.Stop();
        ResetSource(source);
    }

    private static void Play(AudioSource source, AudioClip clip, float volume, float pitch)
    {
        source.clip = clip;
        source.volume = volume;
        source.pitch = Mathf.Clamp(pitch, -3f, 3f);
        source.Play();
    }

    private void ResetSource(AudioSource source)
    {
        _followTargets.Remove(source);
        source.clip = null;
        source.loop = false;
        source.volume = 1f;
        source.pitch = 1f;
        source.spatialBlend = 0f;
        source.transform.localPosition = Vector3.zero;
        source.outputAudioMixerGroup = sfxMixerGroup;
    }

    private void OnActiveSceneChanged(Scene previousScene, Scene nextScene)
    {
        if (previousScene.name == "GameScene" && nextScene.name != "GameScene")
            StopMusic();
    }

    private void OnSceneUnloaded(Scene scene)
    {
        if (scene.name == "GameScene")
            StopMusic();
    }
}