using System;
using TMPro;
using UnityEngine;

public class Prompt : MonoBehaviour
{
    private const int PromptWidth = 450;
    private const int PromptWithInputHeight = 288;
    private const int PromptWithNoInputHeight = 220;
    
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
    
    private Action promptConfirm;
    private Action<string> promptConfirmWithInput;
    private Action promptReject;
    
    private bool isInputPrompt;
    private bool previousCursorLockState;
    [SerializeField] private TMP_InputField promptInput;
    [SerializeField] private GameObject promptGameObject;
    [SerializeField] private GameObject promptInputGameObject;
    [SerializeField] private RectTransform promptRectTransform;
    [SerializeField] private TextMeshProUGUI promptText;
    [SerializeField] private TextMeshProUGUI leftButtonText;
    [SerializeField] private TextMeshProUGUI rightButtonText;

    public void ShowPrompt(string prompt, string confirmButtonText, string rejectButtonText, Action onConfirm, Action onReject = null)
    {
        if (IsPrompting) return;
        
        UnlockCursor();

        IsPrompting = true;
        isInputPrompt = false;
        promptConfirm = onConfirm;
        promptReject = onReject;
        ConfigurePrompt(prompt, confirmButtonText, rejectButtonText);
    }

    public void ShowPromptWithInput(string prompt, string confirmButtonText, string rejectButtonText, Action<string> onConfirm,
        Action onReject = null)
    {
        if (IsPrompting) return;
        
        UnlockCursor();

        IsPrompting = true;
        isInputPrompt = true;
        promptConfirmWithInput = onConfirm;
        promptReject = onReject;
        ConfigurePrompt(prompt, confirmButtonText, rejectButtonText);
    }

    private void UnlockCursor()
    {
        previousCursorLockState = Cursor.lockState == CursorLockMode.Locked; // true if cursor was locked
        Cursor.lockState = CursorLockMode.None;
    }
    
    private void ConfigurePrompt(string prompt, string confirmButtonText, string rejectButtonText)
    {
        promptText.text = prompt;
        promptInput.text = "";
        leftButtonText.text = confirmButtonText;
        rightButtonText.text = rejectButtonText;
        
        promptInputGameObject.SetActive(isInputPrompt);
        promptRectTransform.sizeDelta = new Vector2(PromptWidth, isInputPrompt ? PromptWithInputHeight : PromptWithNoInputHeight);
    }

    public void OnClickLeftButton()
    {
        OnPromptButtonClick();

        if (isInputPrompt)
        {
            promptConfirmWithInput.Invoke(promptInput.text);
            return;
        }

        promptConfirm.Invoke();
    }
    
    public void OnClickRightButton()
    {
        OnPromptButtonClick();
        promptReject.Invoke();
    }

    private void OnPromptButtonClick()
    {
        IsPrompting = false;
        Cursor.lockState = previousCursorLockState ? CursorLockMode.Locked : CursorLockMode.None;
    }
}
