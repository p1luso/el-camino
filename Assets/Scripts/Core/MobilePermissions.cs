using UnityEngine;
using UnityEngine.Android;

namespace ElCamino.Core
{
    public class MobilePermissions : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoStart()
        {
            GameObject go = new GameObject("MobilePermissionsAuto");
            go.AddComponent<MobilePermissions>();
            DontDestroyOnLoad(go);
        }

        void Start()
        {
    #if UNITY_ANDROID
            if (!Permission.HasUserAuthorizedPermission(Permission.FineLocation))
            {
                Permission.RequestUserPermission(Permission.FineLocation);
            }
    #endif
        }
    }
}