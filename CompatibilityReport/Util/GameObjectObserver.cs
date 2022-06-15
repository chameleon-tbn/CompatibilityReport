using System;
using UnityEngine;

namespace CompatibilityReport.Util
{
    /// <summary>
    /// Simple component which should be if we are not managing gameObject lifecycle
    /// OnDestroy will be automatically called upon gameObject destroy, will trigger event
    /// Event is perfect for performing clean up of events or other memory leak prone code
    /// </summary>
    public class GameObjectObserver : MonoBehaviour
    {
        public event Action eventGameObjectDestroyed;
        private void OnDestroy()
        {
            try
            {
                eventGameObjectDestroyed?.Invoke();
                eventGameObjectDestroyed = null;
            }
            catch (Exception e)
            {
                Logger.Exception(e);
            }
        }
    }
}
