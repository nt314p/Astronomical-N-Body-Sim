using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DisplayManager : MonoBehaviour
{
    [SerializeField] private GameObject helpScreen;

    [SerializeField] private GameObject settingsMenu;

    [SerializeField] private GameObject mainCanvas;

    public void ToggleHelp()
    {
        if (!helpScreen.activeInHierarchy)
        {
            mainCanvas.SetActive(true); // ensure that UI is showing for help menu
            helpScreen.SetActive(true);
            return;
        }
        helpScreen.SetActive(!helpScreen.activeInHierarchy);
    }

    public void ToggleSettingsMenu()
    {
        settingsMenu.SetActive(!settingsMenu.activeInHierarchy);
    }

    public void ToggleUI()
    {
        mainCanvas.SetActive(!mainCanvas.activeInHierarchy);
    }
}
