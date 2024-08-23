using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;

public class SceneCapture : MonoBehaviour
{
    public static SceneCapture Instance { get; private set; }
    private string ffmpegPath;
    private int frameRate = 60;
    private int framesToSkip = 3;

    private int width;
    private int height;
    private string outputFilePath;
    private Texture2D screenTexture;
    private Coroutine recordingCoroutine;
    private bool isRecording;
    private float timeBetweenFrames;
    private float timeSinceLastFrame;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        ffmpegPath = Path.Combine(Application.dataPath, "../ffmpeg");
        width = Screen.width;
        height = Screen.height;
        if (height % 2 != 0)
        {
            height -= 1;
        }
        screenTexture = new Texture2D(width, height, TextureFormat.RGB24, false);
        timeBetweenFrames = 1f / frameRate;
    }

    void OnDestroy()
    {
        if (recordingCoroutine != null)
        {
            StopCoroutine(recordingCoroutine);
        }
    }

    public void SetOutputFilePath(string path)
    {
        outputFilePath = path;
    }

    public void SetFFMPEGFilePath(string path)
    {
        ffmpegPath = path;
    }

    public void StartRecording()
    {
        if (recordingCoroutine == null)
        {
            framesToSkip = 3;
            isRecording = true;
            recordingCoroutine = StartCoroutine(Record());
        }
    }

    public void StopRecording()
    {
        if (recordingCoroutine != null)
        {
            isRecording = false;
            //StopCoroutine(recordingCoroutine);
            recordingCoroutine = null;
        }
    }

    private IEnumerator Record()
    {
        if (File.Exists(outputFilePath))
        {
            File.Delete(outputFilePath);
        }

        Process ffmpegProcess = new Process();
        ffmpegProcess.StartInfo.FileName = ffmpegPath;
        ffmpegProcess.StartInfo.Arguments = $"-y -f rawvideo -pixel_format rgb24 -video_size {width}x{height} -framerate 30 -i - -vf \"vflip\" -c:v libx264 -pix_fmt yuv420p -preset ultrafast \"{outputFilePath}\"";
        ffmpegProcess.StartInfo.UseShellExecute = false;
        ffmpegProcess.StartInfo.RedirectStandardInput = true;
        ffmpegProcess.StartInfo.CreateNoWindow = true;
        try
        {
            ffmpegProcess.Start();
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogError($"Failed to start FFmpeg process: {e.Message}");
            yield break;
        }

        while (isRecording)
        {
            timeSinceLastFrame += Time.deltaTime;

            if (timeSinceLastFrame >= timeBetweenFrames)
            {
                timeSinceLastFrame -= timeBetweenFrames;

                if (framesToSkip > 0)
                {
                    framesToSkip--;
                }
                else
                {
                    try
                    {
                        CaptureFrame();
                        byte[] rawData = screenTexture.GetRawTextureData();
                        ffmpegProcess.StandardInput.BaseStream.Write(rawData, 0, rawData.Length);
                        ffmpegProcess.StandardInput.BaseStream.Flush();
                    }
                    catch (Exception e)
                    {
                        UnityEngine.Debug.LogError($"Error during recording: {e.Message}");
                    }
                }
            }

            yield return new WaitForSeconds(timeBetweenFrames);
        }

        ffmpegProcess.StandardInput.Close();
        ffmpegProcess.WaitForExit();
    }

    private void CaptureFrame()
    {
        screenTexture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        screenTexture.Apply();
    }
}
