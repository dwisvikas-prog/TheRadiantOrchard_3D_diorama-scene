using RadiantOrchard.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RadiantOrchard.App
{
    /// <summary>
    /// The one wiring object in the Game scene. Passes the services from Boot to each system's Init method
    /// (systems are added here as they are built). Never searches for objects at runtime.
    /// </summary>
    public sealed class GameInstaller : MonoBehaviour
    {
        [SerializeField] private string _bootSceneName = "Boot";

        private void Awake()
        {
            AppServices services = AppServices.Current;
            if (services == null)
            {
                // Play was pressed with Game open: boot first, so every session starts the same way.
                SceneManager.LoadScene(_bootSceneName);
                return;
            }

            Log.Info($"GameInstaller: ready; {services.State.Seeds} seeds, {services.State.HealedTotal} healed, away {services.LaunchTimeAway}.");
        }
    }
}
