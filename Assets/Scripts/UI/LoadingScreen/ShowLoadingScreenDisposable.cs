using UnityEngine;
public class ShowLoadingScreenDisposable : System.IDisposable
{

    private readonly LoadingScreen _loadingScreen;

    public ShowLoadingScreenDisposable(LoadingScreen loadingScreen)
    {
        _loadingScreen = loadingScreen;
        _loadingScreen.Show();
    }

    // public void SetLoadingBarPercent(float percent)
    // {
    //     _loadingScreen.SetBarPercent(percent);
    // }
    public void Dispose()
    {
        // Hide the loading screen here
        _loadingScreen.Hide();
    }
}