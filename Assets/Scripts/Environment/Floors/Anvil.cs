using UnityEngine;
using System.Collections;
using FishNet;
using FishNet.Object;
using FishNet.Connection;


[RequireComponent(typeof(Animator))]
public class Anvil : Interactable
{

    private FloorManager _floorManager;
    private Animator _animator;
    private SpriteRenderer _spriteRenderer;
    private Collider2D _collider;
    private bool traitTreeOpened = false;
    public string startAnimationName = "start";
     [Header("Interaction")]
    [SerializeField] private bool startEnabled = false;
    [Tooltip("If true, teleporter is interactable from the start (for CommandScene). If false, requires floorClearWatcher to enable it.")]

    
    protected override void Awake()
    {
        base.Awake();
        _floorManager = FloorManager.Instance;
        controlledByFloorClear = true;
        _animator = GetComponent<Animator>();
        _spriteRenderer = GetComponent<SpriteRenderer>();
        _collider = GetComponent<Collider2D>();
        SetInteractable(startEnabled);
        SetVisible(startEnabled);
        traitTreeOpened = false;
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        _floorManager = FloorManager.Instance;
        SetInteractable(startEnabled);
        SetVisible(startEnabled);
    }

    public override void OnInteract(GameObject player)
    {
        if (!CanInteract()) return;
        if (TraitTreeSceneManager.Instance != null && !traitTreeOpened)
        {
            TraitTreeSceneManager.Instance.OpenTraitTree();
            traitTreeOpened = true;
        } else if (traitTreeOpened)
        {
            TraitTreeSceneManager.Instance.CloseTraitTree();
            traitTreeOpened = false;
        }
    }

    [ObserversRpc(BufferLast = true)]
    public void EnableAnvilObserversRpc() 
    {
        SetInteractable(true);
        SetVisible(true);
    }

    public void Enable()
    {
        SetInteractable(true);
        SetVisible(true);
    }

    private void SetVisible(bool visible)
    {
        _spriteRenderer.enabled = visible;
        _collider.enabled = visible;
        if (visible)
        {
            _animator.Play(startAnimationName);
        }
        
    }
}