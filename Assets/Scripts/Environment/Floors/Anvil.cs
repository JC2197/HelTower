using UnityEngine;
using System.Collections;
using FishNet;
using FishNet.Object;
using FishNet.Connection;
using UnitySceneManager = UnityEngine.SceneManagement.SceneManager;


[RequireComponent(typeof(Animator))]
public class Anvil : Interactable
{

    private FloorManager floorManager;
    private Animator _animator;
    private bool traitTreeOpened = false;
    public string startAnimationName = "start";
     [Header("Interaction")]
    [SerializeField] private bool startEnabled = false;
    [Tooltip("If true, teleporter is interactable from the start (for CommandScene). If false, requires floorClearWatcher to enable it.")]

    
    private void Awake()
    {
        base.Awake();
        floorManager = FloorManager.Instance;
        controlledByFloorClear = true;
        _animator = GetComponent<Animator>();
        SetInteractable(startEnabled);
        SetVisible(startEnabled);
        traitTreeOpened = false;
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

    public void Enable()
    {
        SetInteractable(true);
        SetVisible(true);
    }

    private void SetVisible(bool visible)
    {
        gameObject.GetComponent<SpriteRenderer>().enabled = visible;
        gameObject.GetComponent<Collider2D>().enabled = visible;
        if (visible)
        {
            _animator.Play(startAnimationName);
        }
        
    }
}