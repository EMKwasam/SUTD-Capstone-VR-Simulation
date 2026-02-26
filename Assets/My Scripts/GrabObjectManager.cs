using UnityEngine;

public class GrabObjectManager : MonoBehaviour
{
    [SerializeField] private GameObject disableWhenGrabbed;
    [SerializeField] private GameObject enableWhenGrabbed;
    [SerializeField] private bool useCustomReleaseState;
    [SerializeField] private GameObject disableWhenReleased;
    [SerializeField] private GameObject enableWhenReleased;
    [SerializeField] private bool enableDebugLogs = true;

    public void OnObjectGrabbed()
    {
        SetActiveSafe(disableWhenGrabbed, false);
        SetActiveSafe(enableWhenGrabbed, true);

        if (enableDebugLogs)
        {
            Debug.Log($"[GrabObjectManager] Grab state applied. Disabled: {GetName(disableWhenGrabbed)} | Enabled: {GetName(enableWhenGrabbed)}", this);
        }
    }

    public void OnObjectReleased()
    {
        if (useCustomReleaseState)
        {
            SetActiveSafe(disableWhenReleased, false);
            SetActiveSafe(enableWhenReleased, true);

            if (enableDebugLogs)
            {
                Debug.Log($"[GrabObjectManager] Custom release state applied. Disabled: {GetName(disableWhenReleased)} | Enabled: {GetName(enableWhenReleased)}", this);
            }
            return;
        }

        SetActiveSafe(enableWhenGrabbed, false);
        SetActiveSafe(disableWhenGrabbed, true);

        if (enableDebugLogs)
        {
            Debug.Log($"[GrabObjectManager] Default release state applied. Disabled: {GetName(enableWhenGrabbed)} | Enabled: {GetName(disableWhenGrabbed)}", this);
        }
    }

    private static void SetActiveSafe(GameObject target, bool active)
    {
        if (target == null)
        {
            return;
        }

        target.SetActive(active);
    }

    private static string GetName(GameObject target)
    {
        return target != null ? target.name : "<none>";
    }
}