using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Video;
using Random = UnityEngine.Random;
using System.Runtime.InteropServices;

public class NetworkManager : MonoSingleton<NetworkManager>
{



    [Header("Server")]
    [SerializeField] private string serverUploadURL;
    [SerializeField] private string serverFullPoseEstimatorURL;
    [SerializeField] private string serverFullPoseUploadURL;
    [SerializeField] private string serverPoseUploadURL;
    [SerializeField] private string serverPoseEstimatorURL;
    [SerializeField] private string serverHandUploadURL;
    [SerializeField] private string serverHandPoseEstimatorURL;

    [SerializeField] private string serverFaceUploadURL;
    [SerializeField] private string serverFaceMocapURL;

    [DllImport("__Internal")]
    private static extern void sendErrorToWeb(string message);

    [Header("Dependencies")] 
    [SerializeField] private FrameReader frameReader;
    public List<GameObject> nodes;



    
    //for testing in engine only
#if UNITY_EDITOR
    [Header("Debug")] 
    [SerializeField] private bool enableDebug; //if this is true we are in debug mode!
    [SerializeField] private string filePath; //for testing system
#endif




    [Serializable] 
    public struct UploadResponse
    {
        public string file;
        public int totalFrames;
        public float aspectRatio;
    }

    [Serializable]
    public struct WebDataObject
    {
        public string text;
        public int character;
        public string baseUrl;
        public float nextFrameTime;
        public string apiKey;
    }

    [Serializable] 
    public struct PoseRequest
    {
        public string fileName;
        public int index;
    }

    public static class JsonHelper
    {
        public static T[] FromJsonArray<T>(string json)
        {
            string newJson = "{ \"array\": " + json + "}";
            Wrapper<T> wrapper = JsonUtility.FromJson<Wrapper<T>>(newJson);
            return wrapper.array;
        }

        [System.Serializable]
        private class Wrapper<T>
        {
            public T[] array;
        }
    }

    [SerializeField] public string commandLineText = "";
    [SerializeField] public string commandLineOutput = "";
    [SerializeField] public string commandLineFFMPEG = "";
    [SerializeField] public string commandLineBaseUrl = "";
    [SerializeField] public string commandLineApiKey = "";
    [SerializeField] public int commandLineCharacter = 0;
    [SerializeField] public int apiType = -2; //-1: orignal api call; -2: our own api call
    [SerializeField] public float commandLineNextFrameTime = 0.00833f; // 1/120 = 0.00833f

    private void Start()
    {
        #if !UNITY_EDITOR && UNITY_WEBGL
            // disable WebGLInput.captureAllKeyboardInput so elements in web page can handle keyboard inputs
            WebGLInput.captureAllKeyboardInput = false;
            Debug.Log("captureAllKeyboardInput ____________: ");
        #endif
        //commandLineText = "hello how";
        //commandLineOutput = "/Users/ashar/Desktop/repo/spoken-to-signed-translation/temp/test1.mp4";
        //commandLineFFMPEG = "/Users/ashar/Desktop/repo/spoken-to-signed-translation/unity3d/mac/ffmpeg";
        //commandLineBaseUrl = "https://translate.deaftawk.com:3001/";
        //commandLineCharacter = 1;
        //commandLineNextFrameTime = 0.0833f;
        string[] args = System.Environment.GetCommandLineArgs();
        for (int index = 0; index < args.Length; index++)
        {
            if (args[index] == "-text" && (index + 1) < args.Length)
            {
                commandLineText = args[index + 1];
            }
            else if (args[index] == "-output" && (index + 1) < args.Length)
            {
                commandLineOutput = args[index + 1];
            }
            else if (args[index] == "-ffmpeg" && (index + 1) < args.Length)
            {
                commandLineFFMPEG = args[index + 1];
            }
            else if (args[index] == "-url" && (index + 1) < args.Length)
            {
                commandLineBaseUrl = args[index + 1];
            }
            else if (args[index] == "-apiKey" && (index + 1) < args.Length)
            {
                commandLineApiKey = args[index + 1];
            }
            else if (args[index] == "-character" && (index + 1) < args.Length)
            {
                string commandLineArg = args[index + 1];
                if (int.TryParse(commandLineArg, out int character))
                {
                    commandLineCharacter = character;
                }
            }
            else if (args[index] == "-nextFrameTime" && (index + 1) < args.Length) {
                string commandLineArg = args[index + 1];
                if (float.TryParse(commandLineArg, out float nextFrameTime))
                {
                    commandLineNextFrameTime = nextFrameTime;
                }
            }
        }
#if !UNITY_WEBGL
            if (!string.IsNullOrEmpty(commandLineBaseUrl))
            {
                Uri baseUri = new Uri(serverFullPoseUploadURL);
                string pathAndQuery = baseUri.PathAndQuery;
                if (commandLineBaseUrl.EndsWith("/"))
                {
                    commandLineBaseUrl = commandLineBaseUrl.TrimEnd('/');
                }
                serverFullPoseUploadURL = commandLineBaseUrl + pathAndQuery;
                if (!string.IsNullOrEmpty(commandLineApiKey))
                {
                    if (!serverFullPoseUploadURL.Contains("?"))
                    {
                        serverFullPoseUploadURL += $"?apikey={commandLineApiKey}";
                    }
                    else
                    {
                        serverFullPoseUploadURL += $"&apikey={commandLineApiKey}";
                    }
                }
            }
            if (!string.IsNullOrEmpty(commandLineText))
            {
                StartCoroutine(UploadText(commandLineText, serverFullPoseUploadURL, apiType, (response, bytes) =>
                {
                    this.frameReader.nextFrameTime = commandLineNextFrameTime;
                    if (commandLineCharacter >= 0 && commandLineCharacter < this.nodes.Count)
                    {
                        this.frameReader.SetNewCharacter(Instantiate(this.nodes[commandLineCharacter]));
                        this.frameReader.ShowCharacter();
                    }
                    StartCoroutine(GetFullBodyPoseEstimates(response, bytes, apiType));
                }));
            }
#endif
#if UNITY_EDITOR
        if (enableDebug)
            {
                StartCoroutine(Upload(filePath, serverFullPoseUploadURL, (response, bytes) =>
                {
                    StartCoroutine(GetFullBodyPoseEstimates(response,bytes));
                }));
            }
        #endif
        //StartCoroutine(CallRecievePoseTextRepeatedly(10f));
    }

    private IEnumerator CallRecievePoseTextRepeatedly(float intervalSeconds)
    {
        int index = 0;
        while (true)
        {
            string text = "{\"text\": \"" + "hello " + index + "\", \"character\": 0, \"baseUrl\": \"https://translate.deaftawk.com:3001\"}";
            RecievePoseText(text);
            index++;
            yield return new WaitForSeconds(intervalSeconds);
        }
    }

    public void RecievePoseText(string data)
    {
        Debug.Log("Pose text recieved from angular ____________: ");
        Debug.Log(data);
        WebDataObject webData = JsonUtility.FromJson<WebDataObject>(data);
        if (!string.IsNullOrEmpty(webData.text))
        {
            if (!string.IsNullOrEmpty(webData.baseUrl))
            {
                Uri baseUri = new Uri(serverFullPoseUploadURL);
                string pathAndQuery = baseUri.PathAndQuery;
                if (webData.baseUrl.EndsWith("/"))
                {
                    webData.baseUrl = webData.baseUrl.TrimEnd('/');
                }
                serverFullPoseUploadURL = webData.baseUrl + pathAndQuery;
                if (!string.IsNullOrEmpty(webData.apiKey))
                {
                    if (!serverFullPoseUploadURL.Contains("?"))
                    {
                        serverFullPoseUploadURL += $"?apikey={webData.apiKey}";
                    }
                    else
                    {
                        serverFullPoseUploadURL += $"&apikey={webData.apiKey}";
                    }
                }
            }
            StartCoroutine(UploadText(webData.text, serverFullPoseUploadURL, apiType, (response, bytes) =>
            {
                if (webData.nextFrameTime > 0)
                {
                    commandLineNextFrameTime = webData.nextFrameTime;
                }
                this.frameReader.nextFrameTime = commandLineNextFrameTime;
                commandLineCharacter = webData.character;
                if (commandLineCharacter >= 0 && commandLineCharacter < this.nodes.Count)
                {
                    this.frameReader.SetNewCharacter(Instantiate(this.nodes[commandLineCharacter]));
                    this.frameReader.ShowCharacter();
                }
                StartCoroutine(GetFullBodyPoseEstimates(response, bytes, apiType));
            }));
        }
    }

    //starting coroutine for sending ASync to server
    public void UploadAndEstimateFullPoseUsingText(string text, Action onSuccess = null)
    {
        StartCoroutine(UploadText(text, serverFullPoseUploadURL, apiType, (response, bytes) =>
        {
            StartCoroutine(GetFullBodyPoseEstimates(response, bytes));
            onSuccess?.Invoke();
        })); //Get estimates }));
    }

    //starting coroutine for sending ASync to server
    public void UploadImageGauGan(string localFileName,Action<UploadResponse,byte[]> onFinished)
    {
        StartCoroutine(Upload(localFileName, serverUploadURL,onFinished)); //Get estimates }));
    }
    
    

    
    
    //starting coroutine for sending ASync to server

    public void UploadFaceMoacap(string localFileName, Action onSuccess=null)
    {
        StartCoroutine(Upload(localFileName, serverFaceUploadURL, (response, bytes) =>
        {
            StartCoroutine(GetFaceMocap(response,bytes));
            onSuccess?.Invoke();
        })); //Get estimates }));
    }
    
    
    //starting coroutine for sending ASync to server

    public void UploadAndEstimatePose(string localFileName, Action onSuccess=null)
    {

        StartCoroutine(Upload(localFileName, serverPoseUploadURL, (response, bytes) =>
        {
            StartCoroutine(GetPoseEstimates(response,bytes)); 
            onSuccess?.Invoke();
        })); //Get estimates }));
    }
    
    
    //starting coroutine for sending ASync to server
    public void UploadAndEstimateHandPose(string localFileName, Action onSuccess=null)
    {
        StartCoroutine(Upload(localFileName, serverHandUploadURL, (response, bytes) =>
        {
            StartCoroutine(GetHandPoseEstimates(response,bytes));
            onSuccess?.Invoke();
        })); //Get estimates }));
    }
    
    //starting coroutine for sending ASync to server
    public void UploadAndEstimateFullPose(string localFileName, Action onSuccess=null)
    {
        StartCoroutine(Upload(localFileName, serverFullPoseUploadURL, (response, bytes) =>
        {
            StartCoroutine(GetFullBodyPoseEstimates(response,bytes,-1));
            onSuccess?.Invoke();
        })); //Get estimates }));
    }
    
    
    //Async file uploader method2
    IEnumerator UploadText(string text, string url, int type, Action<UploadResponse, byte[]> onFinishedUpload)
    {
        UnityWebRequest www;
        if (type == -2)
        {
            Uri baseUri = new Uri(url);
            string baseUrl = baseUri.Port > 0 ? $"{baseUri.Scheme}://{baseUri.Host}:{baseUri.Port}" : $"{baseUri.Scheme}://{baseUri.Host}";
            string newApiEndpoint = "/spoken_text_to_signed_pose";
            string fullUrl = baseUrl + newApiEndpoint;
            string queryParams = $"?text={UnityWebRequest.EscapeURL(text)}&spoken=en&signed=ase&myown=4&spell=true";
            fullUrl += queryParams;
            www = UnityWebRequest.Get(fullUrl);
        } else
        {
            WWWForm postForm = new WWWForm();
            postForm.AddField("text", text);
            www = UnityWebRequest.Post(url, postForm);
        }
        www.certificateHandler = new BypassCertificateValidation();
        UIManager.Instancce.CheckAndEnableWaitingModeUI(WaitingModeUI.Loading, true);
        yield return www.SendWebRequest();
        UIManager.Instancce.CheckAndEnableWaitingModeUI(WaitingModeUI.Loading, false);

        if (www.result != UnityWebRequest.Result.Success)
        {
            Debug.Log(www.error);
            #if !UNITY_EDITOR
                #if UNITY_WEBGL
                    //Application.ExternalCall("onDataRecieved", "Server Connection Failed!");
                    sendErrorToWeb("Server Connection Failed!");
                #endif
                if (!string.IsNullOrEmpty(NetworkManager.Instancce.commandLineText))
                {
                    Application.Quit();
                }
            #else
                UIManager.Instancce.ShowErrorMessage("Server Connection Failed!");
            #endif
        }
        else
        {
            byte[] results = www.downloadHandler.data;
            using (var stream = new MemoryStream(results))
            using (var binaryStream = new BinaryReader(stream))
            {
                Debug.Log(results.Length);
            }

            //Debug.Log(www.downloadHandler.text);
            try
            {
                if (type == -2)
                {
                    onFinishedUpload((new UploadResponse()), results);
                }
                else
                {
                    UploadResponse uploadResponse = JsonUtility.FromJson<UploadResponse>(www.downloadHandler.text);
                    onFinishedUpload(uploadResponse, results);
                }
            }
            catch (Exception e)
            {
                onFinishedUpload((new UploadResponse()), results);
                Console.WriteLine(e);
                throw;
            }
            Debug.Log("Upload complete!");
        }
    }


    //Async file uploader
    IEnumerator UploadFileCo(string localFileName, string uploadURL)
    {
        WWW localFile = new WWW("file:///" + localFileName);
        yield return localFile;
        if (localFile.error == null)
            Debug.Log("Loaded file successfully");
        else
        {
            Debug.Log("Open file error: "+localFile.error);
            yield break; // stop the coroutine here
        }
        WWWForm postForm = new WWWForm();
        postForm.AddBinaryData("file",localFile.bytes,localFileName,"text/plain");
        WWW upload = new WWW(uploadURL,postForm);        
        yield return upload;
        if (upload.error == null)
        {
            while (upload.MoveNext())
            {
                Debug.Log("upload done :" + upload.text);
                yield return upload;
            }
            Debug.Log(upload.isDone);
                Debug.Log("upload done :" + upload.text);
                Debug.Log(upload.bytes.Length);

        }
        else
            Debug.Log("Error during upload: " + upload.error);
    }

    //Async file uploader method2
    IEnumerator Upload(string localFileName, string url, Action<UploadResponse,byte[]> onFinishedUpload) {

        WWW localFile = new WWW("file:///" + localFileName);
        yield return localFile;
        if (localFile.error == null)
            Debug.Log("Loaded file successfully");
        else
        {
            Debug.Log("Open file error: "+localFile.error);
            yield break; // stop the coroutine here
        }
        WWWForm postForm = new WWWForm();

        postForm.AddBinaryData("file",localFile.bytes,localFileName,"text/plain");
        postForm.AddField("text", "1 2 3 4 5 6 7 8 9");

        UnityWebRequest www = UnityWebRequest.Post(url, postForm);
        www.certificateHandler = new BypassCertificateValidation();

        UIManager.Instancce.CheckAndEnableWaitingModeUI(WaitingModeUI.Loading,true);
        yield return www.SendWebRequest();
        UIManager.Instancce.CheckAndEnableWaitingModeUI(WaitingModeUI.Loading,false);
        
        if (www.result != UnityWebRequest.Result.Success) {
            Debug.Log(www.error);
            UIManager.Instancce.ShowErrorMessage("Server Connection Failed!");
        }
        else {
            byte[] results = www.downloadHandler.data;
            using (var stream = new MemoryStream(results))
            using (var binaryStream = new BinaryReader(stream))
            {             
                Debug.Log(results.Length);
            }
            
            //Debug.Log(www.downloadHandler.text);
            try
            {
                UploadResponse uploadResponse = JsonUtility.FromJson<UploadResponse>(www.downloadHandler.text);
                onFinishedUpload(uploadResponse,results);

            }
            catch (Exception e)
            {
                
                onFinishedUpload((new UploadResponse()),results);

                Console.WriteLine(e);
                throw;
            }
            //sending response to the action method
            Debug.Log("Upload complete!");
        }
    }



    //getting estimates for video facial mocap
    IEnumerator GetFaceMocap(UploadResponse reponse, byte[] bytes)
    {
        PoseRequest poseRequest = new PoseRequest();
        poseRequest.index = 0;
        poseRequest.fileName = reponse.file;

        float totalFrames = reponse.totalFrames;
        
        List<FaceJson> faceJsons = new List<FaceJson>();
        UIManager.Instancce.CheckAndEnableWaitingModeUI(WaitingModeUI.ProgressBar,true);
        UIManager.Instancce.UpdateProgressBar(0);

        while (true)
        {
            UnityWebRequest webRequest = new UnityWebRequest(serverFaceMocapURL, "POST");
            byte[] encodedPayload = new System.Text.UTF8Encoding().GetBytes(JsonUtility.ToJson(poseRequest));
            webRequest.uploadHandler = (UploadHandler) new UploadHandlerRaw(encodedPayload);
            webRequest.downloadHandler = (DownloadHandler) new DownloadHandlerBuffer();
            webRequest.SetRequestHeader("Content-Type", "application/json");
            webRequest.SetRequestHeader("cache-control", "no-cache");
            webRequest.certificateHandler = new BypassCertificateValidation();

            yield return webRequest.SendWebRequest();
            try
            {
                if (webRequest.result != UnityWebRequest.Result.Success)
                {
                    Debug.Log(webRequest.error);
                }
                else
                {
                    if (webRequest.downloadHandler.text.Equals("Done"))
                        break;
                    // Debug.Log(webRequest.downloadHandler.text);
                    FaceJson receivedJson = JsonUtility.FromJson<FaceJson>(webRequest.downloadHandler.text);
                    faceJsons.Add(receivedJson);
                    // Debug.Log(JsonUtility.FromJson<PoseJson>(webRequest.downloadHandler.text).frame);
                    poseRequest.index += 1;
                    UIManager.Instancce.UpdateProgressBar(((float)receivedJson.frame)/totalFrames);
                }

            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                UIManager.Instancce.CheckAndEnableWaitingModeUI(WaitingModeUI.ProgressBar,false);
                UIManager.Instancce.ShowErrorMessage("Error in downloading Facial Data!");
                throw;
            }

            yield return null;
        }
        UIManager.Instancce.UpdateProgressBar(1);
        yield return null;
        
        frameReader.SetFaceMocapList(faceJsons);
        UIManager.Instancce.OnFaceDataReceived();

        UIManager.Instancce.CheckAndEnableWaitingModeUI(WaitingModeUI.ProgressBar,false);

        yield break;
    }
    
    
 //getting estimates for video hand pose
    IEnumerator GetHandPoseEstimates(UploadResponse response, byte[] bytes)
    {
        PoseRequest poseRequest = new PoseRequest();
        poseRequest.index = 0;
        poseRequest.fileName = response.file;
        frameReader.SetVideoFractions(response.aspectRatio);

        float totalFrames = response.totalFrames;    
        
        List<HandJson> poseJsons = new List<HandJson>();
        UIManager.Instancce.CheckAndEnableWaitingModeUI(WaitingModeUI.ProgressBar,true);
        UIManager.Instancce.UpdateProgressBar(0);

        while (true)
        {
            UnityWebRequest webRequest = new UnityWebRequest(serverHandPoseEstimatorURL, "POST");
            byte[] encodedPayload = new System.Text.UTF8Encoding().GetBytes(JsonUtility.ToJson(poseRequest));
            webRequest.uploadHandler = (UploadHandler) new UploadHandlerRaw(encodedPayload);
            webRequest.downloadHandler = (DownloadHandler) new DownloadHandlerBuffer();
            webRequest.SetRequestHeader("Content-Type", "application/json");
            webRequest.SetRequestHeader("cache-control", "no-cache");

            yield return webRequest.SendWebRequest();
            try
            {
                if (webRequest.result != UnityWebRequest.Result.Success)
                {
                    Debug.Log(webRequest.error);
                }
                else
                {
                    if (webRequest.downloadHandler.text.Equals("Done"))
                        break;
                    HandJson receivedJson = JsonUtility.FromJson<HandJson>(webRequest.downloadHandler.text);
                    poseJsons.Add(receivedJson);
                    //Debug.Log(JsonUtility.FromJson<HandJson>(webRequest.downloadHandler.text).frame);
                    poseRequest.index += 1;
                    UIManager.Instancce.UpdateProgressBar(receivedJson.frame/totalFrames);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                UIManager.Instancce.CheckAndEnableWaitingModeUI(WaitingModeUI.ProgressBar,false);
                UIManager.Instancce.ShowErrorMessage("Error in downloading Pose Data!");
                throw;
            }

            yield return null;
        }
        UIManager.Instancce.UpdateProgressBar(1);
        yield return null;
        frameReader.SetHandPoseList(poseJsons);
        UIManager.Instancce.OnHandPoseDataReceived();

        UIManager.Instancce.CheckAndEnableWaitingModeUI(WaitingModeUI.ProgressBar,false);

        yield break;
    }
    
    
    
    
    //getting estimates for video pose
    IEnumerator GetPoseEstimates(UploadResponse response, byte[] bytes)
    {
        PoseRequest poseRequest = new PoseRequest();
        poseRequest.index = 0;
        poseRequest.fileName = response.file;
        frameReader.SetVideoFractions(response.aspectRatio);
        float totalFrames = response.totalFrames;    
        
        List<PoseJson> poseJsons = new List<PoseJson>();
        UIManager.Instancce.CheckAndEnableWaitingModeUI(WaitingModeUI.ProgressBar,true);
        UIManager.Instancce.UpdateProgressBar(0);

        while (true)
        {
            UnityWebRequest webRequest = new UnityWebRequest(serverPoseEstimatorURL, "POST");
            byte[] encodedPayload = new System.Text.UTF8Encoding().GetBytes(JsonUtility.ToJson(poseRequest));
            webRequest.uploadHandler = (UploadHandler) new UploadHandlerRaw(encodedPayload);
            webRequest.downloadHandler = (DownloadHandler) new DownloadHandlerBuffer();
            webRequest.SetRequestHeader("Content-Type", "application/json");
            webRequest.SetRequestHeader("cache-control", "no-cache");

            yield return webRequest.SendWebRequest();
            try
            {
                if (webRequest.result != UnityWebRequest.Result.Success)
                {
                    Debug.Log(webRequest.error);
                }
                else
                {
                    if (webRequest.downloadHandler.text.Equals("Done"))
                        break;
                    PoseJson receivedJson = JsonUtility.FromJson<PoseJson>(webRequest.downloadHandler.text);
                    poseJsons.Add(receivedJson);
                    //Debug.Log(JsonUtility.FromJson<PoseJson>(webRequest.downloadHandler.text).frame);
                    poseRequest.index += 1;
                    UIManager.Instancce.UpdateProgressBar(receivedJson.frame/totalFrames);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                UIManager.Instancce.CheckAndEnableWaitingModeUI(WaitingModeUI.ProgressBar,false);
                UIManager.Instancce.ShowErrorMessage("Error in downloading Pose Data!");
                throw;
            }

            yield return null;
        }
        UIManager.Instancce.UpdateProgressBar(1);
        yield return null;
        frameReader.SetPoseList(poseJsons);
        UIManager.Instancce.OnPoseDataReceived();

        UIManager.Instancce.CheckAndEnableWaitingModeUI(WaitingModeUI.ProgressBar,false);

        yield break;
    }
    
    
    //getting estimates for video body & hand poses
    IEnumerator GetFullBodyPoseEstimates(UploadResponse response, byte[] bytes, int start = 0)
    {
        PoseRequest poseRequest = new PoseRequest();
        poseRequest.index = start;
        //poseRequest.fileName = response.file;
        //frameReader.SetVideoFractions(response.aspectRatio);

        float totalFrames = 0;
        
        List<HandJson> handJsons = new List<HandJson>();
        List<PoseJson> bodyJsons = new List<PoseJson>();
        UIManager.Instancce.CheckAndEnableWaitingModeUI(WaitingModeUI.ProgressBar,true);
        UIManager.Instancce.UpdateProgressBar(0);

        if (start == -2)
        {
            poseRequest.fileName = "test";
            frameReader.SetVideoFractions(1.0f);
            try
            {
                string jsonResponse = System.Text.Encoding.UTF8.GetString(bytes);
                //Debug.Log("JSON Response: " + jsonResponse);

                // Manually parse the JSON array
                FullPoseJson[] receivedJsonArray = JsonHelper.FromJsonArray<FullPoseJson>(jsonResponse);
                totalFrames = receivedJsonArray.Length;
                foreach (var poseJson in receivedJsonArray)
                {
                    bodyJsons.Add(poseJson.bodyPose);
                    handJsons.Add(poseJson.handsPose);
                }
                UIManager.Instancce.UpdateProgressBar(1);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                UIManager.Instancce.CheckAndEnableWaitingModeUI(WaitingModeUI.ProgressBar, false);
                #if !UNITY_EDITOR
                    #if UNITY_WEBGL
                        //Application.ExternalCall("onDataRecieved", "Failed in downloading Full Body Pose Data!");
                        sendErrorToWeb("Failed in downloading Full Body Pose Data!");
                    #endif
                    if (!string.IsNullOrEmpty(NetworkManager.Instancce.commandLineText))
                    {
                        Application.Quit();
                    }
                #else
                    UIManager.Instancce.ShowErrorMessage("Failed in downloading Full Body Pose Data!");
                #endif
                throw;
            }
        }
        else if (start == -1)
        {
            poseRequest.fileName = response.file;
            frameReader.SetVideoFractions(response.aspectRatio);
            totalFrames = response.totalFrames;

            // Fetch all frames at once
            UnityWebRequest webRequest = new UnityWebRequest(serverFullPoseEstimatorURL, "POST");
            byte[] encodedPayload = new System.Text.UTF8Encoding().GetBytes(JsonUtility.ToJson(poseRequest));
            webRequest.uploadHandler = new UploadHandlerRaw(encodedPayload);
            webRequest.downloadHandler = new DownloadHandlerBuffer();
            webRequest.SetRequestHeader("Content-Type", "application/json");
            webRequest.SetRequestHeader("cache-control", "no-cache");
            webRequest.certificateHandler = new BypassCertificateValidation();

            yield return webRequest.SendWebRequest();
            try
            {
                if (webRequest.result != UnityWebRequest.Result.Success)
                {
                    Debug.Log(webRequest.error);
                }
                else
                {
                    string jsonResponse = webRequest.downloadHandler.text;
                    //Debug.Log("JSON Response: " + jsonResponse);

                    // Manually parse the JSON array
                    FullPoseJson[] receivedJsonArray = JsonHelper.FromJsonArray<FullPoseJson>(jsonResponse);
                    foreach (var poseJson in receivedJsonArray)
                    {
                        bodyJsons.Add(poseJson.bodyPose);
                        handJsons.Add(poseJson.handsPose);
                    }
                    UIManager.Instancce.UpdateProgressBar(1);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                UIManager.Instancce.CheckAndEnableWaitingModeUI(WaitingModeUI.ProgressBar,false);
                UIManager.Instancce.ShowErrorMessage("Error in downloading Full Body Pose Data!");
                throw;
            }
        }
        else
        {
            poseRequest.fileName = response.file;
            frameReader.SetVideoFractions(response.aspectRatio);
            totalFrames = response.totalFrames;

            // Fetch frame by frame
            while (true)
            {
                UnityWebRequest webRequest = new UnityWebRequest(serverFullPoseEstimatorURL, "POST");
                byte[] encodedPayload = new System.Text.UTF8Encoding().GetBytes(JsonUtility.ToJson(poseRequest));
                webRequest.uploadHandler = (UploadHandler)new UploadHandlerRaw(encodedPayload);
                webRequest.downloadHandler = (DownloadHandler)new DownloadHandlerBuffer();
                webRequest.SetRequestHeader("Content-Type", "application/json");
                webRequest.SetRequestHeader("cache-control", "no-cache");
                webRequest.certificateHandler = new BypassCertificateValidation();

                yield return webRequest.SendWebRequest();
                try
                {
                    if (webRequest.result != UnityWebRequest.Result.Success)
                    {
                        Debug.Log(webRequest.error);
                    }
                    else
                    {
                        if (webRequest.downloadHandler.text.Equals("Done"))
                            break;
                        FullPoseJson receivedJson = JsonUtility.FromJson<FullPoseJson>(webRequest.downloadHandler.text);
                        bodyJsons.Add(receivedJson.bodyPose);
                        handJsons.Add(receivedJson.handsPose);
                        //Debug.Log(JsonUtility.FromJson<HandJson>(webRequest.downloadHandler.text).frame);
                        poseRequest.index += 1;
                        UIManager.Instancce.UpdateProgressBar(receivedJson.frame / totalFrames);
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    UIManager.Instancce.CheckAndEnableWaitingModeUI(WaitingModeUI.ProgressBar, false);
                    UIManager.Instancce.ShowErrorMessage("Error in downloading Full Body Pose Data!");
                    throw;
                }

                yield return null;
            }
            UIManager.Instancce.UpdateProgressBar(1);
        }
        yield return null;
        frameReader.SetHandPoseList(handJsons);
        frameReader.SetPoseList(bodyJsons);
        UIManager.Instancce.OnFullPoseDataReceived();

        UIManager.Instancce.CheckAndEnableWaitingModeUI(WaitingModeUI.ProgressBar,false);

        yield break;
    }
    
    
    //getting GauGan image from the server
    IEnumerator GetGauGanImage(string serverResponse)
    {
        //Debug.Log(serverResponse);
        yield break;
    }
}

public class BypassCertificateValidation : CertificateHandler
{
    protected override bool ValidateCertificate(byte[] certificateData)
    {
        // Always return true to bypass validation
        return true;
    }
}