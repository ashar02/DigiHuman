using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Runtime.InteropServices;


public enum WaitingModeUI
{
    None, ProgressBar, Loading
}


public class UIManager : MonoSingleton<UIManager>
{
    
    [Header("Requirements")] 
    [SerializeField] private FrameReader frameReader;
    [SerializeField] private Canvas canvas;

    
    [Header("ProgressBars and Loading")] 
    [SerializeField] private Image progressBarImage;
    [SerializeField] private TextMeshProUGUI progressBarText;
    [SerializeField] private GameObject waitingUI; //full background color for progressbar and loading
    [SerializeField] private GameObject loading;
    [SerializeField] private GameObject progressBar;


    // [Header("Slides")] 
    // [SerializeField] private GameObject imageSlideShowPanel;
    // [SerializeField] private GameObject characterSlideShowPanel;

    [Header("Upload Panel")] 
    [SerializeField] private Color successDownloadColor;
    [SerializeField] private Image poseUploadCircleImage;
    [SerializeField] private Image poseUploadCompleteImage;

    [SerializeField] private Image handPoseUploadCircleImage;
    [SerializeField] private Image handPoseUploadCompleteImage;

    [SerializeField] private Image faceUploadCircleImage;
    [SerializeField] private Image faceUploadCompleteImage;


    [Header("Animation Control Panel")] 
    [SerializeField] private Button animationPlayButton;
    [SerializeField] private Sprite pauseImage;
    [SerializeField] private Sprite resumeImage;
    [SerializeField] private Button saveAnimationButton;
    [SerializeField] private Button recordButton;

    
    [Header("Save Animation Panel")] 
    [SerializeField] private GameObject saveAnimationPanel;
    [SerializeField] private TextMeshProUGUI animationNameText;


    [Header("Camera Zoom Panel")] 
    [SerializeField] private GameObject bodyZoomButton;
    [SerializeField] private GameObject faceZoomButton;
    
    
    [Header("Messages")] 
    [SerializeField] private GameObject errorPanel;
    [SerializeField] private GameObject successPanel;

    [DllImport("__Internal")]
    private static extern void sendMessageToWeb(string message);

    public void UpdateProgressBar(float percent)
    {
        progressBarImage.fillAmount = percent;
        progressBarText.text = (percent*100).ToString("0.0") + "%";
    }
    public void CheckAndEnableWaitingModeUI(WaitingModeUI waitingModeUI,bool enable)
    {
        if (enable)
        {
            if (!waitingModeUI.Equals(WaitingModeUI.None))
            {
                waitingUI.SetActive(true);
                if (waitingModeUI.Equals(WaitingModeUI.Loading))
                {
                    loading.SetActive(true);
                    progressBar.SetActive(false);
                }
                else if (waitingModeUI.Equals(WaitingModeUI.ProgressBar))
                {
                    loading.SetActive(false);
                    progressBar.SetActive(true);
                }
            }
        }
        else
        {
            waitingUI.SetActive(false);
        }
    }
    
    //Messages
    public void ShowSuccessMessage(string message)
    {
        successPanel.SetActive(true);
        successPanel.GetComponentInChildren<TextMeshProUGUI>().text = message;
    }
    
    public void ShowErrorMessage(string message)
    {
        errorPanel.SetActive(true);
        errorPanel.GetComponentInChildren<TextMeshProUGUI>().text = message;
    }
    
    
    
    //Animation Actions
    public void OnPlayAnimationButtonClick()
    {
        frameReader.OnTogglePlay();
        if (frameReader.pause)
            animationPlayButton.image.sprite = resumeImage;
        else
            animationPlayButton.image.sprite = pauseImage;
        
    }

    public void ActiveAnimationControlPanel()
    {
        saveAnimationButton.interactable = true;
        animationPlayButton.interactable = true;
        recordButton.interactable = true;
    }
    public void DeActiveAnimationControlPanel()
    {
        saveAnimationButton.interactable = false;
        animationPlayButton.interactable = false;
        recordButton.interactable = false;
    }
    public void ShowAnimationSavePanel()
    {
        saveAnimationPanel.SetActive(true);
        saveAnimationButton.gameObject.SetActive(false);
    }
    public void OnSubmitAnimationSave()
    {

        string animationName = animationNameText.text;
        bool result = FileManager.SaveAnimation(animationName,frameReader.GetFrameData());
        animationNameText.text = "";
        if (result)
        {
            ShowSuccessMessage("Animation Saved Successfully!");
            saveAnimationPanel.SetActive(false);
            saveAnimationButton.gameObject.SetActive(true);
            AnimationChooser.Instancce.AddNewAnimation(animationName);
        }
        else
            ShowErrorMessage("Use another animation name!");
    }
    
    
    //Slides
    public void OnSlideShowEnter(GameObject panel)
    {
        panel.SetActive(true);
        canvas.gameObject.SetActive(false);
        //OnFullBodyZoomClicked();
        OnFaceZoomClicked();
        frameReader.HideCharacter();
    }
    public void OnSlideShowExit(GameObject panel)
    {
        panel.SetActive(false);
        canvas.gameObject.SetActive(true);
        frameReader.ShowCharacter();
    }
    
    
    //Uploading

    public void OnUploadBodyPoseClick()
    {
        poseUploadCircleImage.color = Color.white;
        poseUploadCompleteImage.gameObject.SetActive(false);
        #if UNITY_EDITOR
        string filePath = FileManager.OpenFileVideoExplorer();
        NetworkManager.Instancce.UploadAndEstimatePose(filePath);
        #endif
    }

    public void OnPoseDataReceived()
    {
        ShowSuccessMessage("Pose data downloaded successfully!");
        poseUploadCircleImage.color = successDownloadColor;
        poseUploadCompleteImage.gameObject.SetActive(true);
        frameReader.ArrangeDataFrames();
    }
    public void OnUploadHandPoseClick()
    {
        handPoseUploadCircleImage.color = Color.white;
        handPoseUploadCompleteImage.gameObject.SetActive(false);
        #if UNITY_EDITOR
        string filePath = FileManager.OpenFileVideoExplorer();
        NetworkManager.Instancce.UploadAndEstimateHandPose(filePath);
        
        #endif
    }
    
    public void OnHandPoseDataReceived()
    {
        ShowSuccessMessage("Hands data downloaded successfully!");
        handPoseUploadCircleImage.color = successDownloadColor;
        handPoseUploadCompleteImage.gameObject.SetActive(true);
        frameReader.ArrangeDataFrames();
    }
    
    
    public void OnUploadFullPoseClick()
    {
        handPoseUploadCircleImage.color = Color.white;
        poseUploadCircleImage.color = Color.white;
        handPoseUploadCompleteImage.gameObject.SetActive(false);
        poseUploadCompleteImage.gameObject.SetActive(false);
        #if UNITY_EDITOR
        string filePath = FileManager.OpenFileVideoExplorer();
        NetworkManager.Instancce.UploadAndEstimateFullPose(filePath);
        
        #endif
    }
    
    public void OnFullPoseDataReceived()
    {
#if !UNITY_WEBGL && UNITY_EDITOR
            if (string.IsNullOrEmpty(NetworkManager.Instancce.commandLineText))
            {
                ShowSuccessMessage("Full pose data downloaded successfully!");
            }
#endif
        Debug.Log("WEB GL INPUT CAPTURE DISABLED POSE3DMAPER ____________: ");
#if !UNITY_EDITOR && UNITY_WEBGL
            // disable WebGLInput.captureAllKeyboardInput so elements in web page can handle keyboard inputs
            WebGLInput.captureAllKeyboardInput = false;
            Debug.Log("captureAllKeyboardInput ____________: ");
#endif
        handPoseUploadCircleImage.color = successDownloadColor;
        poseUploadCircleImage.color = successDownloadColor;
        handPoseUploadCompleteImage.gameObject.SetActive(true);
        poseUploadCompleteImage.gameObject.SetActive(true);
        frameReader.ArrangeDataFrames();
#if UNITY_WEBGL && !UNITY_EDITOR
            string message = "Hello from Unity, I received your message";
             Debug.Log("SENDING MESSAGE BACK TO WEB------");
            //Application.ExternalCall("onDataRecieved", message);
            sendMessageToWeb(message);
            frameReader.OnTogglePlay();
            if (frameReader.pause)
                animationPlayButton.image.sprite = resumeImage;
            else
                animationPlayButton.image.sprite = pauseImage;
            canvas.gameObject.SetActive(false);
#endif
        if (!string.IsNullOrEmpty(NetworkManager.Instancce.commandLineText))
        {
            frameReader.OnTogglePlay();
            if (frameReader.pause)
                animationPlayButton.image.sprite = resumeImage;
            else
                animationPlayButton.image.sprite = pauseImage;
            canvas.gameObject.SetActive(false);
        }
        if (!string.IsNullOrEmpty(NetworkManager.Instancce.commandLineOutput) && !string.IsNullOrEmpty(NetworkManager.Instancce.commandLineFFMPEG))
        {
            if (SceneCapture.Instance != null)
            {
                SceneCapture.Instance.SetOutputFilePath(NetworkManager.Instancce.commandLineOutput);
                SceneCapture.Instance.SetFFMPEGFilePath(NetworkManager.Instancce.commandLineFFMPEG);
                SceneCapture.Instance.StartRecording();
            }
        }
    }
    
    
    public void OnUploadFacialExpressionClick()
    {
        faceUploadCircleImage.color = Color.white;
        faceUploadCompleteImage.gameObject.SetActive(false);
        #if UNITY_EDITOR
        string filePath = FileManager.OpenFileVideoExplorer();
        NetworkManager.Instancce.UploadFaceMoacap(filePath,(() =>
        {
            frameReader.SetFaceOriginalVideo(filePath);

        }));
        #endif
    }
    public void OnFaceDataReceived()
    {
        ShowSuccessMessage("Face data downloaded successfully!");
        faceUploadCircleImage.color = successDownloadColor;
        faceUploadCompleteImage.gameObject.SetActive(true);
        frameReader.ArrangeDataFrames();
    }
    

    
    //side panels
    public void SideBarPanelTrigger(Animator panel)
    {
        panel.SetBool("BarTrigger",!panel.GetBool("BarTrigger"));
    }
    public void SideBarButtonTrigger(Animator button)
    {
        button.SetTrigger("Rotate");
    }
    
    
    //Camera Zoom actions
    public void OnFaceZoomClicked()
    {
        faceZoomButton.SetActive(false);
        bodyZoomButton.SetActive(true);
        frameReader.SetFaceZoomCamera();
    }
    
    public void OnFullBodyZoomClicked()
    {
        faceZoomButton.SetActive(true);
        bodyZoomButton.SetActive(false);
        frameReader.SetBodyZoomCamera();
    }

    
    //Recorder
    public void OnRecorderClick()
    {
        frameReader.StartRecording();
    }

}
