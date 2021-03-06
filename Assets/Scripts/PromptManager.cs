using System;
using TMPro;
using UnityEngine;

public class PromptManager : MonoBehaviour
{
    private bool isPrompting;

    public bool IsPrompting
    {
        get => isPrompting;
        private set
        {
            isPrompting = value;
            promptGameObject.SetActive(value);
        }
    }
    
    private const int PromptWidth = 450;
    private const int PromptWithInputHeight = 288;
    private const int PromptWithNoInputHeight = 220;
    private Action<bool, string> promptResult;
    private bool isInputPrompt;
    private bool previousCursorLockState;
    [SerializeField] private TMP_InputField promptInput;
    [SerializeField] private GameObject promptGameObject;
    [SerializeField] private GameObject promptInputGameObject;
    [SerializeField] private RectTransform promptRectTransform;
    [SerializeField] private TextMeshProUGUI promptText;
    [SerializeField] private TextMeshProUGUI leftButtonText;
    [SerializeField] private TextMeshProUGUI rightButtonText;

    public void ShowPrompt(string prompt, string leftButtonMessage, string rightButtonMessage, Action<bool, string> resultCallback, bool useInput=true)
    {
        if (IsPrompting)
        {
            return;
        }

        previousCursorLockState = Cursor.lockState == CursorLockMode.Locked; // true if cursor was locked
        Cursor.lockState = CursorLockMode.None;

        IsPrompting = true;
        promptText.text = prompt;
        promptInput.text = "";
        leftButtonText.text = leftButtonMessage;
        rightButtonText.text = rightButtonMessage;
        promptResult = resultCallback;
        isInputPrompt = useInput;
        ConfigurePrompt();
    }

    private void ConfigurePrompt()
    {
        promptInputGameObject.SetActive(isInputPrompt);
        promptRectTransform.sizeDelta = new Vector2(PromptWidth, isInputPrompt ? PromptWithInputHeight : PromptWithNoInputHeight);
    }

    public void OnClickLeftButton()
    {
        OnPromptButtonClick();

        if (isInputPrompt)
        {
            promptResult.Invoke(false, promptInput.text);
            return;
        }
        promptResult.Invoke(false, null);
    }
    
    public void OnClickRightButton()
    {
        OnPromptButtonClick();
        
        if (isInputPrompt)
        {
            promptResult.Invoke(true, promptInput.text);
            return;
        }
        promptResult.Invoke(true, null);
    }

    private void OnPromptButtonClick()
    {
        IsPrompting = false;
        Cursor.lockState = previousCursorLockState ? CursorLockMode.Locked : CursorLockMode.None;
    }
}
