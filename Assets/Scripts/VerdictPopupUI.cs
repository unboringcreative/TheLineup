using UnityEngine;

public class VerdictPopupUI : MonoBehaviour
{
    public GameObject popupRoot;

    public void OpenVerdictPopup()
    {
        if (popupRoot != null)
            popupRoot.SetActive(true);
    }

    public void CloseVerdictPopup()
    {
        if (popupRoot != null)
            popupRoot.SetActive(false);
    }
}
