using UnityEngine;
using System.Collections;
using Cysharp.Threading.Tasks;
public class GameInitiator : MonoBehaviour
{
    private async UniTask Start()
    {
        await InitializeGame();
    }

    private async UniTask InitializeGame()
    {
        
    }
}